using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 유물: 한 런 동안만 가지는 아이템. 정예 전투 구역을 돌파하면 나오는 유물 상자에서 3개 중 하나를 고른다.
    // 2026-11-24 사람 결정 (Docs/RelicList.md): 31종 → 26종. 5종 삭제(깃털 부적·되감기 모래시계·말굽 자석·단골 도장·돼지 저금통),
    // 2쌍 합침(탐험가의 나침반 → 보물 지도, 젤리 방석 → 솜구름 방석), 새 2종(메아리 수정·속성 공명석), 주인(A·B·공용) 표시 삭제.
    // 빠진 유물의 번호는 저장(RunSave)이 번호로 기억하므로 enum에 남기고 All(후보)에서만 뺀다. 효과도 없다.
    // 효과는 특성 4포인트 정도의 작은 능력치 보너스이고, 한 런에 같은 유물은 한 번만 나온다.
    // 이 파일은 UnityEngine 없는 별도 어셈블리(IBlameYou.Relics)라 EditMode 테스트로 후보 고르기를 검증한다.
    public enum RelicId
    {
        ManaCrystal, // 마력 수정: 최대 마나 +20
        IronCloak,   // 무쇠 망토: 방어력 +8
        SwiftBoots,  // 바람 장화: 이동속도 +8%
        WrathCharm,  // 분노의 부적: 주는 피해 +12%
        LifeSeed,    // 생명의 씨앗: 최대 체력 +20
        SpringRing,  // 샘물 반지: 마나 재생 +10% (마나 샘 방과 중첩)
        HawkEye,     // 매의 눈: 치명타 확률 +8%
        Featherfall, // (2026-11-24 삭제) 깃털 부적
        VampireFang, // 흡혈 송곳니: 적 처치 시 체력 +3
        FocusBead,   // 집중 구슬: 실패로 날린 마나 25% 돌려받기 (신비 10과 합산)
        ShadowVeil,  // 그림자 베일: 대쉬가 끝난 뒤 0.5초 더 무적
        BloodPact,   // 피의 계약: 마나가 모자라면 필요한 마나의 절반만큼 체력으로 대신 낸다 (횟수 제한 없음, 체력 1은 남긴다)
        PhoenixFeather, // 불사조 깃털: 쓰러질 피해를 한 번 버티고 최대 체력 30%로 (특성 버티기와 합산)
        Hourglass,   // (2026-11-24 삭제) 되감기 모래시계
        TwinRing,    // 쌍둥이 반지: B가 마법진을 PerfectAt(90%) 이상으로 그리면 마나 +5 (2026-11-16 방어막이 빠지며 바뀜. 예전: A 방어막 발동)
        Compass,     // (2026-11-24 보물 지도로 합침) 탐험가의 나침반. 예전 저장에 있으면 보물 지도처럼 친다 (PlayerRelics.Has)
        Clover,      // 네잎 클로버: 다음 유물 상자부터 후보가 OfferSize → CloverOfferSize개
        CheerRibbon, // 응원 리본: B의 공격 마법이 발동하면 마나 +CheerRibbonMana
        StarShard,   // 별똥 조각: 적을 처치하면 늘 마나 +StarShardMana (흡혈 송곳니의 마나 짝)
        HeroMedal,   // 용사의 훈장: 중간보스·보스를 처치하면 최대 체력의 HeroMedalHealRatio만큼 회복 (ChunkStreamer.OnArenaCleared)
        AcornPouch,  // 도토리 주머니: 레벨이 오를 때마다 최대 체력 +AcornPouchMaxHealth (PlayerProgression.LeveledUp)
        HoneyJar,    // 꿀단지: 보상 구슬을 얻으면 체력 회복량 +HoneyJarHealBonus (비율, ArenaReward)
        JellyCushion, // (2026-11-24 솜구름 방석으로 합침) 젤리 방석. 예전 저장에 있으면 솜구름 방석처럼 친다
        StarCandy,   // 별사탕: 레벨이 오를 때마다 최대 마나 +StarCandyMaxMana (PlayerProgression.LeveledUp, 도토리 주머니의 마나 짝)
        HorseshoeMagnet, // (2026-11-24 삭제) 말굽 자석
        PaperPlane,  // 종이비행기: 순간이동 마나 × PaperPlaneTeleportCost (A, TeleportSkill.CostMultiplier). 순간이동은 재사용 대기가 없어 백로그 원안(대기 -20%) 대신 마나
        LuckyPouch,  // 복주머니: 몬스터가 떨어뜨리는 골드 × (1 + LuckyPouchGoldBonus), 반올림 (공용, Core/RunGold.OnEnemyKilled)
        RegularStamp, // (2026-11-24 삭제) 단골 도장
        PiggyBank,   // (2026-11-24 삭제) 돼지 저금통
        TreasureMap, // 보물 지도 (2026-11-24 탐험가의 나침반과 합침): 미니맵에 안 가 본 방이 흐리게, 마을·보스 방은 아이콘까지 (UI/MinimapUI)
        CottonCloud, // 솜구름 방석 (2026-11-24 젤리 방석과 합침): 무너지는 발판이 CushionShakeTime초 버티고(CrumblingPlatform), 깜빡 발판은 사라진 뒤에도 CottonCloudGrace초 더 밟힌다 (Traps/BlinkPlatform)
        EchoCrystal, // 메아리 수정 (2026-11-24 새): 같은 속성 공격 마법을 연달아 쓰면 두 번째는 마나 × EchoCrystalCostRate (SpellCaster)
        ElementResonance, // 속성 공명석 (2026-11-24 새): 적의 약점 속성으로 맞히면 피해 × (1 + ResonanceDamageBonus) (CombatStats.WeaknessDamageBonus)
        // 새 유물은 맨 뒤에만 더한다: 저장(RunSave)이 번호로 기억한다.
    }

    public static class RelicBook
    {
        public const int OfferSize = 3;
        public const int CloverOfferSize = 4; // 네잎 클로버가 있으면

        // 효과 수치 (PlayerRelics가 적용한다).
        public const float ManaCrystalMana = 20f;
        public const float IronCloakDefense = 8f;
        public const float SwiftBootsSpeed = 0.08f;
        public const float WrathCharmDamage = 0.12f;
        public const float LifeSeedHealth = 20f;
        public const float SpringRingRegen = 0.10f; // 2026-11-24: 25% → 10% (마나 샘 방 +10%와 중첩)
        public const float HawkEyeCrit = 0.08f;
        public const float VampireFangHeal = 3f;
        public const float FocusBeadRefund = 0.25f;
        public const float ShadowVeilGrace = 0.5f;
        public const float BloodPactHealthPerMana = 0.5f; // 필요한 마나의 절반만큼 체력 (2026-11-24, 예: 40마나 → 체력 20). 횟수 제한 없음
        public const float BloodPactMinHealthLeft = 1f;
        public const int PhoenixFeatherSaves = 1;
        public const float TwinRingMana = 5f;
        public const int TwinRingPercent = 90; // MagicCircleRules.PerfectAt과 같게 ("완벽!")
        public const float CheerRibbonMana = 4f;
        public const float StarShardMana = 10f; // 2026-11-24: 15% 확률 +6 → 늘 +10
        public const float HeroMedalHealRatio = 0.15f;
        public const float AcornPouchMaxHealth = 1f; // 2026-11-24: 체력 +10 회복 → 최대 체력 +1
        public const float CushionShakeTime = 0.9f; // 솜구름 방석: 무너지는 발판 흔들림 0.55초 → 0.9초 (옛 젤리 방석)
        public const float StarCandyMaxMana = 1f;   // 2026-11-24: 마나 +30 회복 → 최대 마나 +1
        public const float MagnetRadius = 1.5f;          // 보상 구슬 자석 거리 (유닛, ArenaReward). 말굽 자석은 2026-11-24 삭제
        public const float PaperPlaneTeleportCost = 0.75f; // 순간이동 마나 20 → 15
        public const float LuckyPouchGoldBonus = 0.25f;    // 몬스터 골드 +25% (2026-11-24: +50%에서 줄임)
        public const float CottonCloudGrace = 0.5f;        // 깜빡 발판(사라짐 1.4초) 중 처음 0.5초는 솜구름 방석이 받쳐 준다
        public const float EchoCrystalCostRate = 0.5f;     // 메아리 수정: 같은 속성 두 번째 마법 마나 절반
        public const float ResonanceDamageBonus = 0.2f;    // 속성 공명석: 약점 속성 피해 +20%
        public const float HoneyJarHealBonus = 0.15f; // 보상 구슬 30% → 45% (생명 5 특성 45%와 합하면 60%)

        // 상자 후보 26종 (2026-11-24 RelicList.md 순서: 능력치 9 · 회복 6 · 안전 3 · 탐험 2 · 골드 1 · 특수 5)
        public static readonly RelicId[] All =
        {
            RelicId.ManaCrystal, RelicId.IronCloak, RelicId.SwiftBoots, RelicId.WrathCharm, RelicId.LifeSeed,
            RelicId.SpringRing, RelicId.HawkEye, RelicId.AcornPouch, RelicId.StarCandy,
            RelicId.VampireFang, RelicId.StarShard, RelicId.TwinRing, RelicId.CheerRibbon, RelicId.HeroMedal, RelicId.HoneyJar,
            RelicId.FocusBead, RelicId.ShadowVeil, RelicId.PhoenixFeather,
            RelicId.TreasureMap, RelicId.CottonCloud,
            RelicId.LuckyPouch,
            RelicId.BloodPact, RelicId.Clover, RelicId.PaperPlane, RelicId.EchoCrystal, RelicId.ElementResonance
        };

        // 빠진 유물 (2026-11-24). 예전 저장에서만 나올 수 있다. 합친 둘(나침반·젤리 방석)은 합쳐진 유물로 친다 (Merged).
        public static bool IsRetired(RelicId id) => Array.IndexOf(All, id) < 0;

        // 합친 유물: 예전 저장의 나침반 = 보물 지도, 젤리 방석 = 솜구름 방석. 나머지는 그대로.
        public static RelicId Merged(RelicId id)
        {
            switch (id)
            {
                case RelicId.Compass: return RelicId.TreasureMap;
                case RelicId.JellyCushion: return RelicId.CottonCloud;
                default: return id;
            }
        }

        public static string Name(RelicId id)
        {
            switch (id)
            {
                case RelicId.ManaCrystal: return "마력 수정";
                case RelicId.IronCloak: return "무쇠 망토";
                case RelicId.SwiftBoots: return "바람 장화";
                case RelicId.WrathCharm: return "분노의 부적";
                case RelicId.LifeSeed: return "생명의 씨앗";
                case RelicId.SpringRing: return "샘물 반지";
                case RelicId.HawkEye: return "매의 눈";
                case RelicId.Featherfall: return "깃털 부적";
                case RelicId.VampireFang: return "흡혈 송곳니";
                case RelicId.ShadowVeil: return "그림자 베일";
                case RelicId.BloodPact: return "피의 계약";
                case RelicId.PhoenixFeather: return "불사조 깃털";
                case RelicId.Hourglass: return "되감기 모래시계";
                case RelicId.TwinRing: return "쌍둥이 반지";
                case RelicId.Compass: return "탐험가의 나침반";
                case RelicId.Clover: return "네잎 클로버";
                case RelicId.CheerRibbon: return "응원 리본";
                case RelicId.StarShard: return "별똥 조각";
                case RelicId.HeroMedal: return "용사의 훈장";
                case RelicId.AcornPouch: return "도토리 주머니";
                case RelicId.HoneyJar: return "꿀단지";
                case RelicId.JellyCushion: return "젤리 방석";
                case RelicId.StarCandy: return "별사탕";
                case RelicId.HorseshoeMagnet: return "말굽 자석";
                case RelicId.PaperPlane: return "종이비행기";
                case RelicId.LuckyPouch: return "복주머니";
                case RelicId.RegularStamp: return "단골 도장";
                case RelicId.PiggyBank: return "돼지 저금통";
                case RelicId.TreasureMap: return "보물 지도";
                case RelicId.CottonCloud: return "솜구름 방석";
                case RelicId.EchoCrystal: return "메아리 수정";
                case RelicId.ElementResonance: return "속성 공명석";
                default: return "집중 구슬";
            }
        }

        public static string Description(RelicId id)
        {
            switch (id)
            {
                case RelicId.ManaCrystal: return $"최대 마나 +{ManaCrystalMana:0}";
                case RelicId.IronCloak: return $"방어력 +{IronCloakDefense:0}";
                case RelicId.SwiftBoots: return $"이동속도 +{SwiftBootsSpeed * 100f:0}%";
                case RelicId.WrathCharm: return $"주는 피해 +{WrathCharmDamage * 100f:0}%";
                case RelicId.LifeSeed: return $"최대 체력 +{LifeSeedHealth:0}";
                case RelicId.SpringRing: return $"마나 재생 +{SpringRingRegen * 100f:0}%";
                case RelicId.HawkEye: return $"치명타 확률 +{HawkEyeCrit * 100f:0}%";
                case RelicId.Featherfall: return "(빠진 유물)";
                case RelicId.VampireFang: return $"적 처치 시 체력 +{VampireFangHeal:0}";
                case RelicId.ShadowVeil: return $"대쉬가 끝난 뒤 {ShadowVeilGrace:0.0}초 더 무적";
                case RelicId.BloodPact: return $"마나가 모자라면 필요한 마나의 {BloodPactHealthPerMana * 100f:0}%만큼 체력으로 대신 내기";
                case RelicId.PhoenixFeather: return "쓰러질 피해를 한 번 버티고 체력 30%로";
                case RelicId.Hourglass: return "(빠진 유물)";
                case RelicId.TwinRing: return $"마법진을 {TwinRingPercent}% 이상으로 그리면 마나 +{TwinRingMana:0}";
                case RelicId.Compass: return "(보물 지도로 합쳐짐)";
                case RelicId.Clover: return $"유물 상자 후보 {OfferSize}개 → {CloverOfferSize}개";
                case RelicId.CheerRibbon: return $"B의 공격 마법이 발동하면 마나 +{CheerRibbonMana:0}";
                case RelicId.StarShard: return $"적 처치 시 마나 +{StarShardMana:0}";
                case RelicId.HeroMedal: return $"중간보스나 보스를 처치하면 체력 {HeroMedalHealRatio * 100f:0}% 회복";
                case RelicId.AcornPouch: return $"레벨이 오를 때마다 최대 체력 +{AcornPouchMaxHealth:0}";
                case RelicId.HoneyJar: return $"보상 구슬을 얻으면 체력 회복량 +{HoneyJarHealBonus * 100f:0}%";
                case RelicId.JellyCushion: return "(솜구름 방석으로 합쳐짐)";
                case RelicId.StarCandy: return $"레벨이 오를 때마다 최대 마나 +{StarCandyMaxMana:0}";
                case RelicId.HorseshoeMagnet: return "(빠진 유물)";
                case RelicId.RegularStamp: return "(빠진 유물)";
                case RelicId.TreasureMap: return "미니맵에 안 가 본 방이 흐리게, 마을·보스 방은 아이콘까지 보인다";
                case RelicId.CottonCloud: return $"무너지는 발판이 {CushionShakeTime:0.0}초 버티고, 깜빡 발판은 사라진 뒤에도 {CottonCloudGrace:0.0}초 더 밟힌다";
                case RelicId.EchoCrystal: return $"같은 속성 마법을 연달아 쓰면 두 번째는 마나 {EchoCrystalCostRate * 100f:0}%";
                case RelicId.ElementResonance: return $"적의 약점 속성으로 맞히면 피해 +{ResonanceDamageBonus * 100f:0}%";
                case RelicId.PiggyBank: return "(빠진 유물)";
                case RelicId.LuckyPouch: return $"몬스터가 떨어뜨리는 골드 +{Math.Round(LuckyPouchGoldBonus * 100f)}%";
                case RelicId.PaperPlane: return $"순간이동 마나 -{Math.Round((1f - PaperPlaneTeleportCost) * 100f)}%";
                default: return $"실패로 날린 마나 {FocusBeadRefund * 100f:0}% 돌려받기";
            }
        }

        // 복주머니: 몬스터 한 마리가 떨어뜨린 골드(amount)를 늘린 값. 반올림(.5는 올림)이라 1골드도 2골드가 된다. 0 이하는 그대로.
        public static int LuckyPouchGold(int amount) =>
            amount <= 0 ? amount : (int)Math.Round(amount * (1.0 + LuckyPouchGoldBonus), MidpointRounding.AwayFromZero);

        // 피의 계약: 마나가 모자란 마법(필요한 마나 required)을 체력으로 낼 수 있으면 true와 그 체력 값을 돌려준다.
        // 체력 = 필요한 마나 × BloodPactHealthPerMana (절반). 마나는 쓰지 않는다. 낸 뒤에도 체력이 BloodPactMinHealthLeft 이상 남아야 한다.
        public static bool BloodPactCost(float health, float required, out float cost)
        {
            cost = Math.Max(0f, required) * BloodPactHealthPerMana;
            return required > 0f && health - cost >= BloodPactMinHealthLeft;
        }

        // 메아리 수정: 바로 전에 발동한 마법(lastElement, 없으면 -1)과 속성이 같으면 이번 마법은 마나 × EchoCrystalCostRate.
        // 할인받아 발동하면 사슬이 끊겨(-1) 셋째는 다시 제값이다 (불·불·불 = 온값·절반·온값). 실패하면 사슬은 끊긴다.
        public static bool EchoDiscounted(int lastElement, int element) => lastElement >= 0 && lastElement == element;

        public static int EchoAfterCast(int lastElement, int element) => EchoDiscounted(lastElement, element) ? -1 : element;

        // 상자 하나가 보여 줄 후보. 시드와 상자가 놓인 청크로 정해지므로 같은 맵·같은 보유 유물이면 늘 같은 후보가 나온다.
        // 이미 가진 유물은 빼고, 남은 게 OfferSize보다 적으면 남은 것만 돌려준다 (다 가졌으면 빈 배열).
        // 네잎 클로버를 가졌으면 CloverOfferSize개 (클로버 자신은 이미 가졌으므로 후보에서 빠진다).
        public static RelicId[] Offer(int seed, int chunkIndex, IEnumerable<RelicId> owned)
        {
            var have = owned != null ? new HashSet<RelicId>(owned) : new HashSet<RelicId>();
            return Offer(seed, chunkIndex, have, have.Contains(RelicId.Clover) ? CloverOfferSize : OfferSize);
        }

        public static RelicId[] Offer(int seed, int chunkIndex, IEnumerable<RelicId> owned, int size)
        {
            var have = owned != null ? new HashSet<RelicId>(owned) : new HashSet<RelicId>();
            var pool = new List<RelicId>();
            foreach (var id in All)
            {
                if (!have.Contains(id)) pool.Add(id);
            }

            var rng = new Random(unchecked(seed * 486187739 + chunkIndex * 16777619 + 7));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var swap = pool[i];
                pool[i] = pool[j];
                pool[j] = swap;
            }

            int count = Math.Min(Math.Max(0, size), pool.Count);
            var offer = new RelicId[count];
            for (int i = 0; i < count; i++) offer[i] = pool[i];
            return offer;
        }
    }
}
