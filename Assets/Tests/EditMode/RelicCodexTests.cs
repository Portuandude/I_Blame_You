using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests.EditMode
{
    public class RelicCodexTests
    {
        [Test]
        public void With_AddsBit_HasReadsIt()
        {
            int mask = RelicCodex.With(0, 3);
            mask = RelicCodex.With(mask, 17);
            Assert.IsTrue(RelicCodex.Has(mask, 3));
            Assert.IsTrue(RelicCodex.Has(mask, 17));
            Assert.IsFalse(RelicCodex.Has(mask, 0));
            Assert.AreEqual(mask, RelicCodex.With(mask, 3), "이미 있으면 그대로");
        }

        [Test]
        public void OutOfRange_Ignored()
        {
            Assert.AreEqual(5, RelicCodex.With(5, -1));
            Assert.AreEqual(5, RelicCodex.With(5, 31));
            Assert.IsFalse(RelicCodex.Has(-1, 31));
            Assert.IsFalse(RelicCodex.Has(-1, -1));
        }

        [Test]
        public void Count_And_Summary()
        {
            int mask = RelicCodex.With(RelicCodex.With(RelicCodex.With(0, 0), 5), 20);
            Assert.AreEqual(3, RelicCodex.Count(mask, 21));
            Assert.AreEqual(2, RelicCodex.Count(mask, 18), "전체 수 밖의 번호는 세지 않는다");
            Assert.AreEqual("유물 도감 2/18", RelicCodex.Summary(mask, 18));
            Assert.AreEqual("유물 도감 0/18", RelicCodex.Summary(0, 18));
        }
    }
}
