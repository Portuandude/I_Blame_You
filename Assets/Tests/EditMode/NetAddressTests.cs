using IBlameYou.Net;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 참가 주소 칸: "주소 복사"로 붙여 넣은 "주소:포트"도 받는다 (Net/Core/NetAddress)
    public class NetAddressTests
    {
        [Test]
        public void PlainAddressUsesPortField()
        {
            Assert.IsTrue(NetAddress.TryParse(" 192.168.0.12 ", 27777, out string host, out int port));
            Assert.AreEqual("192.168.0.12", host);
            Assert.AreEqual(27777, port);
        }

        [Test]
        public void AddressWithPortUsesThatPort()
        {
            Assert.IsTrue(NetAddress.TryParse("26.10.3.4:28000", 27777, out string host, out int port));
            Assert.AreEqual("26.10.3.4", host);
            Assert.AreEqual(28000, port);
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.IsFalse(NetAddress.TryParse("", 27777, out _, out _));
            Assert.IsFalse(NetAddress.TryParse("   ", 27777, out _, out _));
            Assert.IsFalse(NetAddress.TryParse(null, 27777, out _, out _));
            Assert.IsFalse(NetAddress.TryParse("1.2.3.4:abc", 27777, out _, out _));
            Assert.IsFalse(NetAddress.TryParse("1.2.3.4:70000", 27777, out _, out _));
            Assert.IsFalse(NetAddress.TryParse(":27777", 27777, out _, out _));
        }

        [Test]
        public void Ipv6IsLeftAlone()
        {
            Assert.IsTrue(NetAddress.TryParse("fe80::1", 27777, out string host, out int port));
            Assert.AreEqual("fe80::1", host);
            Assert.AreEqual(27777, port);
        }
    }
}
