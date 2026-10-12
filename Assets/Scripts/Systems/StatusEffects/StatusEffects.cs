using UnityEngine;

namespace IBlameYou.Systems
{
    public enum StatusDebuff
    {
        None,
        Burn,  // 불: 중첩 가능한 지속 피해
        Wet,   // 물: 이동 둔화
        Stun   // 대지: 경직
    }

    // 적에게 걸리는 디버프 (화상/젖음/경직). 처음 디버프가 걸릴 때 대상에 자동으로 붙는다.
    // - 확률: 7.5 / 디버프 저항도 (최대 100%). 평균 디버프 저항도 25에서 30%가 되도록 맞춘 값.
    //   디버프 저항도 7.5 이하는 100%, 15는 50%, 30은 25%.
    // - 화상: 맞을 때마다 한 중첩씩 쌓인다. 1초마다 중첩 수 × (공격자 불 속성 데미지 / 10)의 고정 피해를 5초 동안.
    //   새 중첩이 쌓이면 늘어난 중첩 수로 5초를 처음부터 다시 센다.
    // - 젖음: 3초 동안 이동속도 40% 감소. 다시 맞으면 시간만 갱신.
    // - 화상과 젖음은 함께 걸리지 않는다. 새로 걸린 쪽이 다른 쪽을 지운다.
    // - 경직: 1초 동안 이동/공격 정지.
    public class StatusEffects : MonoBehaviour
    {
        public const float ChanceAtReference = 0.3f;   // 기준 디버프 저항도에서의 확률
        public const float ReferenceResistance = 25f;  // 기준(평균) 디버프 저항도
        public const float BurnDuration = 5f;
        public const float BurnRatio = 0.1f;
        public const float WetDuration = 3f;
        public const float WetSlow = 0.4f;
        public const float StunDuration = 1f;

        private static readonly Color BurnColor = new Color(1f, 0.45f, 0.15f);
        private static readonly Color WetColor = new Color(0.35f, 0.65f, 1f);
        private static readonly Color StunColor = new Color(0.8f, 0.65f, 0.4f);

        private int burnStacks;
        private float burnDamagePerStack;
        private float burnRemaining;
        private HealthSystem health;
        private CombatStats stats;
        private HitStun hitStun;
        private float wetRemaining;
        private float burnTickTimer;

        public int BurnStacks => burnStacks;
        public bool IsWet => wetRemaining > 0f;

        // 디버프가 걸릴 확률 (0~1).
        public static float Chance(CombatStats target)
        {
            float resistance = target != null ? target.Stats.debuffResistance : 0f;
            if (resistance <= 0f) return 1f;
            return Mathf.Clamp01(ChanceAtReference * ReferenceResistance / resistance);
        }

        // 확률 판정 후 디버프를 건다. 걸렸으면 true.
        public static bool TryApply(Component target, StatusDebuff debuff, CombatStats attacker)
        {
            if (debuff == StatusDebuff.None || target == null) return false;

            var health = target.GetComponentInParent<HealthSystem>();
            if (health == null || health.IsDead) return false;
            if (Random.value >= Chance(health.GetComponent<CombatStats>())) return false;

            var effects = health.GetComponent<StatusEffects>();
            if (effects == null) effects = health.gameObject.AddComponent<StatusEffects>();
            effects.Apply(debuff, attacker);
            return true;
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            stats = GetComponent<CombatStats>();
            hitStun = GetComponent<HitStun>();
        }

        private void Apply(StatusDebuff debuff, CombatStats attacker)
        {
            switch (debuff)
            {
                case StatusDebuff.Burn:
                    ClearWet();
                    float fire = attacker != null ? attacker.Stats.ElementDamage(Element.Fire) : 0f;
                    burnStacks++;
                    burnDamagePerStack = fire * BurnRatio;
                    burnRemaining = BurnDuration;
                    burnTickTimer = 1f;
                    Announce($"화상 x{burnStacks}", BurnColor);
                    break;

                case StatusDebuff.Wet:
                    ClearBurn();
                    wetRemaining = WetDuration;
                    if (stats != null) stats.SlowMultiplier = 1f - WetSlow;
                    Announce("젖음", WetColor);
                    break;

                case StatusDebuff.Stun:
                    if (hitStun != null) hitStun.Apply(StunDuration);
                    Announce("경직", StunColor);
                    break;
            }
        }

        private void Update()
        {
            if (health == null || health.IsDead)
            {
                ClearBurn();
                ClearWet();
                return;
            }

            if (wetRemaining > 0f)
            {
                wetRemaining -= Time.deltaTime;
                if (wetRemaining <= 0f) ClearWet();
            }

            if (burnStacks == 0) return;

            // 1초마다 중첩 수만큼의 피해를 한 번에 준다. 방어력/속성 방어력을 무시하는 고정 피해.
            burnTickTimer -= Time.deltaTime;
            if (burnTickTimer > 0f) return;
            burnTickTimer += 1f;

            float total = burnStacks * burnDamagePerStack;
            burnRemaining -= 1f;
            if (burnRemaining <= 0.001f) ClearBurn();

            if (total <= 0f) return;
            health.Drain(total);
            DamageNumbers.ShowText(transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 1.5f, 0f),
                total.ToString("0.#"), BurnColor, 0.8f);
        }

        private void ClearBurn()
        {
            burnStacks = 0;
            burnRemaining = 0f;
        }

        private void ClearWet()
        {
            wetRemaining = 0f;
            if (stats != null) stats.SlowMultiplier = 1f;
        }

        private void Announce(string text, Color color)
        {
            DamageNumbers.ShowText(transform.position + new Vector3(0f, 2f, 0f), text, color, 0.8f);
        }
    }
}
