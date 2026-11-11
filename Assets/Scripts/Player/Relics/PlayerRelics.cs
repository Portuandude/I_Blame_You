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

        private bool listeningLevels;

        private void OnDestroy()
        {
            var progression = listeningLevels ? GetComponent<PlayerProgression>() : null;
            if (progression != null) progression.LeveledUp -= OnLeveledUp;
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
            var color = new Color(1f, 0.65f, 0.35f);
            VfxPlayer.ManaRing(transform, RingOffset, color); // B색 고리
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.6f, $"마나 +{RelicBook.CheerRibbonMana:0}", color, 0.8f);
        }

        private int acornFrame = -1;
        private int acornStack;

        private static readonly Vector3 AcornGlintOffset = new Vector3(0f, 1.5f, 0f); // 도토리 주머니 시작 높이 (머리 위, 훈장 메달과 같게)

        private static readonly Vector3 RingOffset = new Vector3(0f, 0.7f, 0f); // 마나 고리 가운데 (몸통 높이, 플레이어 로컬)

        // 쌍둥이 반지: A의 방어막이 발동하면 마나를 조금 돌려받는다 (B가 바로 마법을 이어 쓸 수 있게).
        private void OnShieldActivated(Element element)
        {
            var mana = GetComponent<ManaSystem>();
            if (mana == null) return;
            mana.Restore(RelicBook.TwinRingMana);
            var color = new Color(0.37f, 0.82f, 1f);
            VfxPlayer.ManaRing(transform, RingOffset, color); // A색 고리
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.6f, $"마나 +{RelicBook.TwinRingMana:0}", color, 0.8f);
        }

        // 도토리 주머니: 레벨이 오를 때마다 체력 회복 (한 번에 여러 레벨이 오르면 레벨마다, 쓰러진 뒤에는 회복하지 않는다).
        private void OnLeveledUp(int level)
        {
            var health = GetComponent<HealthSystem>();
            if (health == null || health.IsDead) return;
            float before = health.Current;
            health.Heal(RelicBook.AcornPouchHeal);
            // 한 번에 여러 레벨이 오르면 도토리 연출은 한 번만, 글자는 레벨마다 조금씩 위로 쌓는다 (겹치지 않게, 리뷰 LOW)
            acornStack = acornFrame == Time.frameCount ? acornStack + 1 : 0;
            acornFrame = Time.frameCount;
            if (acornStack == 0) VfxPlayer.AcornGlint(transform, AcornGlintOffset); // 레벨업 빛기둥 위로 도토리 주머니가 톡
            DamageNumbers.ShowHeal(transform.position, health.Current - before, "체력 "); // 실제 찬 만큼, 같은 프레임이면 위로 쌓인다
        }

        // 흡혈 송곳니: 적을 쓰러뜨리면 붉은 하트가 날아와 닿을 때 체력 회복 (쓰러진 뒤에는 회복하지 않는다).
        // 별똥 조각: 적을 쓰러뜨리면 가끔 금별이 적 자리에서 날아와 닿을 때 마나 회복 (노란 글자).
        private void OnEnemyKilled(EnemyController enemy, float xp)
        {
            var health = GetComponent<HealthSystem>();
            if (health == null || health.IsDead) return;
            if (Has(RelicId.VampireFang))
            {
                // 적 자리에서 붉은 하트가 날아와 닿을 때 회복 (최대 1.5초 안에는 반드시 닿는다, StarShardFx)
                Vector3 from = enemy != null ? enemy.transform.position + Vector3.up * 0.5f : transform.position;
                VfxPlayer.VampireHeart(from, transform, Vector3.up * 0.8f, OnVampireHeartArrived);
            }
            if (Has(RelicId.StarShard) && RelicBook.StarShardProcs(UnityEngine.Random.value))
            {
                Vector3 from = enemy != null ? enemy.transform.position + Vector3.up * 0.5f : transform.position;
                if (Has(RelicId.VampireFang)) from += Vector3.right * 0.25f; // 흡혈 하트와 같이 나오면 별은 살짝 옆에서 (겹치지 않게)
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

        private void OnVampireHeartArrived()
        {
            if (this == null) return; // 하트가 날아오는 사이 플레이어가 사라졌다
            var health = GetComponent<HealthSystem>();
            if (health == null || health.IsDead) return;
            float before = health.Current;
            health.Heal(RelicBook.VampireFangHeal);
            GameAudio.PlaySfx("VampireHeart", 0.6f, 0.08f); // 작고 말랑한 뽁 (Tools/AudioGen vampire_heart)
            DamageNumbers.ShowHeal(transform.position, health.Current - before);
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
                case RelicId.AcornPouch:
                    var progression = GetComponent<PlayerProgression>();
                    if (progression != null && !listeningLevels)
                    {
                        progression.LeveledUp += OnLeveledUp;
                        listeningLevels = true;
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
