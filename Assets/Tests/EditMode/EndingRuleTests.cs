using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 멀티 엔딩 판정 (Docs/DesignDecisionsTodo.md B1 엔딩 표, OpenDecisions E1~E8)
    public class EndingRuleTests
    {
        private const int Good = HiddenStatsRule.StartAlignment; // 30 = 선
        private const int Evil = 0;                              // 딱 0도 악 (E1)

        [Test]
        public void PairHasNoNeutral()
        {
            Assert.AreEqual(AlignmentPair.BothGood, EndingRule.Pair(1, 30));
            Assert.AreEqual(AlignmentPair.Mixed, EndingRule.Pair(1, 0));
            Assert.AreEqual(AlignmentPair.Mixed, EndingRule.Pair(-5, 99));
            Assert.AreEqual(AlignmentPair.BothEvil, EndingRule.Pair(0, -10));
        }

        [Test]
        public void LowCausalityWinTable()
        {
            Assert.AreEqual(EndingId.Ga1, EndingRule.Final(0, true, Good, Good, true));
            Assert.AreEqual(EndingId.Ga2, EndingRule.Final(20, true, Good, Evil, true));
            Assert.AreEqual(EndingId.Ga3, EndingRule.Final(24, true, Evil, Evil, true));
            Assert.AreEqual(EndingId.Ga4, EndingRule.Final(0, true, Good, Good, false), "NPC 사망이면 성향 상관없이 가4");
            Assert.AreEqual(EndingId.Ga4, EndingRule.Final(0, true, Evil, Evil, false));
        }

        [Test]
        public void LowCausalityLoseTable()
        {
            Assert.AreEqual(EndingId.Na1, EndingRule.Final(5, false, Good, Good, false));
            Assert.AreEqual(EndingId.Na2, EndingRule.Final(5, false, Evil, Good, false));
            Assert.AreEqual(EndingId.Na3, EndingRule.Final(5, false, Evil, Evil, false));
            Assert.AreEqual(EndingId.Na4, EndingRule.Final(5, false, Good, Good, true));
            Assert.AreEqual(EndingId.Na5, EndingRule.Final(5, false, Good, Evil, true));
            Assert.AreEqual(EndingId.Na6, EndingRule.Final(5, false, Evil, Evil, true));
        }

        [Test]
        public void NormalOrHigherWinTable()
        {
            foreach (int causality in new[] { 25, 74, 75, 100 })
            {
                Assert.AreEqual(EndingId.Da1, EndingRule.Final(causality, true, Good, Good, false));
                Assert.AreEqual(EndingId.Da2, EndingRule.Final(causality, true, Good, Evil, false));
                Assert.AreEqual(EndingId.Da3, EndingRule.Final(causality, true, Evil, Evil, false));
                Assert.AreEqual(EndingId.Da4, EndingRule.Final(causality, true, Good, Good, true));
                Assert.AreEqual(EndingId.Da5, EndingRule.Final(causality, true, Evil, Good, true));
                Assert.AreEqual(EndingId.Da6, EndingRule.Final(causality, true, Evil, Evil, true));
            }
        }

        [Test]
        public void NormalOrHigherLoseRetriesOnceThenVanishes()
        {
            Assert.AreEqual(EndingId.Ra1, EndingRule.Final(50, false, Evil, Evil, true), "성향 상관없이 NPC 생존 = 재도전");
            Assert.AreEqual(EndingId.Ra2, EndingRule.Final(50, false, Good, Good, false));
            Assert.AreEqual(EndingId.Ra2, EndingRule.Final(50, false, Good, Good, true, retried: true), "재도전에서 또 지면 라2 (E5)");
            Assert.IsTrue(EndingRule.IsRetry(EndingId.Ra1));
            Assert.IsFalse(EndingRule.IsRetry(EndingId.Ra2));
            Assert.IsFalse(EndingRule.KeyNpcAliveAfter(EndingId.Ra1, true), "라1은 NPC가 희생한다");
            Assert.IsTrue(EndingRule.KeyNpcAliveAfter(EndingId.Da4, true));
        }

        [Test]
        public void OverflowJudgement()
        {
            Assert.IsFalse(EndingRule.ReachedThreshold(99));
            Assert.IsTrue(EndingRule.ReachedThreshold(100));
            Assert.AreEqual(OverflowOutcome.Vanish, EndingRule.Overflow(Evil, Evil, true));
            Assert.AreEqual(OverflowOutcome.ExtraRevive, EndingRule.Overflow(Good, Evil, true));
            Assert.AreEqual(OverflowOutcome.NpcSacrificeReset, EndingRule.Overflow(Good, Good, true));
            Assert.AreEqual(OverflowOutcome.ExtraRevive, EndingRule.Overflow(Good, Good, false));
        }

        [Test]
        public void SixteenDistinctFinalEndingsWithCodes()
        {
            // 가 4 + 나 5(나4 = 나5) + 다 6 + 라2 = 16. 라1은 재도전이라 빠짐 (표의 "약 18개"는 판 도중 마1 소멸 등을 더한 어림)
            Assert.AreEqual(16, EndingRule.DistinctEndingCount);
            Assert.AreEqual("가1", EndingRule.Code(EndingId.Ga1));
            Assert.AreEqual("나5", EndingRule.Code(EndingId.Na5));
            Assert.AreEqual("다6", EndingRule.Code(EndingId.Da6));
            Assert.AreEqual("라2", EndingRule.Code(EndingId.Ra2));
            Assert.AreEqual(EndingRule.Summary(EndingId.Na4), EndingRule.Summary(EndingId.Na5));
            Assert.AreEqual("73번째 악마", EndingRule.Summary(EndingId.Na3));
        }
    }
}
