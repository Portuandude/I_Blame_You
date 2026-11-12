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
        public void Defeat_ShowsRoomDistanceSideRoomFarthestAndTime()
        {
            Assert.AreEqual("쓰러진 곳: 시작 방에서 3칸 (곁방) · 가장 멀리 4칸 · 버틴 시간 5분 12초",
                ClearPace.DescribeDefeat(3, 4, true, 312.9f));
            // 가장 먼 곳이 지금 방이면 "가장 멀리"는 뺀다. 시간이 0이면 시간도 뺀다.
            Assert.AreEqual("쓰러진 곳: 시작 방에서 5칸", ClearPace.DescribeDefeat(5, 5, false, 0f));
            Assert.AreEqual("쓰러진 곳: 시작 방 · 가장 멀리 2칸 · 버틴 시간 1분 00초", ClearPace.DescribeDefeat(0, 2, false, 60f));
            // 방 격자가 아니면(-1) 방 부분 없이 시간만, 둘 다 없으면 빈 문자열. 진행도(%)는 어디에도 없다.
            Assert.AreEqual("버틴 시간 1분 00초", ClearPace.DescribeDefeat(-1, -1, false, 60f));
            Assert.AreEqual("", ClearPace.DescribeDefeat(-1, -1, false, 0f));
            StringAssert.DoesNotContain("%", ClearPace.DescribeDefeat(3, 4, true, 312.9f));
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
        public void RecentDefeats_KeepNewestFiveAndAverageDistance()
        {
            string list = "";
            for (int i = 1; i <= 7; i++) list = ClearPace.AddRecentDefeat(list, i);
            // 최신 5개: 7, 6, 5, 4, 3 → 평균 5칸
            Assert.AreEqual("7;6;5;4;3", list);
            Assert.AreEqual("최근 쓰러진 곳 5판 평균 시작 방에서 5칸까지", ClearPace.RecentDefeatSummary(list));
        }

        [Test]
        public void DefeatCompare_AgainstRecentAverage()
        {
            Assert.AreEqual(-1, ClearPace.RecentDefeatAverage(""));
            Assert.AreEqual(-1, ClearPace.RecentDefeatAverage("broken"));
            Assert.AreEqual(4, ClearPace.RecentDefeatAverage("3;4"));        // 3.5 → 반올림 4
            Assert.AreEqual("", ClearPace.CompareDefeat(5, -1));
            Assert.AreEqual("평소보다 2칸 더 갔다 (최근 평균 3칸)", ClearPace.CompareDefeat(5, 3));
            Assert.AreEqual("평소보다 1칸 덜 갔다 (최근 평균 4칸)", ClearPace.CompareDefeat(3, 4));
            Assert.AreEqual("평소만큼 갔다 (최근 평균 3칸)", ClearPace.CompareDefeat(3, 3));
            Assert.AreEqual(2, ClearPace.DefeatGap(5, 3));
            Assert.AreEqual(-1, ClearPace.DefeatGap(3, 4));
            Assert.AreEqual(0, ClearPace.DefeatGap(-4, 0));
        }

        [Test]
        public void RecentDefeats_ClampsAndIgnoresBroken()
        {
            Assert.AreEqual("", ClearPace.RecentDefeatSummary(""));
            Assert.AreEqual("", ClearPace.RecentDefeatSummary(null));
            // 거리는 0~99로 자르고, 예전 진행도 형식("30,5,19")·깨진 항목은 버린다.
            Assert.AreEqual("0", ClearPace.AddRecentDefeat(null, -3));
            Assert.AreEqual("99;4", ClearPace.AddRecentDefeat("bad;30,5,19;4;150", 120));
            Assert.AreEqual("최근 쓰러진 곳 2판 평균 시작 방에서 3칸까지", ClearPace.RecentDefeatSummary("2;x;40,0,0;4"));
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
