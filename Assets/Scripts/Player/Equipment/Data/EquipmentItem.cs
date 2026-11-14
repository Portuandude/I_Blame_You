using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 손에 든 장비 하나: 장비 종류 + 수식어 등급 + 이 장비를 리롤한 횟수 (Docs/EquipmentDesign.md 4~6절).
    public sealed class EquipmentItem
    {
        public const float SellRatio = 0.4f;          // 판매가 = 수식어 반영 가격 × 0.4
        public const float RerollBaseRatio = 0.3f;    // 첫 리롤 비용 = 기본 가격 × 0.3
        public const float RerollGrowth = 1.6f;       // 리롤할 때마다 비용 ×1.6 (장비마다 따로)
        public const int MinPrice = 5;

        public readonly EquipmentId Id;
        public int Grade { get; private set; }
        public int RerollCount { get; private set; }

        public EquipmentItem(EquipmentId id, int grade)
        {
            Id = id;
            Grade = Math.Max(EquipmentModifier.MinGrade, Math.Min(EquipmentModifier.MaxGrade, grade));
        }

        public EquipmentDef Def => EquipmentBook.Get(Id);
        public EquipSlot Slot => Def.Slot;

        // "영웅의 불씨 지팡이" (수식어는 늘 붙는다, 흔한 포함)
        public string Name => EquipmentModifier.Name(Grade) + " " + Def.Name;

        public int Price => Math.Max(MinPrice, RoundAway(Def.BasePrice * EquipmentModifier.PriceMultiplier(Grade)));

        public int SellPrice => Math.Max(1, RoundAway(Price * SellRatio));

        public int RerollCost => Math.Max(MinPrice, RoundAway(Def.BasePrice * RerollBaseRatio * Math.Pow(RerollGrowth, RerollCount)));

        // 장비 기본 능력치 그대로 + 수식어가 더하는 마법 공격·마나 소모 (2026-11-14 수식어 4종).
        public List<StatValue> Stats()
        {
            var list = new List<StatValue>(Def.Stats);
            list.AddRange(EquipmentModifier.Stats(Grade));
            return list;
        }

        // 수식어 다시 뽑기: 확률표에서 새로 뽑는다(같은 등급이 다시 나올 수도, 나빠질 수도 있다). 리롤 횟수 +1 → 다음 비용이 오른다.
        // 반환: 새 등급. 비용 지불은 부르는 쪽(상점)이 RerollCost를 먼저 읽고 한다.
        public int Reroll(Random rng)
        {
            Grade = EquipmentModifier.Roll(rng);
            RerollCount++;
            return Grade;
        }

        private static int RoundAway(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }
}
