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

            if (!used && health.Current <= health.Max * triggerHealthRatio && mana >= manaCost)
            {
                Begin();
            }

            if (!Active) return;

            remaining -= Time.deltaTime;
            health.Drain(healthDrainPerSecond * Time.deltaTime);
            if (visual != null) visual.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));

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
        }

        private void End()
        {
            remaining = 0f;
            stats.MoveSpeedMultiplier = 1f;
            stats.AttackSpeedMultiplier = 1f;
            if (visual != null) visual.color = Color.white;
        }
    }
}
