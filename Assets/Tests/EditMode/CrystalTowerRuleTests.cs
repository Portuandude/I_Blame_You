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
            Assert.AreEqual(115, CrystalTowerRule.Cost(TowerStat.CircleEase, 4));
            Assert.AreEqual(-1, CrystalTowerRule.Cost(TowerStat.MaxHealth, 5));
            Assert.AreEqual(-1, CrystalTowerRule.Cost(TowerStat.MaxHealth, -1));
        }

        [Test]
        public void TotalCost_AboutThirteenHundred()
        {
            Assert.AreEqual(2 * 300 + 4 * 325, CrystalTowerRule.TotalCost());
        }

        [Test]
        public void Bonus_PerLevelAndClamped()
        {
            Assert.AreEqual(20f, CrystalTowerRule.Bonus(TowerStat.MaxHealth, 5), 1e-4);
            Assert.AreEqual(0.15f, CrystalTowerRule.Bonus(TowerStat.ManaRegen, 5), 1e-4);
            Assert.AreEqual(0.2f, CrystalTowerRule.Bonus(TowerStat.CircleEase, 9), 1e-4);
            Assert.AreEqual(0f, CrystalTowerRule.Bonus(TowerStat.MaxMana, 0), 1e-4);
            Assert.AreEqual("+9%", CrystalTowerRule.BonusText(TowerStat.ManaRegen, 3));
            Assert.AreEqual("+8%", CrystalTowerRule.BonusText(TowerStat.CircleEase, 2));
            Assert.AreEqual("마법진 여유", CrystalTowerRule.Name(TowerStat.CircleEase));
            Assert.AreEqual("+8", CrystalTowerRule.BonusText(TowerStat.MaxHealth, 2));
            // 2026-11-24 순간이동·점프력 강화
            Assert.AreEqual(25, CrystalTowerRule.Cost(TowerStat.Teleport, 0));
            Assert.AreEqual(115, CrystalTowerRule.Cost(TowerStat.Jump, 4));
            Assert.AreEqual("-12%", CrystalTowerRule.BonusText(TowerStat.Teleport, 3));
            Assert.AreEqual("+10%", CrystalTowerRule.BonusText(TowerStat.Jump, 5));
            Assert.AreEqual(0.8f, CrystalTowerRule.TeleportCostMultiplier(5), 1e-4);
            Assert.AreEqual(1f, CrystalTowerRule.TeleportCostMultiplier(0), 1e-4);
            Assert.AreEqual(1.1f, CrystalTowerRule.JumpMultiplier(5), 1e-4);
            Assert.AreEqual(1.1f, CrystalTowerRule.JumpMultiplier(8), 1e-4, "5단계 넘게는 안 오른다");
            Assert.AreEqual("순간이동 마나", CrystalTowerRule.Name(TowerStat.Teleport));
            Assert.AreEqual("점프력", CrystalTowerRule.Name(TowerStat.Jump));
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
