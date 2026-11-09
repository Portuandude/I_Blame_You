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

        private void OnDestroy()
        {
            if (listeningKills) EnemyController.Killed -= OnEnemyKilled;
        }

        // 흡혈 송곳니: 적을 쓰러뜨리면 체력 회복 (쓰러진 뒤에는 회복하지 않는다).
        private void OnEnemyKilled(EnemyController enemy, float xp)
        {
            var health = GetComponent<HealthSystem>();
            if (health != null && !health.IsDead) health.Heal(RelicBook.VampireFangHeal);
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
                    if (!listeningKills)
                    {
                        EnemyController.Killed += OnEnemyKilled;
                        listeningKills = true;
                    }
                    break;
                case RelicId.FocusBead:
                    if (mana != null) mana.WastedRefundRatio = Mathf.Min(1f, mana.WastedRefundRatio + RelicBook.FocusBeadRefund);
                    break;
            }
        }
    }
}
