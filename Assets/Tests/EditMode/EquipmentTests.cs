using System;
using System.Collections.Generic;
using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 장비 데이터·가격 (Docs/EquipmentDesign.md). 수식어·리롤은 2026-11-24 사람 결정으로 뺐다.
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
        public void PriceIsBasePriceAndNameIsPlain()
        {
            Assert.AreEqual(50, new EquipmentItem(EquipmentId.EmberStaff).Price);
            Assert.AreEqual(20, new EquipmentItem(EquipmentId.EmberStaff).SellPrice); // 50 × 0.4
            Assert.AreEqual(30, new EquipmentItem(EquipmentId.ApprenticeRobe).Price);
            Assert.AreEqual("불씨 지팡이", new EquipmentItem(EquipmentId.EmberStaff).Name, "수식어 없이 장비 이름 그대로");
            Assert.AreEqual("견습생 로브", new EquipmentItem(EquipmentId.ApprenticeRobe).Name);
        }

        [Test]
        public void StatsAreExactlyBaseStats()
        {
            foreach (var def in EquipmentBook.All)
            {
                var stats = new EquipmentItem(def.Id).Stats();
                Assert.AreEqual(def.Stats.Length, stats.Count, def.Name);
                for (int i = 0; i < stats.Count; i++)
                {
                    Assert.AreEqual(def.Stats[i].Stat, stats[i].Stat, def.Name);
                    Assert.AreEqual(def.Stats[i].Value, stats[i].Value, 1e-6, def.Name);
                }
                stats.Clear();
                Assert.AreEqual(def.Stats.Length, new EquipmentItem(def.Id).Stats().Count, "돌려준 목록을 고쳐도 장비 정의는 그대로");
            }
        }

        [Test]
        public void MagicAndManaCostStatsDescribeAsPercent()
        {
            Assert.AreEqual("마법 공격 +15%", EquipmentBook.Describe(new StatValue(EquipStat.MagicPower, 0.15f)));
            Assert.AreEqual("마나 소모 -10%", EquipmentBook.Describe(new StatValue(EquipStat.ManaCost, -0.1f)));
            Assert.IsTrue(EquipmentBook.LowerIsBetter(EquipStat.ManaCost));
            Assert.IsFalse(EquipmentBook.LowerIsBetter(EquipStat.MagicPower));
            Assert.AreEqual(-1, EquipmentBook.ElementIndex(EquipStat.MagicPower));
            Assert.AreEqual(-1, EquipmentBook.ElementIndex(EquipStat.ManaCost));
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
                ids.Add(a.Id);
            }

            Assert.GreaterOrEqual(ids.Count, 6, "상자마다 장비가 골고루 나온다");
        }
    }
}
