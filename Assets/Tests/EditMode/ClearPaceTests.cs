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
        public void NoRooms_OmitsRoomPart_NoTarget_IsEmpty()
        {
            Assert.AreEqual("목표 15분 00초보다 0분 10초 느림", ClearPace.Describe(910f, 900f, 0, 0));
            Assert.AreEqual("", ClearPace.Describe(910f, 0f, 19, 3));
            Assert.IsFalse(ClearPace.IsFast(10f, 0f));
        }
    }
}
