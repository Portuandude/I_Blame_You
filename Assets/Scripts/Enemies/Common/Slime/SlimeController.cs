using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임: 벽에 부딪힐 때까지 좌우로 왕복 이동. 접촉 데미지/경직/사망은 EnemyController 공통 처리.
    public class SlimeController : EnemyController
    {
        [Header("Patrol")]
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float wallCheckDistance = 0.6f;
        [SerializeField] private float wallCheckHeight = 0.4f;
        [SerializeField] private LayerMask obstacleLayer;

        private float facing = -1f;

        // 스포너가 이 몹이 서 있는 바닥/벽의 레이어를 알려줄 때 사용.
        public void ConfigureObstacleLayer(LayerMask layer)
        {
            obstacleLayer = layer;
        }

        protected override void OnAliveUpdate()
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, wallCheckHeight);
            bool blocked = Physics2D.Raycast(origin, new Vector2(facing, 0f), wallCheckDistance, obstacleLayer);
            if (blocked)
            {
                facing = -facing;
                transform.localScale = new Vector3(facing, 1f, 1f);
            }
        }

        protected override void Move()
        {
            Rb.linearVelocity = new Vector2(facing * patrolSpeed, Rb.linearVelocity.y);
        }
    }
}
