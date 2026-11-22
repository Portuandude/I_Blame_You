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

        [Test]
        public void Ipv6LoopbackAndBracketPortWork()
        {
            Assert.IsTrue(NetAddress.TryParse("::1", 27777, out string host, out int port));
            Assert.AreEqual("::1", host);
            Assert.AreEqual(27777, port);
            Assert.IsTrue(NetAddress.TryParse("[fe80::1]:30000", 27777, out host, out port));
            Assert.AreEqual("fe80::1", host);
            Assert.AreEqual(30000, port);
            Assert.IsTrue(NetAddress.TryParse("[::1]", 27777, out host, out port));
            Assert.AreEqual("::1", host);
            Assert.AreEqual(27777, port);
            Assert.IsFalse(NetAddress.TryParse("[::1]:x", 27777, out _, out _));
            Assert.IsFalse(NetAddress.TryParse("[]:1", 27777, out _, out _));
            Assert.IsFalse(NetAddress.TryParse("[::1]9", 27777, out _, out _));
        }

        [Test]
        public void VirtualLanAddressesComeFirst_ThenHome()
        {
            var sorted = AddressRanking.Sort(new[] { "203.0.113.5", "192.168.0.12", "26.10.3.4", "100.101.5.6", "10.0.0.3" });
            CollectionAssert.AreEqual(new[] { "26.10.3.4", "100.101.5.6", "192.168.0.12", "10.0.0.3", "203.0.113.5" }, sorted);
        }

        [Test]
        public void RangesAreRecognised()
        {
            Assert.IsTrue(AddressRanking.IsVirtualLan("26.1.2.3"), "Radmin");
            Assert.IsTrue(AddressRanking.IsVirtualLan("25.1.2.3"), "Hamachi");
            Assert.IsTrue(AddressRanking.IsVirtualLan("100.64.0.1"), "Tailscale");
            Assert.IsFalse(AddressRanking.IsVirtualLan("100.128.0.1"));
            Assert.IsTrue(AddressRanking.IsVirtualLan("10.147.17.5"), "ZeroTier");
            Assert.IsTrue(AddressRanking.IsHomeLan("172.20.1.1"));
            Assert.IsFalse(AddressRanking.IsHomeLan("172.32.1.1"));
            Assert.IsFalse(AddressRanking.IsVirtualLan("fe80::1"));
            Assert.IsFalse(AddressRanking.IsHomeLan(null));
        }
    }
}
