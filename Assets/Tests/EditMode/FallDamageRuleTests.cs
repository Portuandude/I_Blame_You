using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 낙사 피해: 쓰러지지 않고 체력 1을 남긴다 (2026-11-24 사람 결정).
    public class FallDamageRuleTests
    {
        [Test]
        public void NormalFallTakesFullRatio()
        {
            Assert.AreEqual(15f, FallDamageRule.Damage(100f, 100f, 0.15f), 0.001f);
            Assert.IsFalse(FallDamageRule.Capped(100f, 100f, 0.15f));
        }

        [Test]
        public void LethalFallLeavesOneHealth()
        {
            float damage = FallDamageRule.Damage(10f, 100f, 0.15f);
            Assert.AreEqual(9f, damage, 0.001f);
            Assert.AreEqual(1f, 10f - damage, 0.001f);
            Assert.IsTrue(FallDamageRule.Capped(10f, 100f, 0.15f));
            Assert.IsFalse(FallDamageRule.Capped(16f, 100f, 0.15f), "16 - 15 = 1: 딱 1이 남으면 다 깎는다");
            Assert.AreEqual(15f, FallDamageRule.Damage(16f, 100f, 0.15f), 0.001f);
        }

        [Test]
        public void AtOneHealthNoDamage()
        {
            Assert.AreEqual(0f, FallDamageRule.Damage(1f, 100f, 0.15f));
            Assert.AreEqual(0f, FallDamageRule.Damage(0.4f, 100f, 0.15f));
            Assert.IsTrue(FallDamageRule.Capped(1f, 100f, 0.15f));
        }

        [Test]
        public void BadInputsTakeNothing()
        {
            Assert.AreEqual(0f, FallDamageRule.Damage(0f, 100f, 0.15f));
            Assert.AreEqual(0f, FallDamageRule.Damage(50f, 0f, 0.15f));
            Assert.AreEqual(0f, FallDamageRule.Damage(50f, 100f, 0f), "깃털 부적으로 0%");
            Assert.AreEqual(0f, FallDamageRule.Damage(float.NaN, 100f, 0.15f));
            Assert.IsFalse(FallDamageRule.Capped(50f, 100f, 0f));
        }
    }
}
