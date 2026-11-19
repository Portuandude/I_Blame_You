using System;
using System.Threading;
using IBlameYou.Net;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 온라인 2인 연결 지키기 (Net/Core/NetPing): 1초마다 Ping, 왕복 지연 재기, 10초 무응답이면 끊기, 호스트가 친구 자리 비우기.
    public class NetPingTests
    {
        [Test]
        public void FirstPingGoesRightAway_ThenOncePerInterval()
        {
            var p = new PingTracker();
            p.Reset(100f);
            Assert.IsTrue(p.ShouldSendPing(100f));
            Assert.IsFalse(p.ShouldSendPing(100.5f));
            Assert.IsFalse(p.ShouldSendPing(100f + PingTracker.Interval - 0.01f));
            Assert.IsTrue(p.ShouldSendPing(100f + PingTracker.Interval));
        }

        [Test]
        public void PongGivesRoundTrip_AndLaterSamplesAreSmoothed()
        {
            var p = new PingTracker();
            p.Reset(0f);
            Assert.AreEqual(-1, p.RttMs, "아직 못 쟀으면 -1");
            p.PongReceived(10f, 10.05f);
            Assert.AreEqual(50, p.RttMs);
            p.PongReceived(11f, 11.25f); // 한 번 튄 값
            Assert.Greater(p.RttMs, 50);
            Assert.Less(p.RttMs, 250, "튀는 값 하나에 바로 따라가지 않는다");
        }

        [Test]
        public void ImpossiblePongIsIgnored()
        {
            var p = new PingTracker();
            p.Reset(0f);
            p.PongReceived(20f, 10f); // 미래에서 온 값 (이전 연결 등)
            Assert.AreEqual(-1, p.RttMs);
            p.PongReceived(0f, 100f); // 끊겼어야 할 만큼 오래된 값
            Assert.AreEqual(-1, p.RttMs);
        }

        [Test]
        public void SilenceLongerThanTimeoutMeansDead_AnyMessageKeepsAlive()
        {
            var p = new PingTracker();
            p.Reset(0f);
            Assert.IsFalse(p.TimedOut(PingTracker.Timeout - 0.1f));
            p.Heard(PingTracker.Timeout - 0.1f);
            Assert.IsFalse(p.TimedOut(PingTracker.Timeout + 1f));
            Assert.AreEqual(1.1f, p.SilentFor(PingTracker.Timeout + 1f), 0.001f);
            Assert.IsTrue(p.TimedOut(PingTracker.Timeout * 2f));
        }

        [Test]
        public void ResetForgetsOldConnection()
        {
            var p = new PingTracker();
            p.Reset(0f);
            p.PongReceived(0f, 0.2f);
            p.Reset(500f);
            Assert.AreEqual(-1, p.RttMs);
            Assert.IsFalse(p.TimedOut(505f));
            Assert.AreEqual(0f, new PingTracker().SilentFor(1000f), "시작 전에는 조용한 시간이 없다");
        }

        [Test]
        public void PingAndPong_RoundTrip()
        {
            var r = new NetReader(PingMessage.Write(123.25f, 47));
            Assert.AreEqual((byte)MsgType.Ping, r.ReadByte());
            PingMessage.Read(r, out float sentAt, out int rtt);
            Assert.AreEqual(123.25f, sentAt);
            Assert.AreEqual(47, rtt);

            var r2 = new NetReader(PingMessage.Write(1f, -1));
            r2.ReadByte();
            PingMessage.Read(r2, out _, out int unknown);
            Assert.AreEqual(-1, unknown);

            var r3 = new NetReader(PongMessage.Write(123.25f));
            Assert.AreEqual((byte)MsgType.Pong, r3.ReadByte());
            Assert.AreEqual(123.25f, PongMessage.Read(r3));
        }

        private static TransportEvent WaitFor(ITransport t, TransportEventKind kind)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (t.TryReceive(out var e))
                {
                    if (e.Kind == kind) return e;
                    continue;
                }
                Thread.Sleep(5);
            }
            Assert.Fail("기다린 사건이 오지 않음: " + kind);
            return default(TransportEvent);
        }

        [Test]
        public void Tcp_HostDropsSilentPeer_AndTheSeatIsFreeAgain()
        {
            using (var host = TcpTransport.Host(0))
            using (var first = TcpTransport.Connect("127.0.0.1", host.ListenPort))
            {
                WaitFor(host, TransportEventKind.Connected);
                WaitFor(first, TransportEventKind.Connected);

                host.DropPeer("응답 없음");
                var dropped = WaitFor(host, TransportEventKind.Disconnected);
                Assert.AreEqual("응답 없음", dropped.Reason);
                Assert.IsFalse(host.IsConnected);
                WaitFor(first, TransportEventKind.Disconnected);

                // 같은 친구가 다시 들어온다 (예전에는 죽은 연결이 자리를 막고 있었다)
                using (var again = TcpTransport.Connect("127.0.0.1", host.ListenPort))
                {
                    WaitFor(host, TransportEventKind.Connected);
                    WaitFor(again, TransportEventKind.Connected);
                    again.Send(new byte[] { 9 });
                    CollectionAssert.AreEqual(new byte[] { 9 }, WaitFor(host, TransportEventKind.Data).Data);
                    Assert.IsTrue(host.IsConnected);
                }
            }
        }

        [Test]
        public void Tcp_DropPeerWithNobodyIsHarmless()
        {
            using (var host = TcpTransport.Host(0))
            {
                host.DropPeer("아무도 없음");
                Assert.IsFalse(host.TryReceive(out _));
                Assert.IsFalse(host.IsConnected);
            }
        }
    }
}
