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

        // "빛나는 불씨 지팡이" (보통이면 장비 이름만)
        public string Name
        {
            get
            {
                string modifier = EquipmentModifier.Name(Grade);
                return modifier.Length == 0 ? Def.Name : modifier + " " + Def.Name;
            }
        }

        public int Price => Math.Max(MinPrice, RoundAway(Def.BasePrice * EquipmentModifier.PriceMultiplier(Grade)));

        public int SellPrice => Math.Max(1, RoundAway(Price * SellRatio));

        public int RerollCost => Math.Max(MinPrice, RoundAway(Def.BasePrice * RerollBaseRatio * Math.Pow(RerollGrowth, RerollCount)));

        // 수식어 배율을 곱한 능력치. 정수 능력치는 반올림하고, 값이 있던 능력치는 0이 되지 않게 크기 1 이상으로 둔다.
        public List<StatValue> Stats()
        {
            float mult = EquipmentModifier.StatMultiplier(Grade);
            var list = new List<StatValue>();
            foreach (var s in Def.Stats)
            {
                float v = s.Value * mult;
                if (!EquipmentBook.IsPercent(s.Stat))
                {
                    int rounded = RoundAway(v);
                    if (rounded == 0) rounded = s.Value < 0 ? -1 : 1;
                    v = rounded;
                }

                list.Add(new StatValue(s.Stat, v));
            }

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
