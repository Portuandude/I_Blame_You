using System;
using System.Collections.Generic;
using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 장비 데이터·수식어·가격·리롤 비용 (Docs/EquipmentDesign.md 3~6절).
    public class EquipmentTests
    {
        [Test]
        public void BookHasFourItemsPerSlotInIdOrder()
        {
            var all = EquipmentBook.All;
            Assert.AreEqual(Enum.GetValues(typeof(EquipmentId)).Length, all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                Assert.AreEqual((EquipmentId)i, all[i].Id, "Get(id)가 배열 번호로 찾으므로 순서가 맞아야 한다");
                Assert.Greater(all[i].Stats.Length, 0, all[i].Name);
                Assert.Greater(all[i].BasePrice, 0, all[i].Name);
            }

            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
                Assert.AreEqual(4, EquipmentBook.OfSlot(slot).Count, slot.ToString());
        }

        [Test]
        public void ModifierWeightsArePercentAndRollMatchesThem()
        {
            Assert.AreEqual(100, EquipmentModifier.TotalWeight);
            var counts = new Dictionary<int, int>();
            var rng = new Random(7);
            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                int g = EquipmentModifier.Roll(rng);
                Assert.That(g, Is.InRange(EquipmentModifier.MinGrade, EquipmentModifier.MaxGrade));
                counts[g] = counts.TryGetValue(g, out int c) ? c + 1 : 1;
            }

            for (int g = EquipmentModifier.MinGrade; g <= EquipmentModifier.MaxGrade; g++)
            {
                double expected = EquipmentModifier.Weight(g) / 100.0;
                double actual = (counts.TryGetValue(g, out int c) ? c : 0) / (double)n;
                Assert.AreEqual(expected, actual, 0.02, $"등급 {g}");
            }
        }

        [Test]
        public void RollBonusRaisesGradeButNotPastLegendary()
        {
            var rng = new Random(3);
            bool sawMax = false;
            for (int i = 0; i < 2000; i++)
            {
                int g = EquipmentModifier.Roll(rng, 1);
                Assert.That(g, Is.InRange(EquipmentModifier.MinGrade + 1, EquipmentModifier.MaxGrade));
                if (g == EquipmentModifier.MaxGrade) sawMax = true;
            }

            Assert.IsTrue(sawMax);
        }

        [Test]
        public void PriceAndSellPriceFollowModifier()
        {
            Assert.AreEqual(50, new EquipmentItem(EquipmentId.EmberStaff, 0).Price);
            Assert.AreEqual(95, new EquipmentItem(EquipmentId.EmberStaff, 2).Price);   // 50 × 1.9
            Assert.AreEqual(140, new EquipmentItem(EquipmentId.EmberStaff, 3).Price);  // 50 × 2.8
            Assert.AreEqual(20, new EquipmentItem(EquipmentId.EmberStaff, -2).Price);  // 50 × 0.4
            Assert.AreEqual(38, new EquipmentItem(EquipmentId.EmberStaff, 2).SellPrice); // 95 × 0.4
            Assert.AreEqual(12, new EquipmentItem(EquipmentId.ApprenticeRobe, -2).Price); // 30 × 0.4
        }

        [Test]
        public void NameShowsModifierExceptNormal()
        {
            Assert.AreEqual("불씨 지팡이", new EquipmentItem(EquipmentId.EmberStaff, 0).Name);
            Assert.AreEqual("빛나는 불씨 지팡이", new EquipmentItem(EquipmentId.EmberStaff, 2).Name);
            Assert.AreEqual("부서진 견습생 로브", new EquipmentItem(EquipmentId.ApprenticeRobe, -2).Name);
        }

        [Test]
        public void StatsScaleAndNeverDropToZero()
        {
            var legend = new EquipmentItem(EquipmentId.MossRobe, 3).Stats();
            Assert.AreEqual(6f, legend[0].Value);  // 방어력 3 × 2
            Assert.AreEqual(20f, legend[1].Value); // 체력 10 × 2

            var broken = new EquipmentItem(EquipmentId.EmberStaff, -2).Stats();
            Assert.AreEqual(1f, broken[0].Value, "물리 피해 1 × 0.5 = 0.5 → 사라지지 않고 1");
            Assert.AreEqual(3f, broken[1].Value, "불 피해 5 × 0.5 = 2.5 → 반올림 3");

            var bolt = new EquipmentItem(EquipmentId.BoltShoes, 1).Stats();
            Assert.AreEqual(0.0625f, bolt[0].Value, 1e-5, "비율 능력치는 반올림하지 않는다");
            Assert.AreEqual(-0.1f, bolt[1].Value, 1e-5);
        }

        [Test]
        public void RerollCostGrowsPerItem()
        {
            var staff = new EquipmentItem(EquipmentId.EmberStaff, 0);
            var other = new EquipmentItem(EquipmentId.EmberStaff, 0);
            var rng = new Random(1);
            var costs = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                costs.Add(staff.RerollCost);
                staff.Reroll(rng);
            }

            CollectionAssert.AreEqual(new[] { 15, 24, 38, 61 }, costs);
            Assert.AreEqual(4, staff.RerollCount);
            Assert.AreEqual(15, other.RerollCost, "리롤 횟수는 장비마다 따로");
        }

        [Test]
        public void RerollCanGetWorse()
        {
            var rng = new Random(11);
            bool worse = false, better = false;
            for (int i = 0; i < 200; i++)
            {
                var item = new EquipmentItem(EquipmentId.WoodStaff, 0);
                int g = item.Reroll(rng);
                if (g < 0) worse = true;
                if (g > 0) better = true;
            }

            Assert.IsTrue(worse && better);
        }

        [Test]
        public void DescribeFormatsFlatAndPercent()
        {
            Assert.AreEqual("방어력 +4", EquipmentBook.Describe(new StatValue(EquipStat.Defense, 4)));
            Assert.AreEqual("이동속도 +5%", EquipmentBook.Describe(new StatValue(EquipStat.MoveSpeed, 0.05f)));
            Assert.AreEqual("순간이동 대기 -8%", EquipmentBook.Describe(new StatValue(EquipStat.TeleportCooldown, -0.08f)));
            Assert.AreEqual(1, EquipmentBook.ElementIndex(EquipStat.WaterDamage));
            Assert.AreEqual(3, EquipmentBook.ElementIndex(EquipStat.EarthDefense));
            Assert.AreEqual(-1, EquipmentBook.ElementIndex(EquipStat.Defense));
        }
    }
}
