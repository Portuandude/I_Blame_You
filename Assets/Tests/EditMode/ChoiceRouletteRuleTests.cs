using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // A·B 선택지 + 룰렛 (2026-11-24 사람 결정, OpenDecisions R1~R4)
    public class ChoiceRouletteRuleTests
    {
        [Test]
        public void SameChoiceNeedsNoRoulette()
        {
            Assert.AreEqual(2, ChoiceRouletteRule.Resolve(2, 2, 0.9, out bool spun));
            Assert.IsFalse(spun);
        }

        [Test]
        public void DifferentChoicesSpinFiftyFifty()
        {
            Assert.AreEqual(0, ChoiceRouletteRule.Resolve(0, 1, 0.1, out bool spun));
            Assert.IsTrue(spun);
            Assert.AreEqual(1, ChoiceRouletteRule.Resolve(0, 1, 0.5, out _));
            Assert.AreEqual(1, ChoiceRouletteRule.Resolve(0, 1, 0.99, out _));
            int a = 0;
            var rng = new System.Random(5);
            for (int i = 0; i < 10000; i++) if (ChoiceRouletteRule.Resolve(0, 1, rng.NextDouble(), out _) == 0) a++;
            Assert.That(a, Is.InRange(4700, 5300), "50:50");
        }

        [Test]
        public void AlignmentFollowsEachOwnChoice()
        {
            var deltas = new[] { 2, 0, -3 };
            Assert.AreEqual(2, ChoiceRouletteRule.AlignmentDelta(deltas, 0));
            Assert.AreEqual(-3, ChoiceRouletteRule.AlignmentDelta(deltas, 2));
            Assert.AreEqual(0, ChoiceRouletteRule.AlignmentDelta(deltas, 5));
            Assert.AreEqual(0, ChoiceRouletteRule.AlignmentDelta(null, 0));
        }

        [Test]
        public void PointerFlickersThenLandsOnResult()
        {
            Assert.AreEqual(1, ChoiceRouletteRule.Pointer(ChoiceRouletteRule.SpinSeconds, true));
            Assert.AreEqual(0, ChoiceRouletteRule.Pointer(ChoiceRouletteRule.SpinSeconds + 3f, false));
            Assert.AreEqual(1, ChoiceRouletteRule.Pointer(ChoiceRouletteRule.SpinSeconds - 0.001f, true), "끝 직전에도 결과 쪽");
            int changes = 0, last = ChoiceRouletteRule.Pointer(0f, true);
            for (float t = 0f; t < ChoiceRouletteRule.SpinSeconds; t += 0.01f)
            {
                int p = ChoiceRouletteRule.Pointer(t, true);
                if (p != last) changes++;
                last = p;
            }
            Assert.That(changes, Is.InRange(10, 14), "여러 번 번갈아 깜빡인다");
        }
    }
}
