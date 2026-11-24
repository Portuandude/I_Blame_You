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
    // - 젖음: 3초 동안 이동속도 40% 감소. 다시 맞으면 시간을 갱신하고 둔화는 둘 중 센 쪽.
    //   얼음 족쇄는 정예·보스에게 20%만 (TryApply의 wetSlow, ShackleRules).
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
        private float burnDamagePerTick; // 쌓인 중첩마다 그 공격자의 불 데미지 × BurnRatio를 더한 값 (마지막 공격자 값으로 전체를 덮어쓰지 않게, 버그 점검 2026-11-24)
        private float burnRemaining;   // 화면 표시용 남은 시간 (매 프레임 감소)
        private int burnTicksLeft;     // 남은 피해 횟수
        private float stunRemaining;   // 화면 표시용 (실제 경직은 HitStun이 처리)
        private HealthSystem health;
        private CombatStats stats;
        private HitStun hitStun;
        private float wetRemaining;
        private float wetSlow;         // 지금 걸린 젖음 둔화 (0~1)
        private float burnTickTimer;

        public int BurnStacks => burnStacks;
        public bool IsWet => wetRemaining > 0f;

        // 머리 위 아이콘(StatusIconBar)이 쓰는 남은 시간 비율 (1 = 방금 걸림, 0 = 끝).
        public float BurnFraction => burnStacks > 0 ? Mathf.Clamp01(burnRemaining / BurnDuration) : 0f;
        public float WetFraction => Mathf.Clamp01(wetRemaining / WetDuration);
        public float StunFraction => Mathf.Clamp01(stunRemaining / StunDuration);

        // 디버프가 걸릴 확률 (0~1).
        public static float Chance(CombatStats target)
        {
            float resistance = target != null ? target.Stats.debuffResistance : 0f;
            if (resistance <= 0f) return 1f;
            return Mathf.Clamp01(ChanceAtReference * ReferenceResistance / resistance);
        }

        // 확률 판정 후 디버프를 건다. 걸렸으면 true.
        // wetSlow: 젖음일 때 이동속도를 얼마나 줄일지 (기본 40%, 얼음 족쇄가 정예·보스에게는 20%).
        public static bool TryApply(Component target, StatusDebuff debuff, CombatStats attacker, bool always = false, float wetSlow = WetSlow)
        {
            if (debuff == StatusDebuff.None || target == null) return false;

            var health = target.GetComponentInParent<HealthSystem>();
            if (health == null || health.IsDead) return false;
            if (!always && Random.value >= Chance(health.GetComponent<CombatStats>())) return false;

            var effects = health.GetComponent<StatusEffects>();
            if (effects == null) effects = health.gameObject.AddComponent<StatusEffects>();
            effects.Apply(debuff, attacker, wetSlow);
            return true;
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            stats = GetComponent<CombatStats>();
            hitStun = GetComponent<HitStun>();
            StatusIconBar.Attach(this);
        }

        private void Apply(StatusDebuff debuff, CombatStats attacker, float slow)
        {
            switch (debuff)
            {
                case StatusDebuff.Burn:
                    ClearWet();
                    float fire = attacker != null ? attacker.Stats.ElementDamage(Element.Fire) : 0f;
                    if (burnStacks == 0) burnTickTimer = 1f; // 쌓을 때마다 다시 1초로 맞추면 1초 안에 계속 맞는 동안 피해가 한 번도 안 들어간다
                    burnStacks++;
                    burnDamagePerTick += fire * BurnRatio;
                    burnRemaining = BurnDuration;
                    burnTicksLeft = Mathf.RoundToInt(BurnDuration);
                    Announce($"화상 x{burnStacks}", BurnColor);
                    break;

                case StatusDebuff.Wet:
                    ClearBurn();
                    wetSlow = wetRemaining > 0f ? Mathf.Max(wetSlow, slow) : slow; // 이미 젖었으면 센 둔화를 남긴다
                    wetRemaining = WetDuration;
                    if (stats != null) stats.SlowMultiplier = 1f - wetSlow;
                    Announce("젖음", WetColor);
                    break;

                case StatusDebuff.Stun:
                    if (hitStun == null || hitStun.Immune) break; // 경직이 안 걸리는 대상(슈퍼아머 보스 등)에 아이콘·"경직" 글자만 뜨던 것
                    hitStun.Apply(StunDuration);
                    stunRemaining = Mathf.Max(stunRemaining, StunDuration);
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
                stunRemaining = 0f;
                return;
            }

            if (stunRemaining > 0f) stunRemaining -= Time.deltaTime;

            if (wetRemaining > 0f)
            {
                wetRemaining -= Time.deltaTime;
                if (wetRemaining <= 0f) ClearWet();
            }

            if (burnStacks == 0) return;

            // 1초마다 중첩 수만큼의 피해를 한 번에 준다. 방어력/속성 방어력을 무시하는 고정 피해.
            burnRemaining -= Time.deltaTime;
            burnTickTimer -= Time.deltaTime;
            if (burnTickTimer > 0f) return;
            burnTickTimer += 1f;

            float total = burnDamagePerTick;
            burnTicksLeft--;
            if (burnTicksLeft <= 0) ClearBurn();

            if (total <= 0f || !health.TakeDotDamage(total)) return; // 무적 중이면 이번 피해는 없음, 쓰러질 피해면 버티기
            DamageNumbers.ShowText(transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 1.5f, 0f),
                total.ToString("0.#"), BurnColor, 0.8f);
        }

        private void ClearBurn()
        {
            burnStacks = 0;
            burnDamagePerTick = 0f;
            burnRemaining = 0f;
            burnTicksLeft = 0;
        }

        private void ClearWet()
        {
            wetRemaining = 0f;
            wetSlow = 0f;
            if (stats != null) stats.SlowMultiplier = 1f;
        }

        private void Announce(string text, Color color)
        {
            DamageNumbers.ShowText(transform.position + new Vector3(0f, 2f, 0f), text, color, 0.8f);
        }
    }
}
