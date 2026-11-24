using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 손에 든 장비 하나 (Docs/EquipmentDesign.md). 장비 수식어(비틀린·흔한·숙련자의·영웅의)와 상점 "수식어 다시 뽑기"는
    // 2026-11-24 사람 결정(DesignDecisionsTodo C6)으로 뺐다 (옛 코드: Trash/Assets/Scripts/Player/Equipment/Data/EquipmentModifier.cs).
    // 같은 장비는 늘 같은 이름·가격·능력치다. 장비 종류는 나중에 늘린다.
    public sealed class EquipmentItem
    {
        public const float SellRatio = 0.4f;          // 판매가 = 가격 × 0.4
        public const int MinPrice = 5;

        public readonly EquipmentId Id;

        public EquipmentItem(EquipmentId id)
        {
            Id = id;
        }

        public EquipmentDef Def => EquipmentBook.Get(Id);
        public EquipSlot Slot => Def.Slot;

        public string Name => Def.Name;

        public int Price => Math.Max(MinPrice, Def.BasePrice);

        public int SellPrice => Math.Max(1, RoundAway(Price * SellRatio));

        public List<StatValue> Stats() => new List<StatValue>(Def.Stats);

        private static int RoundAway(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }
}
