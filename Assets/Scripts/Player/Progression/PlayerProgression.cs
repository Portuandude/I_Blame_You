using System;
using IBlameYou.Enemies;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 경험치/레벨/특성 포인트. 적을 쓰러뜨리면 경험치가 쌓이고, 레벨이 오르면 특성 포인트 1을 받는다 (TraitTree 참고).
    // 다음 레벨까지 경험치 = 60 + 25 × (현재 레벨 - 1). 스테이지 1의 적을 다 잡으면 평균 2600 정도라 보스 전에 13~14레벨.
    [RequireComponent(typeof(HealthSystem))]
    public class PlayerProgression : MonoBehaviour
    {
        private const float WrathDamagePerPoint = 0.03f;
        private const float SwiftnessSpeedPerPoint = 0.02f;
        private const float PatienceDefensePerPoint = 2f;
        private const float MysticManaPerPoint = 5f;
        private const float VitalityHealthPerPoint = 5f;

        private readonly int[] ranks = new int[TraitTree.All.Length];

        private HealthSystem health;
        private ManaSystem mana;
        private CombatStats stats;
        private PlayerMovement movement;
        private TeleportSkill teleport;
        private SpellCaster caster;
        private HitStun hitStun;

        public int Level { get; private set; } = 1;
        public float Experience { get; private set; }   // 이번 레벨에서 모은 경험치
        public float TotalExperience { get; private set; }
        public int Points { get; private set; }
        public float XpToNext => XpForLevel(Level);

        // 특성 효과 중 다른 시스템이 읽어 가는 값.
        public float CheckpointHealRatio { get; private set; } = 0.5f;

        public event Action<int> LeveledUp;            // 새 레벨
        public event Action<Trait, int> TraitRaised;   // 특성, 새 단계

        public static float XpForLevel(int level) => 60f + 25f * (level - 1);

        public int Rank(Trait trait) => ranks[(int)trait];

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            mana = GetComponent<ManaSystem>();
            stats = GetComponent<CombatStats>();
            movement = GetComponent<PlayerMovement>();
            teleport = GetComponent<TeleportSkill>();
            caster = GetComponent<SpellCaster>();
            hitStun = GetComponent<HitStun>();
            EnemyController.Killed += OnEnemyKilled;
            health.DeathPrevented += OnDeathPrevented;
        }

        private void OnDestroy()
        {
            EnemyController.Killed -= OnEnemyKilled;
            if (health != null) health.DeathPrevented -= OnDeathPrevented;
        }

        private void OnEnemyKilled(EnemyController enemy, float xp)
        {
            if (health.IsDead) return;
            AddExperience(xp);

            if (Rank(Trait.Wrath) >= 20 && mana != null) mana.Restore(5f);
            if (Rank(Trait.Vitality) >= 10) health.Heal(2f);
        }

        public void AddExperience(float amount)
        {
            if (amount <= 0f) return;
            Experience += amount;
            TotalExperience += amount;
            while (Experience >= XpToNext)
            {
                Experience -= XpToNext;
                Level++;
                Points++;
                LeveledUp?.Invoke(Level);
            }
        }

        public bool CanRaise(Trait trait) => Points > 0 && Rank(trait) < TraitTree.MaxRank;

        // 포인트 하나를 찍는다. 포인트당 효과는 매번, 5/10/20 효과는 그 단계에 닿을 때 한 번 적용한다.
        public bool Raise(Trait trait)
        {
            if (!CanRaise(trait)) return false;

            Points--;
            int rank = ++ranks[(int)trait];
            ApplyPerPoint(trait);
            if (Array.IndexOf(TraitTree.Milestones, rank) >= 0) ApplyMilestone(trait, rank);
            TraitRaised?.Invoke(trait, rank);
            return true;
        }

        private void ApplyPerPoint(Trait trait)
        {
            switch (trait)
            {
                case Trait.Wrath:
                    if (stats != null) stats.DamageMultiplier += WrathDamagePerPoint;
                    break;
                case Trait.Swiftness:
                    if (stats != null) stats.MoveSpeedMultiplier += SwiftnessSpeedPerPoint;
                    break;
                case Trait.Patience:
                    if (stats != null) stats.Stats.defense += PatienceDefensePerPoint;
                    break;
                case Trait.Mystic:
                    if (mana != null) mana.AddMax(MysticManaPerPoint);
                    break;
                case Trait.Vitality:
                    health.AddMax(VitalityHealthPerPoint);
                    break;
            }
        }

        private void ApplyMilestone(Trait trait, int rank)
        {
            switch (trait)
            {
                case Trait.Wrath:
                    if (rank == 5 && caster != null) caster.BoltCostReduction = 2f;
                    if (rank == 10 && stats != null) stats.Stats.critChance += 0.1f;
                    break; // 20: 처치 시 마나 (OnEnemyKilled)
                case Trait.Swiftness:
                    if (rank == 5 && movement != null) movement.DashStaminaCost = 35f;
                    if (rank == 10 && teleport != null) teleport.Distance = 6f;
                    if (rank == 20 && movement != null) movement.DashDuration *= 1.5f;
                    break;
                case Trait.Patience:
                    if (rank == 5 && hitStun != null) hitStun.Configure(0.5f, true);
                    if (rank == 10 && stats != null) stats.Stats.debuffResistance += 25f;
                    if (rank == 20) gameObject.AddComponent<LastStandGuard>();
                    break;
                case Trait.Mystic:
                    if (rank == 5 && mana != null) mana.RegenMultiplier += 0.3f;
                    if (rank == 10 && mana != null) mana.WastedRefundRatio = 0.5f;
                    if (rank == 20 && mana != null) mana.CostMultiplier *= 0.8f;
                    break;
                case Trait.Vitality:
                    if (rank == 5) CheckpointHealRatio = 0.75f;
                    if (rank == 20) health.DeathSaves++;
                    break; // 10: 처치 시 체력 (OnEnemyKilled)
            }
        }

        private void OnDeathPrevented()
        {
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.6f, "버텨냈다!", new Color(1f, 0.5f, 0.5f), 1.1f);
            SkillMessages.Post(ManaUser.Other, "생명 20: 쓰러질 피해를 버텨냈다", false);
        }
    }
}
