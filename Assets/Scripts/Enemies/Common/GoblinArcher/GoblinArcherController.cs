using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 고블린 궁수: 플레이어와 거리를 유지하며(너무 가까우면 물러나고, 멀면 다가감) 활을 쏜다.
    // 시위를 당기는 동안(공격 애니메이션 앞 4프레임) 멈춰 있다가 화살을 놓는다. 화살 피해 = 물리 데미지.
    public class GoblinArcherController : EnemyController
    {
        public const float AttackFrameRate = 10f;
        private const int DrawFrames = 4;
        private const int RecoverFrames = 2;
        private const float DrawTime = DrawFrames / AttackFrameRate;
        private const float RecoverTime = RecoverFrames / AttackFrameRate;

        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");

        [SerializeField] private float detectionRange = 14f;
        [SerializeField] private float preferredMinDistance = 4f;
        [SerializeField] private float shootRange = 10f;
        [SerializeField] private float arrowSpeed = 13f;
        [SerializeField] private float arrowHeight = 0.55f;
        [SerializeField] private LayerMask obstacleLayer;

        private float facing = -1f;
        private float moveDirection;
        private bool drawing;
        private bool recovering;
        private float phaseTimeRemaining;
        private float nextShotTime;

        public void ConfigureObstacleLayer(LayerMask layer)
        {
            obstacleLayer = layer;
        }

        private void Start()
        {
            SetFacing(facing);
            nextShotTime = Time.time + 1f; // 방에 들어오자마자 쏘지 않도록
        }

        protected override void OnAliveUpdate()
        {
            var target = FindTarget();
            moveDirection = 0f;

            if (drawing || recovering)
            {
                UpdateShot(target);
                return;
            }

            if (target == null) return;

            float dx = target.position.x - transform.position.x;
            float distance = Vector2.Distance(transform.position, target.position);
            if (distance > detectionRange) return;

            SetFacing(dx >= 0f ? 1f : -1f);

            if (Mathf.Abs(dx) < preferredMinDistance) moveDirection = -Mathf.Sign(dx); // 뒷걸음
            else if (distance > shootRange) moveDirection = Mathf.Sign(dx);

            if (moveDirection != 0f && WallAhead(moveDirection)) moveDirection = 0f;

            if (distance <= shootRange && Time.time >= nextShotTime && !IsStunned)
            {
                drawing = true;
                moveDirection = 0f;
                phaseTimeRemaining = DrawTime;
                if (Animator != null) Animator.SetBool(IsAttackingParam, true);
            }
        }

        protected override void Move()
        {
            // 뒷걸음질은 앞으로 걸을 때보다 느리게.
            float speed = Stats.MoveSpeedUnits * (moveDirection == facing ? 1f : 0.7f);
            Rb.linearVelocity = new Vector2(moveDirection * speed, Rb.linearVelocity.y);
        }

        protected override void OnDied()
        {
            drawing = recovering = false;
            if (Animator != null) Animator.SetBool(IsAttackingParam, false);
        }

        private void UpdateShot(Transform target)
        {
            if (IsStunned)
            {
                EndShot();
                return;
            }

            phaseTimeRemaining -= Time.deltaTime;
            if (phaseTimeRemaining > 0f) return;

            if (drawing)
            {
                drawing = false;
                Release(target);
                recovering = true;
                phaseTimeRemaining = RecoverTime;
            }
            else
            {
                EndShot();
            }
        }

        private void Release(Transform target)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(facing * 0.4f, arrowHeight);
            Vector2 aimAt = target != null ? (Vector2)target.position : origin + Vector2.right * facing;
            var direction = aimAt - origin;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right * facing;

            var go = new GameObject("Arrow");
            go.transform.position = origin;
            EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.ArrowFrames, 12f, true, new Color(0.9f, 0.8f, 0.5f), 0.5f);
            go.AddComponent<Projectile>().Launch(Stats, false, DamageKind.Physical, Element.Neutral, direction, arrowSpeed, 0.2f,
                shootRange * 1.6f, false);
        }

        private void EndShot()
        {
            drawing = recovering = false;
            nextShotTime = Time.time + Stats.AttackInterval;
            if (Animator != null) Animator.SetBool(IsAttackingParam, false);
        }

        private bool WallAhead(float direction)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.4f);
            return LedgeAhead(direction, obstacleLayer) || Physics2D.Raycast(origin, new Vector2(direction, 0f), 0.6f, obstacleLayer);
        }

        private void SetFacing(float direction)
        {
            facing = direction;
            transform.localScale = new Vector3(direction, 1f, 1f);
        }
    }
}
