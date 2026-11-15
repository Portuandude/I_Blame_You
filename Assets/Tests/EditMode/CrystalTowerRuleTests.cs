using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 수정 탑 영구 능력치 (CrystalTowerRule, Docs/CityDesign.md 4절)
    public class CrystalTowerRuleTests
    {
        [Test]
        public void Costs_MatchDesignTable()
        {
            Assert.AreEqual(20, CrystalTowerRule.Cost(TowerStat.MaxHealth, 0));
            Assert.AreEqual(110, CrystalTowerRule.Cost(TowerStat.MaxMana, 4));
            Assert.AreEqual(25, CrystalTowerRule.Cost(TowerStat.ManaRegen, 0));
            Assert.AreEqual(115, CrystalTowerRule.Cost(TowerStat.ShieldDuration, 4));
            Assert.AreEqual(-1, CrystalTowerRule.Cost(TowerStat.MaxHealth, 5));
            Assert.AreEqual(-1, CrystalTowerRule.Cost(TowerStat.MaxHealth, -1));
        }

        [Test]
        public void TotalCost_AboutThirteenHundred()
        {
            Assert.AreEqual(2 * 300 + 2 * 325, CrystalTowerRule.TotalCost());
        }

        [Test]
        public void Bonus_PerLevelAndClamped()
        {
            Assert.AreEqual(20f, CrystalTowerRule.Bonus(TowerStat.MaxHealth, 5), 1e-4);
            Assert.AreEqual(0.15f, CrystalTowerRule.Bonus(TowerStat.ManaRegen, 5), 1e-4);
            Assert.AreEqual(0.5f, CrystalTowerRule.Bonus(TowerStat.ShieldDuration, 9), 1e-4);
            Assert.AreEqual(0f, CrystalTowerRule.Bonus(TowerStat.MaxMana, 0), 1e-4);
            Assert.AreEqual("+9%", CrystalTowerRule.BonusText(TowerStat.ManaRegen, 3));
            Assert.AreEqual("+0.2초", CrystalTowerRule.BonusText(TowerStat.ShieldDuration, 2));
            Assert.AreEqual("+8", CrystalTowerRule.BonusText(TowerStat.MaxHealth, 2));
        }

        [Test]
        public void CanBuy_NeedsDustAndRoom()
        {
            Assert.IsTrue(CrystalTowerRule.CanBuy(TowerStat.MaxHealth, 0, 20));
            Assert.IsFalse(CrystalTowerRule.CanBuy(TowerStat.MaxHealth, 0, 19));
            Assert.IsFalse(CrystalTowerRule.CanBuy(TowerStat.MaxHealth, 5, 9999));
        }
    }
}
