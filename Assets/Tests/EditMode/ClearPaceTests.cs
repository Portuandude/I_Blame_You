using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests.EditMode
{
    public class ClearPaceTests
    {
        [Test]
        public void FasterThanTarget_IsGreenAndSaysFast()
        {
            Assert.IsTrue(ClearPace.IsFast(817f, 900f));
            Assert.AreEqual("목표 15분 00초보다 1분 23초 빠름 · 방 22개 (주 경로 19 · 곁방 3)",
                ClearPace.Describe(817.6f, 900f, 19, 3));
        }

        [Test]
        public void SlowerThanTarget_IsOrangeAndSaysSlow()
        {
            Assert.IsFalse(ClearPace.IsFast(965f, 900f));
            Assert.AreEqual("목표 15분 00초보다 1분 05초 느림 · 방 20개 (주 경로 18 · 곁방 2)",
                ClearPace.Describe(965f, 900f, 18, 2));
        }

        [Test]
        public void SameSecond_CountsAsOnTime()
        {
            Assert.IsTrue(ClearPace.IsFast(900f, 900f));
            StringAssert.StartsWith("목표 15분 00초 딱 맞춤", ClearPace.Describe(900.4f, 900f, 19, 3));
        }

        [Test]
        public void Defeat_ShowsPathStepSideRoomProgressAndTime()
        {
            Assert.AreEqual("쓰러진 곳: 주 경로 7/19번째 방 (곁방) · 진행도 33% · 버틴 시간 5분 12초",
                ClearPace.DescribeDefeat(7, 19, true, 0.3333f, 312.9f));
            Assert.AreEqual("쓰러진 곳: 주 경로 19/19번째 방 · 진행도 100%",
                ClearPace.DescribeDefeat(25, 19, false, 1.2f, 0f));
            Assert.AreEqual("쓰러진 곳: 진행도 40% · 버틴 시간 1분 00초",
                ClearPace.DescribeDefeat(0, 0, false, 0.4f, 60f));
        }

        [Test]
        public void Recent_KeepsNewestFiveAndAverages()
        {
            string list = "";
            for (int i = 1; i <= 7; i++) list = ClearPace.AddRecent(list, 800f + i * 10f, 900f, 20 + i);
            // 최신 5개: 870, 860, 850, 840, 830 → 평균 850초 = 14분 10초, 방 27..23 → 평균 25
            StringAssert.StartsWith("870,900,27;860,900,26", list);
            Assert.AreEqual(5, list.Split(';').Length);
            Assert.AreEqual("최근 5판 평균 14분 10초 (목표 15분 00초 · 방 평균 25개)", ClearPace.RecentSummary(list));
        }

        [Test]
        public void Recent_IgnoresBrokenEntriesAndEmpty()
        {
            Assert.AreEqual("", ClearPace.RecentSummary(""));
            Assert.AreEqual("", ClearPace.RecentSummary(null));
            Assert.AreEqual("최근 1판 평균 10분 00초", ClearPace.RecentSummary("x,1;600,0,0;bad"));
            Assert.AreEqual("abc", ClearPace.AddRecent("abc", 0f, 900f, 10));
            Assert.AreEqual("600,900,19", ClearPace.AddRecent("broken", 600.9f, 900f, 19));
        }

        [Test]
        public void RecentDefeats_KeepNewestFiveAndAverageProgress()
        {
            string list = "";
            for (int i = 1; i <= 7; i++) list = ClearPace.AddRecentDefeat(list, i * 0.1f, i * 2, 19);
            // 최신 5개: 70%·14, 60%·12, 50%·10, 40%·8, 30%·6 → 평균 50%, 10번째
            StringAssert.StartsWith("70,14,19;60,12,19", list);
            Assert.AreEqual(5, list.Split(';').Length);
            Assert.AreEqual("최근 쓰러진 곳 5판 평균 진행도 50% (주 경로 평균 10번째 방)", ClearPace.RecentDefeatSummary(list));
        }

        [Test]
        public void DefeatCompare_AgainstRecentAverage()
        {
            Assert.AreEqual(-1, ClearPace.RecentDefeatAverage(""));
            Assert.AreEqual(-1, ClearPace.RecentDefeatAverage("broken"));
            Assert.AreEqual(35, ClearPace.RecentDefeatAverage("30,5,19;40,8,19"));
            Assert.AreEqual("", ClearPace.CompareDefeat(0.5f, -1));
            Assert.AreEqual("평소보다 12%p 더 갔다 (최근 평균 30%)", ClearPace.CompareDefeat(0.42f, 30));
            Assert.AreEqual("평소보다 5%p 덜 갔다 (최근 평균 40%)", ClearPace.CompareDefeat(0.35f, 40));
            Assert.AreEqual("평소만큼 갔다 (최근 평균 33%)", ClearPace.CompareDefeat(0.334f, 33));
            Assert.AreEqual(12, ClearPace.DefeatGap(0.42f, 30));
            Assert.AreEqual(-5, ClearPace.DefeatGap(0.35f, 40));
            Assert.AreEqual(0, ClearPace.DefeatGap(1.5f, 100));
        }

        [Test]
        public void RecentDefeats_ClampsAndIgnoresBroken()
        {
            Assert.AreEqual("", ClearPace.RecentDefeatSummary(""));
            Assert.AreEqual("", ClearPace.RecentDefeatSummary(null));
            // 진행도 0%도 기록한다. 순번은 방 수 안으로, 방 수가 없으면 0.
            Assert.AreEqual("0,0,19", ClearPace.AddRecentDefeat(null, -0.5f, 0, 19));
            Assert.AreEqual("100,19,19", ClearPace.AddRecentDefeat("bad;150,1,19;5,9,3", 1.4f, 25, 19));
            Assert.AreEqual("40,0,0", ClearPace.AddRecentDefeat("", 0.4f, 7, 0));
            // 순번 0인 기록은 방 평균에서 빠지고, 모두 0이면 괄호를 뺀다.
            Assert.AreEqual("최근 쓰러진 곳 2판 평균 진행도 25%", ClearPace.RecentDefeatSummary("40,0,0;10,0,0"));
            Assert.AreEqual("최근 쓰러진 곳 2판 평균 진행도 35% (주 경로 평균 7번째 방)", ClearPace.RecentDefeatSummary("30,7,19;40,0,0;x"));
        }

        [Test]
        public void NoRooms_OmitsRoomPart_NoTarget_IsEmpty()
        {
            Assert.AreEqual("목표 15분 00초보다 0분 10초 느림", ClearPace.Describe(910f, 900f, 0, 0));
            Assert.AreEqual("", ClearPace.Describe(910f, 0f, 19, 3));
            Assert.IsFalse(ClearPace.IsFast(10f, 0f));
        }
    }
}
