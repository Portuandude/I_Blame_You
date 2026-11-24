using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 숨은 능력치 선악도·인과율 (2026-11-24 사람 결정, Docs/OpenDecisions.md 4장)
    public class HiddenStatsRuleTests
    {
        [Test]
        public void CausalityRisesFivePerRevivalUpToHundred()
        {
            Assert.AreEqual(0, HiddenStatsRule.Causality(0));
            Assert.AreEqual(0, HiddenStatsRule.Causality(-3));
            Assert.AreEqual(5, HiddenStatsRule.Causality(1));
            Assert.AreEqual(25, HiddenStatsRule.Causality(5));
            Assert.AreEqual(75, HiddenStatsRule.Causality(15));
            Assert.AreEqual(100, HiddenStatsRule.Causality(20));
            Assert.AreEqual(100, HiddenStatsRule.Causality(999));
        }

        [Test]
        public void BandsMatchEndingTable()
        {
            Assert.AreEqual(CausalityBand.Low, HiddenStatsRule.Band(0));
            Assert.AreEqual(CausalityBand.Low, HiddenStatsRule.Band(24));
            Assert.AreEqual(CausalityBand.Normal, HiddenStatsRule.Band(25)); // 부활 5번에 보통
            Assert.AreEqual(CausalityBand.Normal, HiddenStatsRule.Band(74));
            Assert.AreEqual(CausalityBand.High, HiddenStatsRule.Band(75));   // 부활 15번에 높음
            Assert.AreEqual(CausalityBand.High, HiddenStatsRule.Band(99));
            Assert.AreEqual(CausalityBand.Overflow, HiddenStatsRule.Band(100)); // 임계치 = 최대치 100
        }

        [Test]
        public void GloomFollowsCausality()
        {
            Assert.AreEqual(0f, HiddenStatsRule.Gloom(0));
            Assert.AreEqual(0.5f, HiddenStatsRule.Gloom(50), 1e-6);
            Assert.AreEqual(1f, HiddenStatsRule.Gloom(100));
            Assert.AreEqual(1f, HiddenStatsRule.Gloom(150));
        }

        [Test]
        public void AlignmentStartsGoodAndNpcSeesTheSum()
        {
            Assert.AreEqual(30, HiddenStatsRule.StartAlignment);
            Assert.IsTrue(HiddenStatsRule.IsGood(HiddenStatsRule.StartAlignment));
            Assert.IsFalse(HiddenStatsRule.IsGood(0), "0 이하 = 악");
            Assert.IsFalse(HiddenStatsRule.IsGood(-12));
            Assert.AreEqual(10, HiddenStatsRule.NpcView(40, -30));
        }
    }
}
