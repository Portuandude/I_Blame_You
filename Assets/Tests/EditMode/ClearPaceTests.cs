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
        public void NoRooms_OmitsRoomPart_NoTarget_IsEmpty()
        {
            Assert.AreEqual("목표 15분 00초보다 0분 10초 느림", ClearPace.Describe(910f, 900f, 0, 0));
            Assert.AreEqual("", ClearPace.Describe(910f, 0f, 19, 3));
            Assert.IsFalse(ClearPace.IsFast(10f, 0f));
        }
    }
}
