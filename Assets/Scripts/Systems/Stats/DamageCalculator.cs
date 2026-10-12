using UnityEngine;

namespace IBlameYou.Systems
{
    public enum DamageKind
    {
        Physical,
        Magic
    }

    // 피격자가 이 인터페이스를 가진 컴포넌트를 갖고 있으면 최종 피해 직전에 값을 바꿀 수 있다 (예: 속성 방어막).
    public interface IDamageModifier
    {
        float Modify(float damage, DamageKind kind, Element element);
    }

    // 기획서 5.2에서 확정한 비율식 데미지 공식.
    //   물리 피해 = 물리 데미지 × 100 / (100 + 방어력)
    //   마법 피해 = 속성 데미지 × 100 / (100 + 속성 방어력)
    // 디버프 저항도는 디버프 확률에만 쓰고 피해 계산에는 쓰지 않는다 (StatusEffects.Chance).
    // 치명타는 방어 계산 후 최종 피해에 배율을 곱한다 (기획서 5.3 추천안).
    public static class DamageCalculator
    {
        public static float Physical(float attack, float defense)
        {
            return attack * 100f / (100f + Mathf.Max(defense, -50f));
        }

        public static float Magic(float elementDamage, float elementDefense)
        {
            return elementDamage * 100f / (100f + Mathf.Max(elementDefense, -50f));
        }

        // 공격자 스탯으로 대상에게 한 번 피해를 준다. multiplier는 스킬 계수.
        // 반환값: 실제로 계산된 피해량(무적 등으로 무시돼도 계산값은 반환). 대상이 피해를 받을 수 없으면 0.
        public static float Strike(CombatStats attacker, Component target, DamageKind kind, Element element = Element.Neutral,
            float multiplier = 1f)
        {
            if (target == null) return 0f;

            var health = target.GetComponentInParent<HealthSystem>();
            if (health == null || health.IsDead) return 0f;

            var defenderStats = health.GetComponent<CombatStats>();
            var attack = attacker != null ? attacker.Stats : null;

            float raw = kind == DamageKind.Physical
                ? (attack != null ? attack.physicalDamage : 0f)
                : (attack != null ? attack.ElementDamage(element) : 0f);
            raw *= multiplier;

            float damage;
            if (defenderStats == null)
            {
                damage = raw;
            }
            else if (kind == DamageKind.Physical)
            {
                damage = Physical(raw, defenderStats.Stats.defense);
            }
            else
            {
                damage = Magic(raw, defenderStats.Stats.ElementDefense(element));
            }

            bool critical = attack != null && attack.critChance > 0f && Random.value < attack.critChance;
            if (critical) damage *= attack.critDamage;

            foreach (var modifier in health.GetComponents<IDamageModifier>())
            {
                damage = modifier.Modify(damage, kind, element);
            }

            if (damage <= 0f) return 0f;

            bool applied = !health.Invulnerable;
            health.TakeDamage(damage);
            if (applied) DamageNumbers.Show(health.transform.position, damage, critical, kind, element);
            return damage;
        }
    }
}
