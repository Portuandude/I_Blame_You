using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests.EditMode
{
    public class RunTallyTests
    {
        private static readonly string[] Names = { "불", "물", "바람", "땅", "무" };

        [Test]
        public void Favorite_PicksMostUsed_TieGoesFirst()
        {
            Assert.AreEqual("B 최애 마법: 바람(7회)", RunTally.Favorite("B", "마법", new[] { 3, 0, 7, 1, 0 }, Names));
            Assert.AreEqual("A 최애 방어막: 물(2회)", RunTally.Favorite("A", "방어막", new[] { 0, 2, 0, 2, 0 }, Names));
            Assert.AreEqual("", RunTally.Favorite("B", "마법", new int[5], Names));
            Assert.AreEqual("", RunTally.Favorite("B", "마법", null, Names));
        }

        [Test]
        public void Line_JoinsOrDropsEmpty()
        {
            Assert.AreEqual("B 최애 마법: 불(12회) · A 최애 방어막: 땅(4회)",
                RunTally.Line(new[] { 12, 0, 0, 0, 0 }, new[] { 0, 0, 0, 4, 1 }, Names));
            Assert.AreEqual("A 최애 방어막: 무(1회)", RunTally.Line(new int[5], new[] { 0, 0, 0, 0, 1 }, Names));
            Assert.AreEqual("B 최애 마법: 물(1회)", RunTally.Line(new[] { 0, 1, 0, 0, 0 }, new int[5], Names));
            Assert.AreEqual("", RunTally.Line(new int[5], new int[5], Names));
        }
    }
}
