using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 비율식 데미지 공식과 속성 상성 (기획서 5.2, 2026-10-16 상성 확정안).
    public class DamageFormulaTests
    {
        [TestCase(100f, 0f, 100f)]
        [TestCase(100f, 100f, 50f)]
        [TestCase(30f, 50f, 20f)]
        [TestCase(100f, -50f, 200f)]
        [TestCase(100f, -80f, 200f)] // -50 아래 방어는 -50으로 본다 (피해 최대 2배)
        [TestCase(0f, 30f, 0f)]
        public void RatioFormula(float attack, float defense, float expected)
        {
            Assert.AreEqual(expected, DamageFormula.Ratio(attack, defense), 0.001f);
        }

        [Test]
        public void MoreDefenseNeverIncreasesDamage()
        {
            float previous = float.MaxValue;
            for (float defense = -60f; defense <= 300f; defense += 5f)
            {
                float damage = DamageFormula.Ratio(80f, defense);
                Assert.LessOrEqual(damage, previous + 0.0001f, $"방어 {defense}");
                previous = damage;
            }
        }

        // 물 > 불 > 무 > 바람 > 땅 > 물: 한 바퀴 순환이고, 각 속성은 정확히 하나를 이기고 하나에 진다.
        [Test]
        public void AffinityIsOneCycle()
        {
            Assert.AreEqual(Element.Fire, ElementAffinity.Beats(Element.Water));
            Assert.AreEqual(Element.Neutral, ElementAffinity.Beats(Element.Fire));
            Assert.AreEqual(Element.Wind, ElementAffinity.Beats(Element.Neutral));
            Assert.AreEqual(Element.Earth, ElementAffinity.Beats(Element.Wind));
            Assert.AreEqual(Element.Water, ElementAffinity.Beats(Element.Earth));

            var all = (Element[])System.Enum.GetValues(typeof(Element));
            foreach (var attack in all)
            {
                int wins = 0, losses = 0;
                foreach (var defender in all)
                {
                    float m = ElementAffinity.Multiplier(attack, defender);
                    if (m > 1f) wins++;
                    if (m < 1f) losses++;
                }

                Assert.AreEqual(1, wins, $"{attack} 이기는 상대 수");
                Assert.AreEqual(1, losses, $"{attack} 지는 상대 수");
            }
        }

        [Test]
        public void AffinityMultipliers()
        {
            Assert.AreEqual(ElementAffinity.Advantage, ElementAffinity.Multiplier(Element.Water, Element.Fire));
            Assert.AreEqual(ElementAffinity.Disadvantage, ElementAffinity.Multiplier(Element.Fire, Element.Water));
            Assert.AreEqual(1f, ElementAffinity.Multiplier(Element.Fire, Element.Fire));
            Assert.AreEqual(1f, ElementAffinity.Multiplier(Element.Fire, Element.Wind)); // 관계 없음
        }
    }
}
