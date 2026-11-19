using System;
using System.Threading;
using IBlameYou.Net;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 온라인 상태 줄의 초당 보낸·받은 양 (Net/Core/TrafficMeter)
    public class NetTrafficTests
    {
        [Test]
        public void RateIsDifferenceOverOneSecondWindow()
        {
            var m = new TrafficMeter();
            m.Sample(10f, 1000, 500);
            m.Sample(10.5f, 60000, 900);
            Assert.AreEqual(0f, m.SentPerSecond, "1초가 안 지났으면 아직 0");
            m.Sample(12f, 1000 + 2 * 20480, 500 + 2 * 1024);
            Assert.AreEqual(20480f, m.SentPerSecond, 0.01f);
            Assert.AreEqual(1024f, m.ReceivedPerSecond, 0.01f);
        }

        [Test]
        public void NewTransportWithSmallerTotalsRestartsBaseline()
        {
            var m = new TrafficMeter();
            m.Sample(0f, 100000, 100000);
            m.Sample(1f, 200000, 200000);
            m.Sample(2f, 10, 10); // 새 연결: 누적이 줄었다
            Assert.Greater(m.SentPerSecond, 0f, "기준만 다시 잡고 음수 값을 만들지 않는다");
            m.Sample(3f, 1034, 10);
            Assert.AreEqual(1024f, m.SentPerSecond, 0.01f);
            Assert.AreEqual(0f, m.ReceivedPerSecond, 0.01f);
        }

        [Test]
        public void ResetClearsRates()
        {
            var m = new TrafficMeter();
            m.Sample(0f, 0, 0);
            m.Sample(1f, 5000, 5000);
            m.Reset();
            Assert.AreEqual(0f, m.SentPerSecond);
            Assert.AreEqual(0f, m.ReceivedPerSecond);
        }

        [Test]
        public void FormatUsesKbThenMb()
        {
            Assert.AreEqual("0KB/s", TrafficMeter.Format(0f));
            Assert.AreEqual("120KB/s", TrafficMeter.Format(120f * 1024f));
            Assert.AreEqual("1.5MB/s", TrafficMeter.Format(1.5f * 1024f * 1024f));
        }

        [Test]
        public void TcpTransportCountsBytesBothWays()
        {
            using (var host = TcpTransport.Host(0))
            using (var client = TcpTransport.Connect("127.0.0.1", host.ListenPort))
            {
                WaitFor(client, TransportEventKind.Connected);
                WaitFor(host, TransportEventKind.Connected);
                client.Send(new byte[100]);
                WaitFor(host, TransportEventKind.Data);
                var deadline = DateTime.UtcNow.AddSeconds(5); // 보낸 쪽은 Write가 끝난 뒤 센다 (받는 쪽보다 늦을 수 있음)
                while (client.BytesSent < 100L && DateTime.UtcNow < deadline) Thread.Sleep(5);
                Assert.GreaterOrEqual(client.BytesSent, 100L);
                Assert.GreaterOrEqual(host.BytesReceived, 100L);
                Assert.AreEqual(0L, host.BytesSent);
            }
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
    }
}
