using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 모든 적의 공통 기반: 체력/경직/접촉 데미지/사망 처리.
    // 적별 행동은 파생 클래스에서 Move(매 물리 프레임 이동)와 필요하면 OnAliveUpdate/OnDied를 구현한다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(HitStun))]
    public abstract class EnemyController : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsDeadParam = Animator.StringToHash("IsDead");

        [Header("Contact Damage")]
        [SerializeField] private float contactDamage = 8f;
        [SerializeField] private float contactDamageCooldown = 1f;

        [Header("Death")]
        [SerializeField] private float destroyDelayAfterDeath = 1.5f;

        private HitStun hitStun;
        private Collider2D bodyCollider;
        private readonly Dictionary<HealthSystem, float> lastHitTime = new Dictionary<HealthSystem, float>();

        protected Rigidbody2D Rb { get; private set; }
        protected HealthSystem Health { get; private set; }
        protected Animator Animator { get; private set; }
        protected bool IsDead { get; private set; }

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            Health = GetComponent<HealthSystem>();
            hitStun = GetComponent<HitStun>();
            if (hitStun == null) hitStun = gameObject.AddComponent<HitStun>(); // 프리팹을 재생성하기 전의 구버전 대비
            bodyCollider = GetComponent<Collider2D>();
            Animator = GetComponentInChildren<Animator>();

            Health.HealthChanged += OnHealthChanged;
        }

        // 스포너가 적마다 다른 접촉 데미지를 지정할 때 사용.
        public void ConfigureContactDamage(float damage, float cooldown)
        {
            contactDamage = damage;
            contactDamageCooldown = cooldown;
        }

        // 생존 중 매 프레임 실행 (감지, 방향 전환 등 판단 로직).
        protected virtual void OnAliveUpdate() { }

        // 생존 중이고 경직 상태가 아닐 때 매 물리 프레임 실행 (이동 로직).
        protected abstract void Move();

        // 체력이 0이 되어 사망 처리에 들어간 직후 한 번 호출.
        protected virtual void OnDied() { }

        private void Update()
        {
            if (IsDead) return;

            OnAliveUpdate();

            if (Animator != null) Animator.SetFloat(SpeedParam, Mathf.Abs(Rb.linearVelocity.x));
        }

        private void FixedUpdate()
        {
            if (IsDead || hitStun.IsStunned)
            {
                Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
                return;
            }

            Move();
            ApplyContactDamage();
        }

        // 플레이어와 몸체 충돌은 꺼져 있어(밀어내지 않기 위해) 충돌 콜백 대신 겹침 조회로 접촉 데미지를 준다.
        private void ApplyContactDamage()
        {
            var bounds = bodyCollider.bounds;
            foreach (var hit in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f))
            {
                var targetHealth = hit.GetComponentInParent<HealthSystem>();
                if (targetHealth == null || targetHealth == Health) continue;
                if (targetHealth.GetComponent<EnemyController>() != null) continue; // 적끼리는 피해 없음

                if (lastHitTime.TryGetValue(targetHealth, out float last) && Time.time - last < contactDamageCooldown) continue;

                targetHealth.TakeDamage(contactDamage);
                lastHitTime[targetHealth] = Time.time;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current > 0f || IsDead) return;

            IsDead = true;
            if (Animator != null) Animator.SetBool(IsDeadParam, true);
            OnDied();
            Destroy(gameObject, destroyDelayAfterDeath);
        }

        protected virtual void OnDestroy()
        {
            if (Health != null) Health.HealthChanged -= OnHealthChanged;
        }
    }
}
