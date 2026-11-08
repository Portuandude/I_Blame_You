using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 근접 고블린류(고블린, 고블린 병사, 고블린 대전사) 공통 행동:
    // 감지 거리 안이면 추적하고, 공격 사거리 안이면 멈춰서 휘두른다 (예비 동작 → 판정 → 회복).
    // 공격 애니메이션(6프레임)의 3번째 프레임이 끝나는 순간 판정이 나가도록 시간을 맞춘다.
    public class GoblinController : EnemyController
    {
        private enum AttackPhase
        {
            None,
            Windup,
            Recover
        }

        public const float AttackFrameRate = 10f; // GameAssetSetup이 이 프레임레이트로 공격 클립을 만든다
        private const int WindupFrames = 3;
        private const int RecoverFrames = 3;
        private const float WindupTime = WindupFrames / AttackFrameRate;
        private const float RecoverTime = RecoverFrames / AttackFrameRate;
        private const float ArrivalTolerance = 0.2f;

        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");

        [Header("Behaviour")]
        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private float loseRangeMultiplier = 1.25f;
        [SerializeField] private float patrolRadius = 3f;
        [SerializeField] private float patrolSpeedRatio = 0.5f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.2f, 1f);
        [SerializeField] private float hitboxForward = 0.7f;
        [SerializeField] private float hitboxHeight = 0.5f;

        [Header("Wall Check")]
        [SerializeField] private float wallCheckDistance = 0.6f;
        [SerializeField] private float wallCheckHeight = 0.4f;
        [SerializeField] private LayerMask obstacleLayer;

        private readonly HashSet<HealthSystem> struck = new HashSet<HealthSystem>();

        private float facing = -1f;
        private float moveDirection;
        private float homeX;
        private bool chasing;
        private AttackPhase phase;
        private float phaseTimeRemaining;
        private float nextAttackTime;

        protected override bool ContactDamageActive => phase == AttackPhase.None;

        public void Configure(float detection, float range, Vector2 hitbox, float forward, float height)
        {
            detectionRange = detection;
            attackRange = range;
            hitboxSize = hitbox;
            hitboxForward = forward;
            hitboxHeight = height;
        }

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
            // 광폭화처럼 공격 속도 배율이 오르면 애니메이션도 그만큼 빨라진다.
            if (Animator != null) Animator.speed = Stats.AttackSpeedMultiplier;

            var target = FindTarget();
            UpdateChaseState(target);

            if (phase != AttackPhase.None)
            {
                UpdateAttack();
                return;
            }

            if (chasing && TryStartAttack(target)) return;

            if (chasing)
            {
                float dx = target.position.x - transform.position.x;
                moveDirection = Mathf.Abs(dx) > ArrivalTolerance ? Mathf.Sign(dx) : 0f;
                if (moveDirection != 0f) SetFacing(moveDirection);
                if (Mathf.Abs(dx) < attackRange * 0.8f) moveDirection = 0f; // 바짝 붙지 않고 사거리에서 멈춤
                if (moveDirection != 0f && WallAhead(moveDirection)) moveDirection = 0f;
                return;
            }

            float offsetFromHome = transform.position.x - homeX;
            if (Mathf.Abs(offsetFromHome) > patrolRadius) SetFacing(-Mathf.Sign(offsetFromHome));
            else if (WallAhead(facing)) SetFacing(-facing);
            moveDirection = facing;
        }

        protected override void Move()
        {
            if (phase != AttackPhase.None)
            {
                Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
                return;
            }

            float speed = Stats.MoveSpeedUnits * (chasing ? 1f : patrolSpeedRatio);
            Rb.linearVelocity = new Vector2(moveDirection * speed, Rb.linearVelocity.y);
        }

        protected override void OnDied()
        {
            EndAttack(false);
            if (Animator != null) Animator.speed = 1f;
        }

        private bool TryStartAttack(Transform target)
        {
            if (target == null || Time.time < nextAttackTime || IsStunned) return false;

            float dx = target.position.x - transform.position.x;
            float dy = Mathf.Abs(target.position.y - transform.position.y);
            if (Mathf.Abs(dx) > attackRange || dy > 1.5f) return false;

            SetFacing(dx >= 0f ? 1f : -1f);
            moveDirection = 0f;
            phase = AttackPhase.Windup;
            phaseTimeRemaining = WindupTime / Stats.AttackSpeedMultiplier;
            if (Animator != null) Animator.SetBool(IsAttackingParam, true);
            return true;
        }

        private void UpdateAttack()
        {
            if (IsStunned)
            {
                EndAttack(true);
                return;
            }

            phaseTimeRemaining -= Time.deltaTime;
            if (phaseTimeRemaining > 0f) return;

            if (phase == AttackPhase.Windup)
            {
                Swing();
                phase = AttackPhase.Recover;
                phaseTimeRemaining = RecoverTime / Stats.AttackSpeedMultiplier;
            }
            else
            {
                EndAttack(true);
            }
        }

        private void Swing()
        {
            struck.Clear();
            Vector2 center = (Vector2)transform.position + new Vector2(facing * hitboxForward, hitboxHeight);
            foreach (var hit in Physics2D.OverlapBoxAll(center, hitboxSize, 0f))
            {
                var targetHealth = hit.GetComponentInParent<HealthSystem>();
                if (targetHealth == null || !struck.Add(targetHealth)) continue;
                StrikePlayer(targetHealth);
            }

            EffectLibrary.PlayOneShot(center, EffectLibrary.SlashFrames, 24f, new Color(1f, 1f, 1f, 0.5f), 0.8f, facing < 0f);
        }

        private void EndAttack(bool startCooldown)
        {
            phase = AttackPhase.None;
            if (startCooldown) nextAttackTime = Time.time + Stats.AttackInterval;
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
            return LedgeAhead(direction, obstacleLayer) || Physics2D.Raycast(origin, new Vector2(direction, 0f), wallCheckDistance, obstacleLayer);
        }

        private void SetFacing(float direction)
        {
            facing = direction;
            transform.localScale = new Vector3(direction, 1f, 1f);
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 center = (Vector2)transform.position + new Vector2(facing * hitboxForward, hitboxHeight);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, hitboxSize);
        }
    }
}
