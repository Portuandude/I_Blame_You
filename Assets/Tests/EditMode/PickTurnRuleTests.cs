using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 유물·장비 고르기 번갈아 (2026-11-24 사람 결정 E2)
    public class PickTurnRuleTests
    {
        [Test]
        public void P1PicksFirstThenTurnsAlternate()
        {
            Assert.IsTrue(PickTurnRule.IsP1Turn(0));
            Assert.IsFalse(PickTurnRule.IsP1Turn(1));
            Assert.IsTrue(PickTurnRule.IsP1Turn(2));
            Assert.IsFalse(PickTurnRule.IsP1Turn(7));
        }

        [Test]
        public void OnlyTheTurnHoldersInputCounts()
        {
            Assert.IsTrue(PickTurnRule.KeyboardAllowed(0, false));
            Assert.IsFalse(PickTurnRule.MouseAllowed(0, false));
            Assert.IsFalse(PickTurnRule.KeyboardAllowed(1, false));
            Assert.IsTrue(PickTurnRule.MouseAllowed(1, false));
        }

        [Test]
        public void DevModeTakesBoth()
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(PickTurnRule.KeyboardAllowed(i, true));
                Assert.IsTrue(PickTurnRule.MouseAllowed(i, true));
                Assert.AreEqual("", PickTurnRule.Label(i, true));
            }
            StringAssert.Contains("아루", PickTurnRule.Label(0, false));
            StringAssert.Contains("벨", PickTurnRule.Label(1, false));
        }
    }
}
