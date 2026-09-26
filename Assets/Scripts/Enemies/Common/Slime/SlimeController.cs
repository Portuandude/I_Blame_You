using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임: 감지 거리 안에 플레이어가 오면 쫓아오고, 없으면 짧은 거리를 왕복하거나 가만히 서 있는다.
    // 접촉 데미지/경직/사망은 EnemyController 공통 처리.
    public class SlimeController : EnemyController
    {
        public enum IdleBehaviour
        {
            Patrol, // 출발 위치 기준 patrolRadius 안에서 왕복
            Stand   // 가만히 서 있음
        }

        [Header("Idle (플레이어가 근처에 없을 때)")]
        [SerializeField] private IdleBehaviour idleBehaviour = IdleBehaviour.Patrol;
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float patrolRadius = 3f;

        [Header("Chase")]
        [SerializeField] private float chaseSpeed = 2.5f;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private float loseRangeMultiplier = 1.25f; // 추적을 놓치는 거리 = detectionRange × 배수 (경계에서 깜빡임 방지)

        [Header("Wall Check")]
        [SerializeField] private float wallCheckDistance = 0.6f;
        [SerializeField] private float wallCheckHeight = 0.4f;
        [SerializeField] private LayerMask obstacleLayer;

        private const float ArrivalTolerance = 0.2f;

        private float facing = -1f;
        private float moveDirection;
        private float homeX;
        private bool chasing;

        // 스포너가 적의 수치를 지정할 때 사용.
        public void Configure(IdleBehaviour idle, float idleSpeed, float idleRadius, float chase, float detection)
        {
            idleBehaviour = idle;
            patrolSpeed = idleSpeed;
            patrolRadius = idleRadius;
            chaseSpeed = chase;
            detectionRange = detection;
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
            UpdateChaseState(FindTarget());

            if (chasing)
            {
                float dx = FindTarget().position.x - transform.position.x;
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
            float speed = chasing ? chaseSpeed : patrolSpeed;
            Rb.linearVelocity = new Vector2(moveDirection * speed, Rb.linearVelocity.y);
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
