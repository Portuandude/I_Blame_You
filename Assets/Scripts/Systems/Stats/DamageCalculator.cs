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
    // 마법 피해는 대상에게 몸 속성이 있으면 속성 상성(ElementAffinity) 배율을 곱한다.
    public static class DamageCalculator
    {
        public static float Physical(float attack, float defense)
        {
            return DamageFormula.Ratio(attack, defense);
        }

        public static float Magic(float elementDamage, float elementDefense)
        {
            return DamageFormula.Ratio(elementDamage, elementDefense);
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
            if (attacker != null) raw *= attacker.DamageMultiplier;
            if (attacker != null && kind == DamageKind.Magic) raw *= Mathf.Max(0.1f, attacker.MagicDamageMultiplier);

            float damage;
            float affinity = 1f;
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
                if (defenderStats.HasBodyElement) affinity = ElementAffinity.Multiplier(element, defenderStats.BodyElement);
                damage *= affinity;
            }

            bool critical = attack != null && attack.critChance > 0f && Random.value < attack.critChance;
            if (critical) damage *= attack.critDamage;

            foreach (var modifier in health.GetComponents<IDamageModifier>())
            {
                damage = modifier.Modify(damage, kind, element);
            }

            if (damage <= 0f) return 0f;

            bool applied = !health.IsInvulnerable;
            health.TakeDamage(damage);
            if (applied)
            {
                DamageNumbers.Show(health.transform.position, damage, critical, kind, element);
                if (affinity > 1f) DamageNumbers.ShowText(health.transform.position + new Vector3(0f, 1.9f, 0f), "약점!", new Color(1f, 0.85f, 0.3f), 0.75f);
                else if (affinity < 1f) DamageNumbers.ShowText(health.transform.position + new Vector3(0f, 1.9f, 0f), "저항", new Color(0.7f, 0.7f, 0.75f), 0.7f);
                PlayHitFeel(attacker, health, critical);
            }
            return damage;
        }

        // 치명타 효과음 Crit (높고 짧은 "팅"): 적중음(Hit_)에 겹친다. 광역 마법이 여러 마리에 한꺼번에 치명타를 내도 한 번만 울리게 쿨다운.
        private const float CritSfxCooldown = 0.08f;
        private static float lastCritSfx = -1f;

        private static void PlayCritSfx()
        {
            float now = Time.unscaledTime;
            if (now >= lastCritSfx && now - lastCritSfx < CritSfxCooldown) return;
            lastCritSfx = now;
            GameAudio.PlaySfx("Crit", 0.75f, 0.05f);
        }

        // 전투 손맛 (2026-11-12): 플레이어 공격이 적에게 들어갔을 때만. 일반 적은 넉백. 쓰러뜨렸으면 히트스톱(정예는 더 길게), 아니면 치명타일 때 짧게.
        // 보스를 마지막으로 쓰러뜨린 순간은 BossDefeatFx 슬로모션이 먼저 걸려 HitStop은 timeScale이 1이 아니라 저절로 빠진다.
        private static void PlayHitFeel(CombatStats attacker, HealthSystem health, bool critical)
        {
            // 플레이어가 맞았을 때도 아주 짧게 멈춰 "맞았다"를 몸으로 느끼게 (함정 포함). 쓰러지는 일격은 사망 연출 슬로모션이 먼저 걸려 저절로 빠진다
            if (!health.IsDead && health.GetComponent<IBlameYou.Player.PlayerCombat>() != null)
            {
                HitStop.Play(HitStop.HurtDuration);
                return;
            }
            if (attacker == null || attacker.GetComponent<IBlameYou.Player.PlayerCombat>() == null) return;
            var enemy = health.GetComponent<IBlameYou.Enemies.EnemyController>();
            if (enemy == null) return;

            // 넉백: 일반 적만, 공격자(플레이어 몸) 반대쪽으로. 바로 위·아래에서 맞으면 밀지 않는다
            float dx = health.transform.position.x - attacker.transform.position.x;
            var rank = IBlameYou.Core.RunGold.RankOf(enemy);
            if (rank == EnemyRank.Normal && Mathf.Abs(dx) > 0.05f) enemy.Knockback(dx);

            if (critical) PlayCritSfx();

            if (health.IsDead)
                HitStop.Play(rank == EnemyRank.Elite ? HitStop.EliteDuration : HitStop.KillDuration);
            else if (critical)
                HitStop.Play(HitStop.CritDuration);
        }
    }
}
