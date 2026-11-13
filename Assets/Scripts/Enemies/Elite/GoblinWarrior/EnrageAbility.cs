using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 고블린 대전사 패턴 (스탯 문서): 마나 50을 소모하여 이동속도 및 공격속도 30% 상승, 체력 초당 3씩 감소.
    // 발동 조건과 지속시간은 문서에 없어 가안: 체력 50% 이하에서 한 번, 10초 동안.
    [RequireComponent(typeof(CombatStats))]
    [RequireComponent(typeof(HealthSystem))]
    public class EnrageAbility : MonoBehaviour
    {
        [SerializeField] private float triggerHealthRatio = 0.5f;
        [SerializeField] private float manaCost = 50f;
        [SerializeField] private float speedBonus = 0.3f;
        [SerializeField] private float healthDrainPerSecond = 3f;
        [SerializeField] private float duration = 10f;

        private CombatStats stats;
        private HealthSystem health;
        private SpriteRenderer visual;
        private float mana;
        private float remaining;
        private bool used;

        public bool Active => remaining > 0f;

        // 광폭화가 풀리기까지 남은 초 (꺼져 있으면 0).
        public float Remaining => Mathf.Max(0f, remaining);

        // false면 체력 조건으로 스스로 켜지지 않고 Trigger로만 켜진다 (쌍둥이 슬라임 기사: 짝이 쓰러지면 광폭화).
        public bool AutoTrigger { get; set; } = true;

        // false면 몸 색을 건드리지 않는다 (색을 따로 관리하는 몹).
        public bool TintsBody { get; set; } = true;

        // false면 처음 켜질 때 EnrageFx(김·잔상·헉헉 연출)를 붙이지 않는다 (연출을 따로 하는 몹: 쌍둥이 슬라임 기사).
        public bool OwnFx { get; set; } = true;

        // 풀리기 이만큼 전부터 붉은 깜빡임이 빨라진다 (곧 풀린다는 예고).
        public const float EndingWarnSeconds = 3f;

        // 바로 광폭화한다 (마나와 상관없이, 한 번만).
        public void Trigger()
        {
            if (used || health == null || health.IsDead) return;
            mana = Mathf.Max(mana, manaCost);
            Begin();
        }

        // 광폭화 시간·체력 감소를 바꾼다 (스포너가 붙인 직후에).
        public void Configure(float bonus, float drainPerSecond, float seconds)
        {
            speedBonus = bonus;
            healthDrainPerSecond = drainPerSecond;
            duration = seconds;
        }

        private void Awake()
        {
            stats = GetComponent<CombatStats>();
            health = GetComponent<HealthSystem>();
            visual = GetComponentInChildren<SpriteRenderer>();
            mana = stats.Stats.maxMana;
        }

        private void Update()
        {
            if (health.IsDead) return;

            if (AutoTrigger && !used && health.Current <= health.Max * triggerHealthRatio && mana >= manaCost)
            {
                Begin();
            }

            if (!Active) return;

            remaining -= Time.deltaTime;
            health.Drain(healthDrainPerSecond * Time.deltaTime);
            if (TintsBody && visual != null)
            {
                float speed = remaining <= EndingWarnSeconds ? 22f : 10f;
                visual.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * speed));
            }

            if (remaining <= 0f) End();
        }

        private void Begin()
        {
            used = true;
            mana -= manaCost;
            remaining = duration;
            stats.MoveSpeedMultiplier = 1f + speedBonus;
            stats.AttackSpeedMultiplier = 1f + speedBonus;
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.8f, "광폭화!", new Color(1f, 0.3f, 0.2f), 1.3f);
            GameAudio.PlaySfx("Enrage", 0.9f); // 낮게 으르렁 올라가는 소리 (Tools/AudioGen)
            CameraFollow.ShakeMain(0.25f, 0.12f);

            if (OwnFx)
            {
                var fx = GetComponent<EnrageFx>();
                if (fx == null) fx = gameObject.AddComponent<EnrageFx>();
                fx.OnBegin();
            }
        }

        private void End()
        {
            remaining = 0f;
            stats.MoveSpeedMultiplier = 1f;
            stats.AttackSpeedMultiplier = 1f;
            if (TintsBody && visual != null) visual.color = Color.white;
        }
    }
}
