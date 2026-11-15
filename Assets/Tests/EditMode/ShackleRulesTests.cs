using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 얼음 족쇄 둔화: 일반 40%, 정예·보스 20% (ShackleRules)
    public class ShackleRulesTests
    {
        [Test]
        public void Slow_NormalIs40_StrongIs20()
        {
            Assert.AreEqual(0.4f, ShackleRules.Slow(false), 1e-5f);
            Assert.AreEqual(0.2f, ShackleRules.Slow(true), 1e-5f);
            Assert.Less(ShackleRules.Slow(true), ShackleRules.Slow(false));
        }
    }
}
