using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 도시 메뉴 한마디·수정 탑 금 (CityTalkRule)
    public class CityTalkRuleTests
    {
        [Test]
        public void FirstVisit_ThreeLinesInOrder_FromTeacher()
        {
            Assert.AreEqual(3, CityTalkRule.FirstVisitLines.Length);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(CityTalkRule.FirstVisitLines[i], CityTalkRule.Line(CityArrival.FirstVisit, i));
            Assert.AreEqual(CityTalkRule.Teacher, CityTalkRule.Speaker(CityArrival.FirstVisit));
            Assert.AreEqual(CityTalkRule.Keeper, CityTalkRule.Speaker(CityArrival.AfterRun));
        }

        [Test]
        public void Line_AnySeedGivesALine()
        {
            foreach (CityArrival arrival in System.Enum.GetValues(typeof(CityArrival)))
                foreach (int seed in new[] { 0, 1, 7, -3, int.MaxValue, int.MinValue + 1 })
                    Assert.IsFalse(string.IsNullOrEmpty(CityTalkRule.Line(arrival, seed)));
        }

        [Test]
        public void Cracks_FillWithTowerLevels()
        {
            Assert.AreEqual(30, CityTalkRule.Cracks(0));
            Assert.AreEqual(23, CityTalkRule.Cracks(7));
            Assert.AreEqual(0, CityTalkRule.Cracks(30));
            Assert.AreEqual(0, CityTalkRule.Cracks(35));
            Assert.AreEqual(30, CityTalkRule.Cracks(-1));
        }
    }
}
