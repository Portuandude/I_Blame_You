using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests.EditMode
{
    public class BlameVerdictTests
    {
        [Test]
        public void MoreWasted_GetsBlamed()
        {
            Assert.AreEqual(BlameVerdict.Who.B, BlameVerdict.Judge(12.9f, 44.2f));
            Assert.AreEqual("판정: B 탓! (A보다 마나 31 더 날림)", BlameVerdict.Describe(12.9f, 44.2f));
            Assert.AreEqual("판정: A 탓! (B보다 마나 30 더 날림)", BlameVerdict.Describe(30f, 0f));
        }

        [Test]
        public void SmallGap_IsEven()
        {
            Assert.AreEqual(BlameVerdict.Who.Even, BlameVerdict.Judge(10f, 15f));   // 차이 5
            Assert.AreEqual(BlameVerdict.Who.Even, BlameVerdict.Judge(100f, 110f));  // 큰 쪽의 10% 이하
            Assert.AreEqual(BlameVerdict.Who.B, BlameVerdict.Judge(100f, 112f));
            Assert.AreEqual(BlameVerdict.Who.A, BlameVerdict.Judge(6f, 0f));
            StringAssert.StartsWith("판정: 반반", BlameVerdict.Describe(3f, 0f));
        }

        [Test]
        public void NothingWasted_IsNobody()
        {
            Assert.AreEqual(BlameVerdict.Who.Nobody, BlameVerdict.Judge(0f, 0.4f));
            Assert.AreEqual(BlameVerdict.Who.Nobody, BlameVerdict.Judge(-5f, 0f));
            Assert.AreEqual("판정: 아무도 마나를 날리지 않았다!", BlameVerdict.Describe(0f, 0f));
        }

        [Test]
        public void Recent_KeepsNewestTenAndCounts()
        {
            string list = "";
            for (int i = 0; i < 12; i++) list = BlameVerdict.AddRecent(list, i % 2 == 0 ? BlameVerdict.Who.A : BlameVerdict.Who.B);
            Assert.AreEqual("BABABABABA", list);
            Assert.AreEqual("최근 10판 탓: A 5 · B 5", BlameVerdict.RecentSummary(list));
            list = BlameVerdict.AddRecent(list, BlameVerdict.Who.Even);
            list = BlameVerdict.AddRecent(list, BlameVerdict.Who.Nobody);
            Assert.AreEqual("NEBABABABA", list);
            Assert.AreEqual("최근 10판 탓: A 4 · B 4 · 반반 1 · 탓 없음 1", BlameVerdict.RecentSummary(list));
        }

        [Test]
        public void Title_ByGap()
        {
            Assert.AreEqual("", BlameVerdict.RecentTitle(""));
            Assert.AreEqual("", BlameVerdict.RecentTitle("xyz"));
            Assert.AreEqual("환상의 짝꿍", BlameVerdict.RecentTitle("ABEN"));
            Assert.AreEqual("환상의 짝꿍", BlameVerdict.RecentTitle("EN"));
            Assert.AreEqual("B: 마나 흘리개", BlameVerdict.RecentTitle("BAB"));
            Assert.AreEqual("A: 마나 먹는 하마", BlameVerdict.RecentTitle("AAAB"));
            Assert.AreEqual("B: 마나 블랙홀", BlameVerdict.RecentTitle("BBBBNE"));
            Assert.AreEqual("A: 전설의 탓받이", BlameVerdict.RecentTitle("AAAAAAAAAA"));
            // 최근 10개만 센다: 11번째 B는 무시.
            Assert.AreEqual("A: 전설의 탓받이", BlameVerdict.RecentTitle("AAAAAAAAAAB"));
        }

        [Test]
        public void Recent_IgnoresBrokenAndEmpty()
        {
            Assert.AreEqual("", BlameVerdict.RecentSummary(null));
            Assert.AreEqual("", BlameVerdict.RecentSummary("xyz"));
            Assert.AreEqual("AB", BlameVerdict.AddRecent("x;B", BlameVerdict.Who.A));
            Assert.AreEqual("최근 2판 탓: A 1 · B 1", BlameVerdict.RecentSummary("A?B"));
        }
    }
}
