using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 마을 식당 (Docs/EquipmentDesign.md 10절): 메뉴 표, 방문마다 종류별 한 번, 골드·소용없음 검사.
    public class VillageFoodTests
    {
        [Test]
        public void MenuMatchesDesignTable()
        {
            Assert.AreEqual(5, VillageFood.Count);
            Assert.AreEqual(20, VillageFood.Get(FoodId.WarmSoup).Price);
            Assert.AreEqual(15, VillageFood.Get(FoodId.StarTea).Price);
            Assert.AreEqual(45, VillageFood.Get(FoodId.MushroomStew).Price);
            Assert.AreEqual(40, VillageFood.Get(FoodId.HoneyDumpling).Price);
            Assert.AreEqual(30, VillageFood.Get(FoodId.Lunchbox).Price);
            foreach (var food in VillageFood.All)
            {
                Assert.That(food.Price, Is.InRange(10, 60), food.Name); // 마을에서 쓰는 50~170골드 수지 안 (11절)
                Assert.Greater(food.Value, 0f, food.Name);
                Assert.IsNotEmpty(food.Describe(), food.Name);
            }
            Assert.AreEqual("체력 30% 회복", VillageFood.Get(FoodId.WarmSoup).Describe());
            Assert.AreEqual("최대 체력 +15 (이번 판)", VillageFood.Get(FoodId.MushroomStew).Describe());
            Assert.AreEqual("마나 재생 +15% (이번 판)", VillageFood.Get(FoodId.HoneyDumpling).Describe());
            Assert.AreEqual("다음 전투 구역 돌파까지 받는 피해 -20%", VillageFood.Get(FoodId.Lunchbox).Describe());
        }

        [Test]
        public void EachFoodOncePerVisit()
        {
            var visit = new RestaurantVisit();
            visit.BeginVisit(1);
            Assert.IsTrue(visit.TryBuy(FoodId.MushroomStew, 100, false, false));
            Assert.AreEqual(FoodBuyCheck.AlreadyBought, visit.Check(FoodId.MushroomStew, 100, false, false));
            Assert.IsFalse(visit.TryBuy(FoodId.MushroomStew, 100, false, false));
            Assert.AreEqual(FoodBuyCheck.Ok, visit.Check(FoodId.Lunchbox, 100, false, false)); // 다른 음식은 된다
            visit.BeginVisit(1); // 창을 닫았다 열어도 같은 방문
            Assert.IsTrue(visit.Bought(FoodId.MushroomStew));
            visit.BeginVisit(2); // 마을에 다시 들어오면 새 방문
            Assert.IsFalse(visit.Bought(FoodId.MushroomStew));
            Assert.IsTrue(visit.TryBuy(FoodId.MushroomStew, 45, false, false));
        }

        [Test]
        public void GoldAndUselessChecks()
        {
            var visit = new RestaurantVisit();
            visit.BeginVisit(0);
            Assert.AreEqual(FoodBuyCheck.NotEnoughGold, visit.Check(FoodId.WarmSoup, 19, false, false));
            Assert.AreEqual(FoodBuyCheck.Ok, visit.Check(FoodId.WarmSoup, 20, false, false));
            Assert.AreEqual(FoodBuyCheck.NoEffect, visit.Check(FoodId.WarmSoup, 100, true, false));   // 체력 가득이면 수프는 소용없음
            Assert.AreEqual(FoodBuyCheck.NoEffect, visit.Check(FoodId.StarTea, 100, false, true));    // 마나 가득이면 차는 소용없음
            Assert.AreEqual(FoodBuyCheck.Ok, visit.Check(FoodId.MushroomStew, 100, true, true));      // 이번 판 효과는 가득이어도 산다
            Assert.IsFalse(visit.TryBuy(FoodId.WarmSoup, 5, false, false));
            Assert.IsFalse(visit.Bought(FoodId.WarmSoup)); // 실패하면 기록 안 함
        }
    }
}
