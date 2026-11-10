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
            Assert.AreEqual("판정: B 탓! (A보다 마나 32 더 날림)", BlameVerdict.Describe(12.9f, 44.2f));
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
            Assert.AreEqual(BlameVerdict.Who.Nobody, BlameVerdict.Judge(0f, 0.9f));
            Assert.AreEqual(BlameVerdict.Who.Nobody, BlameVerdict.Judge(-5f, 0f));
            Assert.AreEqual("판정: 아무도 마나를 날리지 않았다!", BlameVerdict.Describe(0f, 0f));
        }
    }
}
