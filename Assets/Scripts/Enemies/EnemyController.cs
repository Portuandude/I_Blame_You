using System;
using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 모든 적의 공통 기반: 체력/경직/접촉 데미지/사망 처리. 수치는 CombatStats(스탯 문서 값)에서 읽는다.
    // 적별 행동은 파생 클래스에서 Move(매 물리 프레임 이동)와 필요하면 OnAliveUpdate/OnDied를 구현한다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(HitStun))]
    [RequireComponent(typeof(CombatStats))]
    public abstract class EnemyController : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsDeadParam = Animator.StringToHash("IsDead");

        // 몸통 접촉 피해 = 물리 데미지 × 이 배율 (스탯 문서에 접촉 피해 항목이 없어 가안으로 30%).
        [Header("Contact Damage")]
        [SerializeField] private float contactDamageMultiplier = 0.3f;
        [SerializeField] private float contactDamageCooldown = 1f;

        [Header("Death")]
        [SerializeField] private float destroyDelayAfterDeath = 1.5f;

        private const float TargetSearchInterval = 0.5f;
        private const float LedgeDropLimit = 3f;

        private HitStun hitStun;
        private Collider2D bodyCollider;
        private Transform target;
        private float nextTargetSearchTime;
        private readonly Dictionary<HealthSystem, float> lastHitTime = new Dictionary<HealthSystem, float>();

        // 적이 죽을 때 (적, 드랍 경험치). HUD가 경험치를 모은다.
        public static event Action<EnemyController, float> Killed;

        protected Rigidbody2D Rb { get; private set; }
        protected HealthSystem Health { get; private set; }
        protected Animator Animator { get; private set; }
        protected CombatStats Stats { get; private set; }
        protected bool IsDead { get; private set; }
        protected bool IsStunned => hitStun.IsStunned;
        protected Bounds BodyBounds => bodyCollider.bounds;

        // 적이 자체 공격 중일 때처럼 기본 접촉 데미지를 끄고 싶으면 파생 클래스에서 false를 반환한다.
        protected virtual bool ContactDamageActive => true;

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            Rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 카메라가 움직일 때 적도 번져 보이지 않게 (PlayerMovement 참고)
            Health = GetComponent<HealthSystem>();
            hitStun = GetComponent<HitStun>();
            if (hitStun == null) hitStun = gameObject.AddComponent<HitStun>(); // 프리팹을 재생성하기 전의 구버전 대비
            bodyCollider = GetComponent<Collider2D>();
            Animator = GetComponentInChildren<Animator>();
            Stats = GetComponent<CombatStats>();

            Health.HealthChanged += OnHealthChanged;
        }

        // 스포너가 적마다 다른 접촉 데미지를 지정할 때 사용.
        public void ConfigureContactDamage(float damageMultiplier, float cooldown)
        {
            contactDamageMultiplier = damageMultiplier;
            contactDamageCooldown = cooldown;
        }

        // 속성 공격을 하는 적(속성 슬라임)이면 공격(도약 등)이 그 속성의 마법 피해가 된다. 접촉 피해는 계속 물리.
        public bool HasAttackElement { get; private set; }
        public Element AttackElement { get; private set; }

        public void SetAttackElement(Element element)
        {
            HasAttackElement = true;
            AttackElement = element;
        }

        // 공격 판정이 닿은 대상에게 피해를 준다 (적끼리는 피해 없음). 피해를 줬으면 true.
        // 공격 속성이 있으면 그 속성의 마법 피해, 없으면 물리 피해.
        protected bool StrikePlayer(Component target, float multiplier = 1f)
        {
            var targetHealth = target.GetComponentInParent<HealthSystem>();
            if (targetHealth == null || targetHealth == Health) return false;
            if (targetHealth.GetComponent<EnemyController>() != null) return false;

            if (HasAttackElement) DamageCalculator.Strike(Stats, targetHealth, DamageKind.Magic, AttackElement, multiplier);
            else DamageCalculator.Strike(Stats, targetHealth, DamageKind.Physical, Element.Neutral, multiplier);
            return true;
        }

        // 추적/감지 대상(플레이어). 없거나 사라졌으면 일정 간격으로만 다시 찾아서 매 프레임 탐색하지 않는다.
        protected Transform FindTarget()
        {
            if (target == null && Time.time >= nextTargetSearchTime)
            {
                var player = FindFirstObjectByType<PlayerMovement>();
                target = player != null ? player.transform : null;
                nextTargetSearchTime = Time.time + TargetSearchInterval;
            }

            // 플레이어가 죽었으면 더 이상 쫓지 않는다.
            if (target != null)
            {
                var targetHealth = target.GetComponent<HealthSystem>();
                if (targetHealth != null && targetHealth.IsDead) return null;
            }

            return target;
        }

        // 바로 앞에 디딜 땅이 없으면(낭떠러지나 3칸 넘게 꺼진 곳) true. 순찰/추적 중 스스로 떨어지지 않게 벽처럼 취급한다.
        // 공중에 떠 있을 때(도약 중)는 판단하지 않는다.
        protected bool LedgeAhead(float direction, LayerMask ground)
        {
            var bounds = BodyBounds;
            Vector2 feet = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
            if (!Physics2D.Raycast(feet, Vector2.down, 0.3f, ground)) return false;

            Vector2 ahead = new Vector2(bounds.center.x + direction * (bounds.extents.x + 0.25f), bounds.min.y + 0.1f);
            return !Physics2D.Raycast(ahead, Vector2.down, LedgeDropLimit, ground);
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
            if (ContactDamageActive) ApplyContactDamage();
        }

        // 플레이어와 몸체 충돌은 꺼져 있어(밀어내지 않기 위해) 충돌 콜백 대신 겹침 조회로 접촉 데미지를 준다.
        private void ApplyContactDamage()
        {
            var bounds = BodyBounds;
            foreach (var hit in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f))
            {
                var targetHealth = hit.GetComponentInParent<HealthSystem>();
                if (targetHealth == null || targetHealth == Health) continue;
                if (targetHealth.GetComponent<EnemyController>() != null) continue; // 적끼리는 피해 없음

                if (lastHitTime.TryGetValue(targetHealth, out float last) && Time.time - last < contactDamageCooldown) continue;

                DamageCalculator.Strike(Stats, targetHealth, DamageKind.Physical, Element.Neutral, contactDamageMultiplier);
                lastHitTime[targetHealth] = Time.time;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current > 0f || IsDead) return;

            IsDead = true;
            if (Animator != null) Animator.SetBool(IsDeadParam, true);
            OnDied();
            Killed?.Invoke(this, Stats != null ? Stats.Stats.experience : 0f);
            Destroy(gameObject, destroyDelayAfterDeath);
        }

        protected virtual void OnDestroy()
        {
            if (Health != null) Health.HealthChanged -= OnHealthChanged;
        }
    }
}
