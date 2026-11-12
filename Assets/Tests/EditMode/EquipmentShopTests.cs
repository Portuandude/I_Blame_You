using System.Collections.Generic;
using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 마을 장비 상점 (Docs/EquipmentDesign.md 5·6절): 진열, 사기(낀 장비를 팔아 값 깎기), 리롤 비용 증가.
    public class EquipmentShopTests
    {
        [Test]
        public void StockIsFourWithEverySlotAndSameForSameSeed()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var shop = new EquipmentShop(seed, 3);
                Assert.AreEqual(EquipmentShop.StockSize, shop.Stock.Count);
                var slots = new HashSet<EquipSlot>();
                for (int i = 0; i < 3; i++) slots.Add(shop.Stock[i].Slot);
                Assert.AreEqual(3, slots.Count, $"seed {seed}: 부위별 하나씩");
                var again = new EquipmentShop(seed, 3);
                for (int i = 0; i < shop.Stock.Count; i++)
                {
                    Assert.AreEqual(shop.Stock[i].Id, again.Stock[i].Id);
                    Assert.AreEqual(shop.Stock[i].Grade, again.Stock[i].Grade);
                }
            }
        }

        [Test]
        public void BuyRemovesFromStockAndTradesInCurrent()
        {
            var shop = new EquipmentShop(7, 2);
            var item = shop.Stock[0];
            var current = new EquipmentItem(item.Id, 0);
            int net = EquipmentShop.NetCost(item, current);
            Assert.AreEqual(System.Math.Max(0, item.Price - current.SellPrice), net);
            Assert.IsFalse(shop.CanBuy(0, current, net - 1));
            Assert.IsNull(shop.Buy(0, current, net - 1, out int none));
            Assert.AreEqual(0, none);
            Assert.AreEqual(4, shop.Stock.Count);

            var bought = shop.Buy(0, current, net, out int cost);
            Assert.AreSame(item, bought);
            Assert.AreEqual(net, cost);
            Assert.AreEqual(3, shop.Stock.Count);
            Assert.IsFalse(shop.CanBuy(5, null, 9999));
            // 낀 장비가 없으면 제값
            var next = shop.Stock[0];
            Assert.AreEqual(next.Price, EquipmentShop.NetCost(next, null));
        }

        [Test]
        public void RerollChargesCurrentCostAndRaisesIt()
        {
            var shop = new EquipmentShop(1, 1);
            var item = new EquipmentItem(shop.Stock[0].Id, 0);
            int first = item.RerollCost;
            Assert.AreEqual(-1, shop.Reroll(item, first - 1));
            Assert.AreEqual(0, item.RerollCount);
            Assert.AreEqual(first, shop.Reroll(item, 9999));
            Assert.AreEqual(1, item.RerollCount);
            Assert.Greater(item.RerollCost, first);
            Assert.That(item.Grade, Is.InRange(EquipmentModifier.MinGrade, EquipmentModifier.MaxGrade));
            Assert.IsFalse(EquipmentShop.CanReroll(null, 9999));
        }
    
        // 단골 도장 (2026-11-12): 장비 값만 깎이고 낀 장비 판매가는 그대로.
        [Test]
        public void DiscountAppliesToPriceNotTradeIn()
        {
            var shop = new EquipmentShop(7, 2);
            var item = shop.Stock[0];
            var current = new EquipmentItem(item.Id, 0);
            int discounted = VillagePrice.Apply(item.Price, 0.8f);
            Assert.Less(discounted, item.Price);
            Assert.AreEqual(discounted, EquipmentShop.NetCost(item, null, 0.8f));
            Assert.AreEqual(System.Math.Max(0, discounted - current.SellPrice), EquipmentShop.NetCost(item, current, 0.8f));
            Assert.IsFalse(shop.CanBuy(0, null, discounted - 1, 0.8f));
            Assert.IsTrue(shop.CanBuy(0, null, discounted, 0.8f));
            Assert.AreSame(item, shop.Buy(0, null, discounted, out int cost, 0.8f));
            Assert.AreEqual(discounted, cost);
        }
    }
}
