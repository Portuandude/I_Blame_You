using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 가장 기본적인 쫄몹: 벽에 부딪힐 때까지 좌우로 왕복하고, 닿으면 접촉 데미지를 준다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(HitStun))]
    public class EnemyController : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsDeadParam = Animator.StringToHash("IsDead");

        [Header("Patrol")]
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float wallCheckDistance = 0.6f;
        [SerializeField] private float wallCheckHeight = 0.4f;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Contact Damage")]
        [SerializeField] private float contactDamage = 8f;
        [SerializeField] private float contactDamageCooldown = 1f;

        private Rigidbody2D rb;
        private HealthSystem health;
        private HitStun hitStun;
        private Collider2D body;
        private Animator animator;
        private float facing = -1f;
        private bool isDead;
        private readonly Dictionary<HealthSystem, float> lastHitTime = new Dictionary<HealthSystem, float>();

        // 스포너가 이 몹이 서 있는 바닥/벽의 레이어를 알려줄 때 사용.
        public void ConfigureObstacleLayer(LayerMask layer)
        {
            obstacleLayer = layer;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<HealthSystem>();
            hitStun = GetComponent<HitStun>();
            if (hitStun == null) hitStun = gameObject.AddComponent<HitStun>(); // 프리팹을 재생성하기 전의 구버전 대비
            body = GetComponent<Collider2D>();
            animator = GetComponentInChildren<Animator>();

            health.HealthChanged += OnHealthChanged;
        }

        private void Update()
        {
            if (isDead) return;

            Vector2 origin = (Vector2)transform.position + new Vector2(0f, wallCheckHeight);
            bool blocked = Physics2D.Raycast(origin, new Vector2(facing, 0f), wallCheckDistance, obstacleLayer);
            if (blocked)
            {
                facing = -facing;
                transform.localScale = new Vector3(facing, 1f, 1f);
            }

            if (animator != null) animator.SetFloat(SpeedParam, Mathf.Abs(rb.linearVelocity.x));
        }

        private void FixedUpdate()
        {
            if (isDead || hitStun.IsStunned)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                return;
            }

            rb.linearVelocity = new Vector2(facing * patrolSpeed, rb.linearVelocity.y);
            ApplyContactDamage();
        }

        // 플레이어와 몸체 충돌은 꺼져 있어(밀어내지 않기 위해) 충돌 콜백 대신 겹침 조회로 접촉 데미지를 준다.
        private void ApplyContactDamage()
        {
            var bounds = body.bounds;
            foreach (var hit in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f))
            {
                var targetHealth = hit.GetComponentInParent<HealthSystem>();
                if (targetHealth == null || targetHealth == health) continue;
                if (targetHealth.GetComponent<EnemyController>() != null) continue; // 적끼리는 피해 없음

                if (lastHitTime.TryGetValue(targetHealth, out float last) && Time.time - last < contactDamageCooldown) continue;

                targetHealth.TakeDamage(contactDamage);
                lastHitTime[targetHealth] = Time.time;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current > 0f || isDead) return;

            isDead = true;
            if (animator != null) animator.SetBool(IsDeadParam, true);
            Destroy(gameObject, 1.5f);
        }

        private void OnDestroy()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
        }
    }
}
