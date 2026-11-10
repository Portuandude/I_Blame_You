using System;
using System.Collections.Generic;
using IBlameYou.Enemies;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 이번 런에서 얻은 유물 목록과 효과 적용. 유물은 A/B 공용이고, 효과는 얻는 순간 한 번 적용된다 (RelicBook 참고).
    public class PlayerRelics : MonoBehaviour
    {
        private readonly List<RelicId> owned = new List<RelicId>();

        public IReadOnlyList<RelicId> Owned => owned;
        public event Action<RelicId> Added;

        public bool Has(RelicId id) => owned.Contains(id);

        private bool listeningKills;

        private bool listeningShield;

        private bool listeningSpells;

        private void OnDestroy()
        {
            if (listeningKills) EnemyController.Killed -= OnEnemyKilled;
            var shieldSkill = listeningShield ? GetComponent<ElementShieldSkill>() : null;
            if (shieldSkill != null) shieldSkill.Activated -= OnShieldActivated;
            var caster = listeningSpells ? GetComponent<SpellCaster>() : null;
            if (caster != null) caster.Casted -= OnSpellCasted;
        }

        // 응원 리본: B의 공격 마법이 발동하면 마나를 조금 돌려받는다 (쌍둥이 반지의 B 쪽 짝, 주황 글자).
        private void OnSpellCasted(Element element)
        {
            var mana = GetComponent<ManaSystem>();
            if (mana == null) return;
            mana.Restore(RelicBook.CheerRibbonMana);
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.6f, $"마나 +{RelicBook.CheerRibbonMana:0}", new Color(1f, 0.65f, 0.35f), 0.8f);
        }

        // 쌍둥이 반지: A의 방어막이 발동하면 마나를 조금 돌려받는다 (B가 바로 마법을 이어 쓸 수 있게).
        private void OnShieldActivated(Element element)
        {
            var mana = GetComponent<ManaSystem>();
            if (mana == null) return;
            mana.Restore(RelicBook.TwinRingMana);
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.6f, $"마나 +{RelicBook.TwinRingMana:0}", new Color(0.37f, 0.82f, 1f), 0.8f);
        }

        // 흡혈 송곳니: 적을 쓰러뜨리면 체력 회복 (쓰러진 뒤에는 회복하지 않는다).
        // 별똥 조각: 적을 쓰러뜨리면 가끔 금별이 적 자리에서 날아와 닿을 때 마나 회복 (노란 글자).
        private void OnEnemyKilled(EnemyController enemy, float xp)
        {
            var health = GetComponent<HealthSystem>();
            if (health == null || health.IsDead) return;
            if (Has(RelicId.VampireFang)) health.Heal(RelicBook.VampireFangHeal);
            if (Has(RelicId.StarShard) && RelicBook.StarShardProcs(UnityEngine.Random.value))
            {
                Vector3 from = enemy != null ? enemy.transform.position + Vector3.up * 0.5f : transform.position;
                VfxPlayer.StarShard(from, transform, Vector3.up * 0.8f, OnStarShardArrived);
            }
        }

        private void OnStarShardArrived()
        {
            if (this == null) return; // 별이 날아오는 사이 플레이어가 사라졌다
            var health = GetComponent<HealthSystem>();
            if (health == null || health.IsDead) return;
            var mana = GetComponent<ManaSystem>();
            if (mana == null) return;
            mana.Restore(RelicBook.StarShardMana);
            GameAudio.PlaySfx("StarShard", 0.6f, 0.08f); // 작고 맑은 띵 (Tools/AudioGen star_shard)
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.6f, $"마나 +{RelicBook.StarShardMana:0}", new Color(1f, 0.9f, 0.35f), 0.8f);
        }

        // 피의 계약: 모자란 마나를 체력으로 대신 낸다. 재사용 대기 중이거나 내면 체력이 1 밑으로 내려가면 거절.
        // 체력은 Drain으로 깎는다 (경직·피해 숫자 없이, 버티기도 쓰지 않음).
        private float bloodPactReadyAt;

        private bool PayWithHealth(ManaUser user, float shortfall)
        {
            var health = GetComponent<HealthSystem>();
            if (health == null || health.IsDead || Time.time < bloodPactReadyAt) return false;
            if (!RelicBook.BloodPactCost(health.Current, shortfall, out float cost)) return false;

            health.Drain(cost);
            bloodPactReadyAt = Time.time + RelicBook.BloodPactCooldown;
            // 연출: berry 글자 + 작은 별·하트 펑 (체력이 마나로 바뀐 느낌)
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.6f, $"체력 {cost:0} → 마나", new Color(1f, 0.44f, 0.68f), 0.9f);
            VfxPlayer.DeathPop(transform.position + Vector3.up * 0.8f, 0.6f);
            SkillMessages.Post(user, $"피의 계약: 체력 {cost:0}으로 모자란 마나를 냈다 ({RelicBook.BloodPactCooldown:0}초 뒤 다시)");
            return true;
        }

        // 플레이어 프리팹이 이 컴포넌트가 생기기 전에 구워졌어도 동작하도록, 없으면 붙여서 돌려준다.
        public static PlayerRelics For(GameObject player)
        {
            if (player == null) return null;
            var relics = player.GetComponent<PlayerRelics>();
            return relics != null ? relics : player.AddComponent<PlayerRelics>();
        }

        public bool Add(RelicId id)
        {
            if (Has(id)) return false;
            owned.Add(id);
            Apply(id);
            Added?.Invoke(id);
            return true;
        }

        private void Apply(RelicId id)
        {
            var stats = GetComponent<CombatStats>();
            var mana = GetComponent<ManaSystem>();
            var health = GetComponent<HealthSystem>();

            switch (id)
            {
                case RelicId.ManaCrystal:
                    if (mana != null) mana.AddMax(RelicBook.ManaCrystalMana);
                    break;
                case RelicId.IronCloak:
                    if (stats != null) stats.Stats.defense += RelicBook.IronCloakDefense;
                    break;
                case RelicId.SwiftBoots:
                    if (stats != null) stats.MoveSpeedMultiplier += RelicBook.SwiftBootsSpeed;
                    break;
                case RelicId.WrathCharm:
                    if (stats != null) stats.DamageMultiplier += RelicBook.WrathCharmDamage;
                    break;
                case RelicId.LifeSeed:
                    if (health != null) health.AddMax(RelicBook.LifeSeedHealth);
                    break;
                case RelicId.SpringRing:
                    if (mana != null) mana.RegenMultiplier += RelicBook.SpringRingRegen;
                    break;
                case RelicId.HawkEye:
                    if (stats != null) stats.Stats.critChance = Mathf.Min(1f, stats.Stats.critChance + RelicBook.HawkEyeCrit);
                    break;
                case RelicId.Featherfall:
                    var fall = GetComponent<FallRecovery>();
                    if (fall != null) fall.DamageRatio = Mathf.Min(fall.DamageRatio, RelicBook.FeatherfallDamageRatio);
                    break;
                case RelicId.VampireFang:
                case RelicId.StarShard:
                    if (!listeningKills)
                    {
                        EnemyController.Killed += OnEnemyKilled;
                        listeningKills = true;
                    }
                    break;
                case RelicId.FocusBead:
                    if (mana != null) mana.WastedRefundRatio = Mathf.Min(1f, mana.WastedRefundRatio + RelicBook.FocusBeadRefund);
                    break;
                case RelicId.ShadowVeil:
                    var movement = GetComponent<PlayerMovement>();
                    if (movement != null) movement.PostDashGrace = Mathf.Max(movement.PostDashGrace, RelicBook.ShadowVeilGrace);
                    if (GetComponent<GraceBlink>() == null) gameObject.AddComponent<GraceBlink>(); // 무적 동안 보라 깜빡임 + 잔상
                    break;
                case RelicId.BloodPact:
                    if (mana != null) mana.ShortfallCover = PayWithHealth;
                    break;
                case RelicId.PhoenixFeather:
                    if (health != null) health.DeathSaves += RelicBook.PhoenixFeatherSaves;
                    break;
                case RelicId.Hourglass:
                    var recovery = GetComponent<FallRecovery>();
                    if (recovery != null) recovery.FreeFalls += RelicBook.HourglassFreeFalls;
                    break;
                case RelicId.TwinRing:
                    var shieldSkill = GetComponent<ElementShieldSkill>();
                    if (shieldSkill != null && !listeningShield)
                    {
                        shieldSkill.Activated += OnShieldActivated;
                        listeningShield = true;
                    }
                    break;
                case RelicId.CheerRibbon:
                    var caster = GetComponent<SpellCaster>();
                    if (caster != null && !listeningSpells)
                    {
                        caster.Casted += OnSpellCasted;
                        listeningSpells = true;
                    }
                    break;
            }
        }
    }
}
