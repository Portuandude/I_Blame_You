using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 유물: 한 런 동안만 가지는 아이템. 정예 전투 구역을 돌파하면 나오는 유물 상자에서 3개 중 하나를 고른다.
    // 효과는 특성 4포인트 정도의 작은 능력치 보너스이고, 한 런에 같은 유물은 한 번만 나온다.
    // 이 파일은 UnityEngine 없는 별도 어셈블리(IBlameYou.Relics)라 EditMode 테스트로 후보 고르기를 검증한다.
    public enum RelicId
    {
        ManaCrystal, // 마력 수정: 최대 마나 +20
        IronCloak,   // 무쇠 망토: 방어력 +8
        SwiftBoots,  // 바람 장화: 이동속도 +8%
        WrathCharm,  // 분노의 부적: 주는 피해 +12%
        LifeSeed,    // 생명의 씨앗: 최대 체력 +20
        SpringRing,  // 샘물 반지: 마나 재생 +25%
        HawkEye,     // 매의 눈: 치명타 확률 +8%
        Featherfall, // 깃털 부적: 낭떠러지 낙하 피해 15% → 5%
        VampireFang, // 흡혈 송곳니: 적 처치 시 체력 +3
        FocusBead,   // 집중 구슬: 실패로 날린 마나 25% 돌려받기 (신비 10과 합산)
        ShadowVeil,  // 그림자 베일: 대쉬가 끝난 뒤 0.5초 더 무적
        BloodPact,   // 피의 계약: 마나가 모자라면 모자란 만큼 체력으로 대신 낸다 (40초에 한 번, 체력 1은 남긴다)
        PhoenixFeather, // 불사조 깃털: 쓰러질 피해를 한 번 버티고 최대 체력 30%로 (특성 버티기와 합산)
        Hourglass,   // 되감기 모래시계: 낭떠러지에 떨어져도 2번은 피해 없이 돌아온다
        TwinRing,    // 쌍둥이 반지: A의 방어막이 발동하면 마나 +5 (방어막 미니게임 성공 보상)
        Compass      // 탐험가의 나침반: 미니맵에 안 가 본 방도 흐린 칸으로 보인다 (MinimapRules.Look)
        // 새 유물은 맨 뒤에만 더한다: 저장(RunSave)이 번호로 기억한다.
    }

    public static class RelicBook
    {
        public const int OfferSize = 3;

        // 효과 수치 (PlayerRelics가 적용한다).
        public const float ManaCrystalMana = 20f;
        public const float IronCloakDefense = 8f;
        public const float SwiftBootsSpeed = 0.08f;
        public const float WrathCharmDamage = 0.12f;
        public const float LifeSeedHealth = 20f;
        public const float SpringRingRegen = 0.25f;
        public const float HawkEyeCrit = 0.08f;
        public const float FeatherfallDamageRatio = 0.05f;
        public const float VampireFangHeal = 3f;
        public const float FocusBeadRefund = 0.25f;
        public const float ShadowVeilGrace = 0.5f;
        public const float BloodPactHealthPerMana = 1f;
        public const float BloodPactCooldown = 40f;
        public const float BloodPactMinHealthLeft = 1f;
        public const int PhoenixFeatherSaves = 1;
        public const int HourglassFreeFalls = 2;
        public const float TwinRingMana = 5f;

        public static readonly RelicId[] All =
        {
            RelicId.ManaCrystal, RelicId.IronCloak, RelicId.SwiftBoots, RelicId.WrathCharm,
            RelicId.LifeSeed, RelicId.SpringRing, RelicId.HawkEye, RelicId.Featherfall, RelicId.VampireFang, RelicId.FocusBead,
            RelicId.ShadowVeil, RelicId.BloodPact, RelicId.PhoenixFeather, RelicId.Hourglass, RelicId.TwinRing,
            RelicId.Compass
        };

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
                case RelicId.Featherfall: return $"낭떠러지 낙하 피해 15% → {FeatherfallDamageRatio * 100f:0}%";
                case RelicId.VampireFang: return $"적 처치 시 체력 +{VampireFangHeal:0}";
                case RelicId.ShadowVeil: return $"대쉬가 끝난 뒤 {ShadowVeilGrace:0.0}초 더 무적";
                case RelicId.BloodPact: return $"마나가 모자라면 체력으로 대신 내기 ({BloodPactCooldown:0}초에 한 번)";
                case RelicId.PhoenixFeather: return "쓰러질 피해를 한 번 버티고 체력 30%로";
                case RelicId.Hourglass: return $"낭떠러지에 떨어져도 {HourglassFreeFalls}번은 피해 없이 복귀";
                case RelicId.TwinRing: return $"A의 방어막이 발동하면 마나 +{TwinRingMana:0}";
                case RelicId.Compass: return "미니맵에 안 가 본 방도 흐리게 보인다";
                default: return $"실패로 날린 마나 {FocusBeadRefund * 100f:0}% 돌려받기";
            }
        }

        // 이 유물을 주로 반기는 쪽 (특성 창과 같은 표기: A / B / 공용).
        public static string Owner(RelicId id)
        {
            switch (id)
            {
                case RelicId.IronCloak:
                case RelicId.SwiftBoots:
                case RelicId.Featherfall:
                case RelicId.ShadowVeil:
                case RelicId.Hourglass:
                case RelicId.TwinRing: return "A";
                case RelicId.WrathCharm:
                case RelicId.HawkEye:
                case RelicId.VampireFang: return "B";
                default: return "공용";
            }
        }

        // 피의 계약: 모자란 마나(shortfall)를 체력으로 낼 수 있으면 true와 그 체력 값을 돌려준다.
        // 낸 뒤에도 체력이 BloodPactMinHealthLeft 이상 남아야 한다 (이 유물로 쓰러지지 않는다). 재사용 대기는 부르는 쪽이 본다.
        public static bool BloodPactCost(float health, float shortfall, out float cost)
        {
            cost = Math.Max(0f, shortfall) * BloodPactHealthPerMana;
            return shortfall > 0f && health - cost >= BloodPactMinHealthLeft;
        }

        // 상자 하나가 보여 줄 후보. 시드와 상자가 놓인 청크로 정해지므로 같은 맵·같은 보유 유물이면 늘 같은 후보가 나온다.
        // 이미 가진 유물은 빼고, 남은 게 OfferSize보다 적으면 남은 것만 돌려준다 (다 가졌으면 빈 배열).
        public static RelicId[] Offer(int seed, int chunkIndex, IEnumerable<RelicId> owned)
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

            int count = Math.Min(OfferSize, pool.Count);
            var offer = new RelicId[count];
            for (int i = 0; i < count; i++) offer[i] = pool[i];
            return offer;
        }
    }
}
