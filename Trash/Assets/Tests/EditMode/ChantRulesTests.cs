using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 순풍 동안 짧아지는 영창 문장 (ChantRules)
    public class ChantRulesTests
    {
        [Test]
        public void Short_KeepsCallBeforeComma()
        {
            Assert.AreEqual("불꽃이여!", ChantRules.Short("불꽃이여, 작렬하라!"));
            Assert.AreEqual("대지여!", ChantRules.Short("대지여, 창이 되어 꿰뚫어라!"));
        }

        [Test]
        public void Short_NoCommaOrEmpty_Unchanged()
        {
            Assert.AreEqual("가라!", ChantRules.Short("가라!"));
            Assert.AreEqual("", ChantRules.Short(""));
            Assert.IsNull(ChantRules.Short(null));
            Assert.AreEqual(",앞이 없음", ChantRules.Short(",앞이 없음"));
        }

        [Test]
        public void For_OnlyWhenTailwind()
        {
            Assert.AreEqual("물이여, 꿰뚫어라!", ChantRules.For("물이여, 꿰뚫어라!", false));
            Assert.AreEqual("물이여!", ChantRules.For("물이여, 꿰뚫어라!", true));
        }
    }
}
