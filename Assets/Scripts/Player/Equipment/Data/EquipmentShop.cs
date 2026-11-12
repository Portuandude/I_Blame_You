using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 마을 장비 상점 (Docs/EquipmentDesign.md 5·6절). UnityEngine 없이 돌아서 진열·가격·리롤 규칙을 EditMode 테스트로 고정한다.
    // 진열: 부위별 1개씩 3개 + 무작위 1개 = 4개, 수식어는 4절 확률. 시드 = 스테이지 시드 + 방 번호 (다시 들어와도 같은 진열, 산 것은 빠짐).
    // 사기: 그 부위에 낀 장비가 있으면 판매가로 팔아 값을 깎고(가방 없음, 한 부위에 하나) 새 장비를 낀다.
    // 팔기: 낀 장비를 판매가(수식어 반영 가격 × 0.4)에 판다. 리롤: 낀 장비의 수식어를 다시 뽑는다 (장비마다 비용 ×1.6).
    public sealed class EquipmentShop
    {
        public const int StockSize = 4;

        private readonly List<EquipmentItem> stock = new List<EquipmentItem>();
        private readonly Random rerollRng;

        public int Seed { get; }
        public int Room { get; }
        public IReadOnlyList<EquipmentItem> Stock => stock; // 아직 안 팔린 진열 (산 것은 빠진다)

        public EquipmentShop(int seed, int room)
        {
            Seed = seed;
            Room = room;
            var rng = new Random(unchecked(seed * 31337 + room * 7919 + 5)); // 유물·상자 장비 난수열과 따로
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                var defs = EquipmentBook.OfSlot(slot);
                stock.Add(new EquipmentItem(defs[rng.Next(defs.Count)].Id, EquipmentModifier.Roll(rng)));
            }
            var all = EquipmentBook.All;
            stock.Add(new EquipmentItem(all[rng.Next(all.Count)].Id, EquipmentModifier.Roll(rng)));
            rerollRng = new Random(unchecked(seed * 104729 + room * 17 + 11));
        }

        // 사는 데 실제로 드는 골드: 장비 가격 − 그 부위에 낀 장비 판매가 (0 아래로는 안 내려감 = 웃돈은 안 줌).
        public static int NetCost(EquipmentItem item, EquipmentItem current) =>
            Math.Max(0, item.Price - (current != null ? current.SellPrice : 0));

        public bool CanBuy(int index, EquipmentItem current, int gold) =>
            index >= 0 && index < stock.Count && gold >= NetCost(stock[index], current);

        // 진열에서 꺼낸다. 돌려준 장비를 부르는 쪽이 끼고, cost만큼 골드를 뺀다. 못 사면 null.
        public EquipmentItem Buy(int index, EquipmentItem current, int gold, out int cost)
        {
            cost = 0;
            if (!CanBuy(index, current, gold)) return null;
            var item = stock[index];
            cost = NetCost(item, current);
            stock.RemoveAt(index);
            return item;
        }

        public static bool CanReroll(EquipmentItem item, int gold) => item != null && gold >= item.RerollCost;

        // 낀 장비의 수식어를 다시 뽑는다. 낸 비용을 돌려준다 (못 하면 -1, 아무것도 안 바뀜). 능력치 다시 맞추기는 부르는 쪽(PlayerEquipment.Refresh).
        public int Reroll(EquipmentItem item, int gold)
        {
            if (!CanReroll(item, gold)) return -1;
            int cost = item.RerollCost;
            item.Reroll(rerollRng);
            return cost;
        }
    }
}
