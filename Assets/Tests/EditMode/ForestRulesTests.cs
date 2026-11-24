using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 스테이지 2 숲 규칙: 그물 속박 (NetSnareRules), 코볼트 족장 무리 (KoboldChiefRules).
    public class ForestRulesTests
    {
        [Test]
        public void NetLastsFiveSecondsAndEachPressCutsHalf()
        {
            Assert.AreEqual(5f, NetSnareRules.Remaining(0f, 0), 1e-4);
            Assert.AreEqual(3f, NetSnareRules.Remaining(0f, 4), 1e-4);
            Assert.AreEqual(0f, NetSnareRules.Remaining(0f, 10), 1e-4);
            Assert.AreEqual(0f, NetSnareRules.Remaining(0f, 50), 1e-4);
            Assert.AreEqual(1f, NetSnareRules.Remaining(2f, 4), 1e-4);
            Assert.IsTrue(NetSnareRules.Free(5f, 0));
            Assert.IsFalse(NetSnareRules.Free(4.9f, 0));
            Assert.AreEqual(1f, NetSnareRules.Fraction(0f, 0), 1e-4);
            Assert.AreEqual(5f, NetSnareRules.Remaining(-1f, -3), 1e-4, "음수 입력은 0으로");
        }

        [Test]
        public void PackEnrageSpeedsUpButKeepsAFloor()
        {
            Assert.AreEqual(1f, KoboldChiefRules.CooldownFor(0), 1e-4);
            Assert.Less(KoboldChiefRules.CooldownFor(2), KoboldChiefRules.CooldownFor(1));
            Assert.AreEqual(KoboldChiefRules.MinCooldown, KoboldChiefRules.CooldownFor(20), 1e-4);
            Assert.Less(KoboldChiefRules.NetIntervalFor(0.3f), KoboldChiefRules.NetIntervalFor(0.8f));
            Assert.IsFalse(KoboldChiefRules.InNetRange(1f));
            Assert.IsTrue(KoboldChiefRules.InNetRange(5f));
            Assert.IsFalse(KoboldChiefRules.InNetRange(20f));
        }
    }
}
