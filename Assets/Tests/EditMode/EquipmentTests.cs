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
        public void RollBonusRaisesGradeButNotPastHero()
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
            Assert.AreEqual(50, new EquipmentItem(EquipmentId.EmberStaff, 0).Price);   // 흔한
            Assert.AreEqual(70, new EquipmentItem(EquipmentId.EmberStaff, 1).Price);   // 숙련자의 50 × 1.4
            Assert.AreEqual(100, new EquipmentItem(EquipmentId.EmberStaff, 2).Price);  // 영웅의 50 × 2
            Assert.AreEqual(35, new EquipmentItem(EquipmentId.EmberStaff, -1).Price);  // 비틀린 50 × 0.7
            Assert.AreEqual(40, new EquipmentItem(EquipmentId.EmberStaff, 2).SellPrice); // 100 × 0.4
            Assert.AreEqual(21, new EquipmentItem(EquipmentId.ApprenticeRobe, -1).Price); // 30 × 0.7
        }

        [Test]
        public void NameAlwaysShowsModifier()
        {
            Assert.AreEqual("흔한 불씨 지팡이", new EquipmentItem(EquipmentId.EmberStaff, 0).Name);
            Assert.AreEqual("영웅의 불씨 지팡이", new EquipmentItem(EquipmentId.EmberStaff, 2).Name);
            Assert.AreEqual("숙련자의 불씨 지팡이", new EquipmentItem(EquipmentId.EmberStaff, 1).Name);
            Assert.AreEqual("비틀린 견습생 로브", new EquipmentItem(EquipmentId.ApprenticeRobe, -1).Name);
            Assert.AreEqual("비틀린 견습생 로브", new EquipmentItem(EquipmentId.ApprenticeRobe, -5).Name, "범위 밖 등급은 끝으로 자른다");
        }

        [Test]
        public void ModifierKeepsBaseStatsAndAddsMagicAndManaCost()
        {
            var def = EquipmentBook.Get(EquipmentId.MossRobe);
            foreach (int grade in new[] { -1, 0, 1, 2 })
            {
                var stats = new EquipmentItem(EquipmentId.MossRobe, grade).Stats();
                for (int i = 0; i < def.Stats.Length; i++)
                {
                    Assert.AreEqual(def.Stats[i].Stat, stats[i].Stat);
                    Assert.AreEqual(def.Stats[i].Value, stats[i].Value, $"등급 {grade}: 기본 능력치는 그대로");
                }
            }

            Assert.AreEqual(def.Stats.Length, new EquipmentItem(EquipmentId.MossRobe, 0).Stats().Count, "흔한은 더하는 것 없음");

            var twisted = new EquipmentItem(EquipmentId.MossRobe, -1).Stats();
            Assert.AreEqual(-EquipmentModifier.MagicBig, Find(twisted, EquipStat.MagicPower), 1e-6, "비틀린: 마법 공격 --");
            Assert.AreEqual(EquipmentModifier.CostStep, Find(twisted, EquipStat.ManaCost), 1e-6, "비틀린: 마나 소모 +");

            var skilled = new EquipmentItem(EquipmentId.MossRobe, 1).Stats();
            Assert.IsFalse(Has(skilled, EquipStat.MagicPower), "숙련자의: 마법 공격 변화 없음");
            Assert.AreEqual(-EquipmentModifier.CostStep, Find(skilled, EquipStat.ManaCost), 1e-6, "숙련자의: 마나 소모 -");

            var hero = new EquipmentItem(EquipmentId.MossRobe, 2).Stats();
            Assert.AreEqual(EquipmentModifier.MagicBig, Find(hero, EquipStat.MagicPower), 1e-6, "영웅의: 마법 공격 ++");
            Assert.AreEqual(-EquipmentModifier.CostStep, Find(hero, EquipStat.ManaCost), 1e-6, "영웅의: 마나 소모 -");
        }

        [Test]
        public void ModifierStatsDescribeAsPercent()
        {
            Assert.AreEqual("마법 공격 +15%", EquipmentBook.Describe(new StatValue(EquipStat.MagicPower, 0.15f)));
            Assert.AreEqual("마나 소모 -10%", EquipmentBook.Describe(new StatValue(EquipStat.ManaCost, -0.1f)));
            Assert.IsTrue(EquipmentBook.LowerIsBetter(EquipStat.ManaCost));
            Assert.IsFalse(EquipmentBook.LowerIsBetter(EquipStat.MagicPower));
            Assert.AreEqual(-1, EquipmentBook.ElementIndex(EquipStat.MagicPower));
            Assert.AreEqual(-1, EquipmentBook.ElementIndex(EquipStat.ManaCost));
        }

        private static float Find(List<StatValue> stats, EquipStat stat)
        {
            foreach (var s in stats) if (s.Stat == stat) return s.Value;
            Assert.Fail(stat + " 없음");
            return 0f;
        }

        private static bool Has(List<StatValue> stats, EquipStat stat)
        {
            foreach (var s in stats) if (s.Stat == stat) return true;
            return false;
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
            Assert.AreEqual("이동속도 -2.5%", EquipmentBook.Describe(new StatValue(EquipStat.MoveSpeed, -0.025f)));
            Assert.AreEqual(1, EquipmentBook.ElementIndex(EquipStat.WaterDamage));
            Assert.AreEqual(3, EquipmentBook.ElementIndex(EquipStat.EarthDefense));
            Assert.AreEqual(-1, EquipmentBook.ElementIndex(EquipStat.Defense));
        }
    
        [Test]
        public void ChestOfferIsDeterministicAndVaried()
        {
            var ids = new HashSet<EquipmentId>();
            for (int chunk = 0; chunk < 40; chunk++)
            {
                var a = EquipmentOffer.Roll(123, chunk);
                var b = EquipmentOffer.Roll(123, chunk);
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Grade, b.Grade);
                ids.Add(a.Id);
                Assert.GreaterOrEqual(EquipmentOffer.Roll(123, chunk, EquipmentOffer.MidBossGradeBonus).Grade, EquipmentModifier.MinGrade + 1);
            }

            Assert.GreaterOrEqual(ids.Count, 6, "상자마다 장비가 골고루 나온다");
        }
    }
}
