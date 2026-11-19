using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 프리즘 젤리 (두 번째 중간보스): 색 바꾸기와 같은 색 피해 50% (PrismJellyRules)
    public class PrismJellyRulesTests
    {
        [Test]
        public void NextColor_IsAlwaysADifferentOfFourColors_AndAllAppear()
        {
            foreach (var current in PrismJellyRules.Colors)
            {
                var seen = new HashSet<Element>();
                for (int i = 0; i < 100; i++)
                {
                    var next = PrismJellyRules.NextColor(current, i / 100.0);
                    Assert.AreNotEqual(current, next);
                    Assert.Contains(next, PrismJellyRules.Colors);
                    seen.Add(next);
                }
                Assert.AreEqual(3, seen.Count, $"{current}에서 나머지 세 색이 모두 나옴");
            }
            Assert.AreNotEqual(Element.Neutral, PrismJellyRules.NextColor(Element.Neutral, 0.99));
            Assert.AreNotEqual(Element.Neutral, PrismJellyRules.FirstColor(0.999999));
            Assert.AreNotEqual(Element.Neutral, PrismJellyRules.NextColor(Element.Fire, 1.0)); // 범위 밖 난수도 안전
        }

        [Test]
        public void SameColorIsHalved_CounterIsOneAndHalf()
        {
            Assert.AreEqual(0.5f, PrismJellyRules.TotalMultiplier(Element.Fire, Element.Fire), 1e-5f);
            Assert.AreEqual(0.5f, PrismJellyRules.TotalMultiplier(Element.Earth, Element.Earth), 1e-5f);
            Assert.AreEqual(1.5f, PrismJellyRules.TotalMultiplier(Element.Water, Element.Fire), 1e-5f);  // 물 > 불
            Assert.AreEqual(1.5f, PrismJellyRules.TotalMultiplier(Element.Earth, Element.Water), 1e-5f); // 땅 > 물
            Assert.AreEqual(1.5f, PrismJellyRules.TotalMultiplier(Element.Wind, Element.Earth), 1e-5f);  // 바람 > 땅
            Assert.AreEqual(1.5f, PrismJellyRules.TotalMultiplier(Element.Neutral, Element.Wind), 1e-5f); // 무 > 바람
            Assert.AreEqual(1f, PrismJellyRules.ColorMultiplier(Element.Water, Element.Fire), 1e-5f);
            // 네 색 모두 약점 속성이 하나 이상 있다 (P2가 늘 바꿔 그릴 답이 있다)
            foreach (var color in PrismJellyRules.Colors)
            {
                bool hasWeakness = false;
                foreach (Element attack in System.Enum.GetValues(typeof(Element)))
                    if (PrismJellyRules.TotalMultiplier(attack, color) > 1f) hasWeakness = true;
                Assert.IsTrue(hasWeakness, $"{color}");
            }
        }

        [Test]
        public void TimingIsDraftValues()
        {
            Assert.AreEqual(8f, PrismJellyRules.ShiftInterval);
            Assert.Less(PrismJellyRules.WarnSeconds, PrismJellyRules.ShiftInterval);
        }

        [Test]
        public void Patterns_AlternateAndBeamOpensAtHalfHealth()
        {
            Assert.AreEqual(PrismJellyRules.Pattern.Droplets, PrismJellyRules.NextPattern(PrismJellyRules.Pattern.Slam));
            Assert.AreEqual(PrismJellyRules.Pattern.Slam, PrismJellyRules.NextPattern(PrismJellyRules.Pattern.Droplets));
            for (int i = 0; i < 100; i++)
            {
                int n = PrismJellyRules.DropletCount(i / 100.0);
                Assert.That(n, Is.InRange(4, 6));
            }
            Assert.AreEqual(4, PrismJellyRules.DropletCount(0));
            Assert.AreEqual(6, PrismJellyRules.DropletCount(0.999));
            Assert.IsFalse(PrismJellyRules.BeamUnlocked(201f, 400f));
            Assert.IsTrue(PrismJellyRules.BeamUnlocked(200f, 400f));
            Assert.IsFalse(PrismJellyRules.BeamUnlocked(0f, 0f));
            Assert.Less(PrismJellyRules.BeamWarn, PrismJellyRules.BeamInterval);
            Assert.Less(PrismJellyRules.ShockwaveHeight, 1f, "점프로 넘을 수 있는 높이");
        }
    }
}
