using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임: 감지 거리 안에 플레이어가 오면 쫓아오고, 없으면 짧은 거리를 왕복하거나 가만히 서 있는다.
    // 공격 사거리 안에 들어오면 웅크렸다가(윈드업) 플레이어 쪽으로 도약해 덮친다(공중→착지 후 회복).
    // 접촉 데미지/경직/사망은 EnemyController 공통 처리.
    public class SlimeController : EnemyController
    {
        public enum IdleBehaviour
        {
            Patrol, // 출발 위치 기준 patrolRadius 안에서 왕복
            Stand   // 가만히 서 있음
        }

        private enum AttackPhase
        {
            None,
            Windup,  // 웅크림 (프레임 0~2)
            Air,     // 도약 ~ 낙하 (프레임 3~6), 이 동안 몸통이 닿으면 공격 피해
            Recover  // 착지 스플랫 ~ 원상 복귀 (프레임 7~9)
        }

        // 공격 애니메이션(Slime_Attack, 프레임 10장)의 구간 길이와 물리 타이밍을 한곳에서 맞춘다.
        // GameAssetSetup이 이 프레임레이트로 클립을 만든다.
        public const float AttackFrameRate = 8f;
        private const int WindupFrames = 3;
        private const int AirFrames = 4;
        private const int RecoverFrames = 3;
        private const float WindupTime = WindupFrames / AttackFrameRate;
        private const float AirTime = AirFrames / AttackFrameRate;
        private const float RecoverTime = RecoverFrames / AttackFrameRate;

        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");

        [Header("Idle (플레이어가 근처에 없을 때)")]
        [SerializeField] private IdleBehaviour idleBehaviour = IdleBehaviour.Patrol;
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float patrolRadius = 3f;

        [Header("Chase")]
        [SerializeField] private float chaseSpeed = 2.5f;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private float loseRangeMultiplier = 1.25f; // 추적을 놓치는 거리 = detectionRange × 배수 (경계에서 깜빡임 방지)

        [Header("Attack (도약 덮치기)")]
        [SerializeField] private float attackRange = 4f;
        [SerializeField] private float attackDamage = 12f;
        [SerializeField] private float attackCooldown = 2f;
        [SerializeField] private float maxLeapSpeed = 8f;

        [Header("Wall Check")]
        [SerializeField] private float wallCheckDistance = 0.6f;
        [SerializeField] private float wallCheckHeight = 0.4f;
        [SerializeField] private LayerMask obstacleLayer;

        private const float ArrivalTolerance = 0.2f;

        private readonly HashSet<HealthSystem> attackedThisLeap = new HashSet<HealthSystem>();

        private float facing = -1f;
        private float moveDirection;
        private float homeX;
        private bool chasing;

        private AttackPhase phase;
        private float phaseTimeRemaining;
        private float nextAttackTime;
        private bool leapPending;
        private Vector2 leapVelocity;

        protected override bool ContactDamageActive => phase == AttackPhase.None;

        // 스포너가 적의 수치를 지정할 때 사용.
        public void Configure(IdleBehaviour idle, float idleSpeed, float idleRadius, float chase, float detection)
        {
            idleBehaviour = idle;
            patrolSpeed = idleSpeed;
            patrolRadius = idleRadius;
            chaseSpeed = chase;
            detectionRange = detection;
        }

        public void ConfigureAttack(float range, float damage, float cooldown, float leapSpeed)
        {
            attackRange = range;
            attackDamage = damage;
            attackCooldown = cooldown;
            maxLeapSpeed = leapSpeed;
        }

        // 스포너가 이 몹이 서 있는 바닥/벽의 레이어를 알려줄 때 사용.
        public void ConfigureObstacleLayer(LayerMask layer)
        {
            obstacleLayer = layer;
        }

        private void Start()
        {
            homeX = transform.position.x;
            SetFacing(facing);
        }

        protected override void OnAliveUpdate()
        {
            var target = FindTarget();
            UpdateChaseState(target);

            if (phase != AttackPhase.None)
            {
                UpdateAttack(target);
                return;
            }

            if (chasing && TryStartAttack(target)) return;

            if (chasing)
            {
                float dx = target.position.x - transform.position.x;
                moveDirection = Mathf.Abs(dx) > ArrivalTolerance ? Mathf.Sign(dx) : 0f;
                if (moveDirection != 0f) SetFacing(moveDirection);

                // 추적 중 벽에 막히면 뒤돌지 않고 그 자리에서 멈춘다.
                if (moveDirection != 0f && WallAhead(moveDirection)) moveDirection = 0f;
                return;
            }

            if (idleBehaviour == IdleBehaviour.Stand)
            {
                moveDirection = 0f;
                return;
            }

            // 왕복: 벽에 닿거나 출발 위치 반경을 벗어나면 방향 전환. 추적 후 멀어졌다면 출발 위치 쪽으로 돌아온다.
            float offsetFromHome = transform.position.x - homeX;
            if (Mathf.Abs(offsetFromHome) > patrolRadius)
            {
                SetFacing(-Mathf.Sign(offsetFromHome));
            }
            else if (WallAhead(facing))
            {
                SetFacing(-facing);
            }

            moveDirection = facing;
        }

        protected override void Move()
        {
            switch (phase)
            {
                case AttackPhase.Windup:
                case AttackPhase.Recover:
                    Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
                    return;

                case AttackPhase.Air:
                    if (leapPending)
                    {
                        Rb.linearVelocity = leapVelocity;
                        leapPending = false;
                    }

                    // 공중에서는 물리에 맡기고, 몸통이 닿은 대상에게 한 번씩만 공격 피해를 준다.
                    ApplyAttackDamage();
                    return;
            }

            float speed = chasing ? chaseSpeed : patrolSpeed;
            Rb.linearVelocity = new Vector2(moveDirection * speed, Rb.linearVelocity.y);
        }

        protected override void OnDied()
        {
            EndAttack(startCooldown: false);
        }

        private bool TryStartAttack(Transform target)
        {
            if (target == null || Time.time < nextAttackTime || IsStunned) return false;
            if (Mathf.Abs(Rb.linearVelocity.y) > 0.1f) return false; // 착지해 있을 때만

            float dx = target.position.x - transform.position.x;
            if (Mathf.Abs(dx) > attackRange) return false;

            SetFacing(dx >= 0f ? 1f : -1f);
            moveDirection = 0f;
            attackedThisLeap.Clear();
            EnterPhase(AttackPhase.Windup, WindupTime);
            if (Animator != null) Animator.SetBool(IsAttackingParam, true);
            return true;
        }

        private void UpdateAttack(Transform target)
        {
            // 경직(피격)되면 공격이 끊기고 재사용 대기에 들어간다.
            if (IsStunned)
            {
                EndAttack(startCooldown: true);
                return;
            }

            phaseTimeRemaining -= Time.deltaTime;
            if (phaseTimeRemaining > 0f) return;

            switch (phase)
            {
                case AttackPhase.Windup:
                    BeginLeap(target);
                    EnterPhase(AttackPhase.Air, AirTime);
                    break;
                case AttackPhase.Air:
                    EnterPhase(AttackPhase.Recover, RecoverTime);
                    break;
                case AttackPhase.Recover:
                    EndAttack(startCooldown: true);
                    break;
            }
        }

        // 체공 시간이 AirTime이 되도록 수직 속도를 정하고, 수평은 플레이어 쪽으로 (사거리 안이면 정확히 그 위치까지).
        private void BeginLeap(Transform target)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y) * Rb.gravityScale;
            float vy = gravity * AirTime / 2f;

            float dx = target != null ? target.position.x - transform.position.x : facing * attackRange;
            float vx = Mathf.Clamp(dx / AirTime, -maxLeapSpeed, maxLeapSpeed);
            if (Mathf.Abs(vx) > 0.01f) SetFacing(Mathf.Sign(vx));

            leapVelocity = new Vector2(vx, vy);
            leapPending = true;
        }

        private void ApplyAttackDamage()
        {
            var bounds = BodyBounds;
            foreach (var hit in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f))
            {
                var targetHealth = hit.GetComponentInParent<HealthSystem>();
                if (targetHealth == null || targetHealth == Health) continue;
                if (targetHealth.GetComponent<EnemyController>() != null) continue; // 적끼리는 피해 없음
                if (!attackedThisLeap.Add(targetHealth)) continue;

                targetHealth.TakeDamage(attackDamage);
            }
        }

        private void EnterPhase(AttackPhase next, float duration)
        {
            phase = next;
            phaseTimeRemaining = duration;
        }

        private void EndAttack(bool startCooldown)
        {
            phase = AttackPhase.None;
            leapPending = false;
            if (startCooldown) nextAttackTime = Time.time + attackCooldown;
            if (Animator != null) Animator.SetBool(IsAttackingParam, false);
        }

        private void UpdateChaseState(Transform target)
        {
            if (target == null)
            {
                chasing = false;
                return;
            }

            float distance = Vector2.Distance(transform.position, target.position);
            chasing = chasing ? distance <= detectionRange * loseRangeMultiplier : distance <= detectionRange;
        }

        private bool WallAhead(float direction)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, wallCheckHeight);
            return Physics2D.Raycast(origin, new Vector2(direction, 0f), wallCheckDistance, obstacleLayer);
        }

        private void SetFacing(float direction)
        {
            facing = direction;
            transform.localScale = new Vector3(direction, 1f, 1f);
        }
    }
}
