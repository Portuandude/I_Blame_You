using System;
using System.Collections.Generic;
using System.Threading;
using IBlameYou.Net;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 온라인 2인 (Docs/OnlinePlay.md) 순수 네트워크 코드: 바이트 쓰기/읽기, 메시지 경계, P2 입력 누름 판정, TCP 왕복.
    public class NetCoreTests
    {
        [Test]
        public void WriterReader_RoundTripsAllTypes()
        {
            var w = new NetWriter(4); // 작게 시작해서 늘어나는 것도 본다
            w.WriteByte(250);
            w.WriteBool(true);
            w.WriteUInt16(65000);
            w.WriteUInt32(4000000000u);
            w.WriteVarUInt(0);
            w.WriteVarUInt(127);
            w.WriteVarUInt(128);
            w.WriteVarUInt(uint.MaxValue);
            w.WriteVarInt(-1);
            w.WriteVarInt(int.MinValue);
            w.WriteVarInt(123456);
            w.WriteFloat(-3.25f);
            w.WriteString(null);
            w.WriteString("");
            w.WriteString("마법진 火");
            w.WriteBytes(new byte[] { 1, 2, 3 });
            w.WriteBytes(null);

            var r = new NetReader(w.ToArray());
            Assert.AreEqual(250, r.ReadByte());
            Assert.IsTrue(r.ReadBool());
            Assert.AreEqual(65000, r.ReadUInt16());
            Assert.AreEqual(4000000000u, r.ReadUInt32());
            Assert.AreEqual(0u, r.ReadVarUInt());
            Assert.AreEqual(127u, r.ReadVarUInt());
            Assert.AreEqual(128u, r.ReadVarUInt());
            Assert.AreEqual(uint.MaxValue, r.ReadVarUInt());
            Assert.AreEqual(-1, r.ReadVarInt());
            Assert.AreEqual(int.MinValue, r.ReadVarInt());
            Assert.AreEqual(123456, r.ReadVarInt());
            Assert.AreEqual(-3.25f, r.ReadFloat());
            Assert.IsNull(r.ReadString());
            Assert.AreEqual("", r.ReadString());
            Assert.AreEqual("마법진 火", r.ReadString());
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, r.ReadBytes());
            Assert.IsNull(r.ReadBytes());
            Assert.IsTrue(r.AtEnd);
        }

        [Test]
        public void Reader_ThrowsOnTruncatedMessage()
        {
            var w = new NetWriter();
            w.WriteString("abcdef");
            var bytes = w.ToArray();
            var r = new NetReader(bytes, 0, bytes.Length - 2);
            Assert.Throws<FormatException>(() => r.ReadString());
        }

        [Test]
        public void SmallVarUIntsTakeOneByte()
        {
            var w = new NetWriter();
            w.WriteVarUInt(100);
            w.WriteVarInt(-50);
            Assert.AreEqual(2, w.Length);
        }

        [Test]
        public void Writer_HashFollowsContent()
        {
            var a = new NetWriter();
            var b = new NetWriter();
            a.WriteVarInt(18); a.WriteString("x");
            b.WriteVarInt(18); b.WriteString("x");
            Assert.AreEqual(a.Hash(), b.Hash()); // 다른 writer라도 같은 내용이면 같은 해시 (HUD 스타일 묶기)
            b.WriteByte(1);
            Assert.AreNotEqual(a.Hash(), b.Hash());
            b.Clear(); b.WriteVarInt(18); b.WriteString("x");
            Assert.AreEqual(a.Hash(), b.Hash());
        }

        [Test]
        public void Writer_ToArrayReusesSameLengthBuffer()
        {
            var w = new NetWriter();
            w.WriteByte(1); w.WriteByte(2);
            var first = w.ToArray();
            w.Clear(); w.WriteByte(3); w.WriteByte(4);
            var again = w.ToArray(first);
            Assert.AreSame(first, again);
            CollectionAssert.AreEqual(new byte[] { 3, 4 }, again);
            w.WriteByte(5);
            var longer = w.ToArray(first);
            Assert.AreNotSame(first, longer);
            CollectionAssert.AreEqual(new byte[] { 3, 4, 5 }, longer);
            Assert.AreEqual(3, w.ToArray(null).Length);
        }

        [Test]
        public void FrameAssembler_SplitsAndJoinsChunks()
        {
            var a = FrameAssembler.Frame(new byte[] { 1, 2, 3 });
            var b = FrameAssembler.Frame(new byte[0]);
            var c = FrameAssembler.Frame(new byte[] { 9 });
            var all = new List<byte>();
            all.AddRange(a);
            all.AddRange(b);
            all.AddRange(c);
            var stream = all.ToArray();

            // 한 바이트씩 들어와도, 한 번에 다 들어와도 같은 메시지 셋
            foreach (int chunk in new[] { 1, 2, 5, stream.Length })
            {
                var assembler = new FrameAssembler();
                var frames = new List<byte[]>();
                for (int i = 0; i < stream.Length; i += chunk)
                    assembler.Push(stream, i, Math.Min(chunk, stream.Length - i), frames);
                Assert.AreEqual(3, frames.Count, "chunk " + chunk);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, frames[0]);
                Assert.AreEqual(0, frames[1].Length);
                CollectionAssert.AreEqual(new byte[] { 9 }, frames[2]);
            }
        }

        [Test]
        public void FrameAssembler_RejectsAbsurdLength()
        {
            var assembler = new FrameAssembler();
            var bad = new byte[] { 0xff, 0xff, 0xff, 0x7f };
            Assert.Throws<FormatException>(() => assembler.Push(bad, 0, bad.Length, new List<byte[]>()));
        }

        [Test]
        public void FrameAssembler_HostLimitRejectsBigFrame_ButClientAcceptsIt()
        {
            var header = new byte[] { 0, 0, 8, 0 }; // 512KB
            Assert.Throws<FormatException>(() =>
                new FrameAssembler(FrameAssembler.HostMaxFrameBytes).Push(header, 0, header.Length, new List<byte[]>()));
            Assert.DoesNotThrow(() => new FrameAssembler().Push(header, 0, header.Length, new List<byte[]>()));
        }

        [Test]
        public void FrameAssembler_KeepsWorkingAfterBigFrameShrinksBuffer()
        {
            var big = new byte[3 * 1024 * 1024];
            big[big.Length - 1] = 7;
            var assembler = new FrameAssembler();
            var frames = new List<byte[]>();
            var framed = FrameAssembler.Frame(big);
            assembler.Push(framed, 0, framed.Length, frames);
            Assert.AreEqual(1, frames.Count);
            Assert.AreEqual(7, frames[0][big.Length - 1]);

            // 줄어든 뒤에도 반쯤 들어온 메시지를 잇는다
            var small = FrameAssembler.Frame(new byte[] { 4, 5, 6 });
            assembler.Push(small, 0, 3, frames);
            assembler.Push(small, 3, small.Length - 3, frames);
            Assert.AreEqual(2, frames.Count);
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, frames[1]);
        }

        [Test]
        public void Reader_HugeByteCountThrowsFormatException()
        {
            var w = new NetWriter();
            w.WriteVarUInt(int.MaxValue); // 길이만 적고 내용 없음
            var bytes = w.ToArray();
            Assert.Throws<FormatException>(() => new NetReader(bytes).ReadBytes());
            Assert.Throws<FormatException>(() => new NetReader(bytes).ReadString());
        }

        [Test]
        public void Compression_RoundTrips_AndRejectsBomb()
        {
            var text = new byte[8000];
            for (int i = 0; i < text.Length; i++) text[i] = (byte)(i % 7);
            var packed = NetCompression.MaybeCompress(text);
            Assert.AreEqual((byte)MsgType.Compressed, packed[0]);
            CollectionAssert.AreEqual(text, NetCompression.Unwrap(packed));

            // 1MB 0을 줄인 것을 64KB 한도로 풀면 거절
            var zeros = NetCompression.MaybeCompress(new byte[1024 * 1024]);
            Assert.Less(zeros.Length, 64 * 1024);
            Assert.Throws<FormatException>(() => NetCompression.Unwrap(zeros, 64 * 1024));
        }

        [Test]
        public void P2Input_NonFiniteValuesKeepPreviousValue()
        {
            var good = new P2InputState { MouseX = 0.3f, MouseY = 0.6f, WheelTotal = 4f };
            var back = new P2InputState();
            var bytes = good.Write();
            back.Read(new NetReader(bytes, 1, bytes.Length - 1));

            var bad = new P2InputState { MouseX = float.NaN, MouseY = float.PositiveInfinity, WheelTotal = float.NegativeInfinity };
            bytes = bad.Write();
            back.Read(new NetReader(bytes, 1, bytes.Length - 1));
            Assert.AreEqual(0.3f, back.MouseX);
            Assert.AreEqual(0.6f, back.MouseY);
            Assert.AreEqual(4f, back.WheelTotal);
        }

        private static P2InputState RoundTrip(P2InputState s)
        {
            var bytes = s.Write();
            Assert.AreEqual((byte)MsgType.P2Input, bytes[0]);
            var r = new NetReader(bytes, 1, bytes.Length - 1);
            var back = new P2InputState();
            back.Read(r);
            Assert.IsTrue(r.AtEnd);
            return back;
        }

        [Test]
        public void P2Input_RoundTrips()
        {
            var s = new P2InputState { Sequence = 77, MouseX = 0.25f, MouseY = 0.75f, MouseInside = false, WheelTotal = -2f };
            s.ButtonHeld[1] = true;
            s.ButtonPresses[0] = 65535;
            s.ButtonReleases[2] = 3;
            s.KeyHeld[(int)P2Key.Space] = true;
            s.KeyPresses[(int)P2Key.Alpha3] = 9;

            var b = RoundTrip(s);
            Assert.AreEqual(77u, b.Sequence);
            Assert.AreEqual(0.25f, b.MouseX);
            Assert.AreEqual(0.75f, b.MouseY);
            Assert.IsFalse(b.MouseInside);
            Assert.AreEqual(-2f, b.WheelTotal);
            CollectionAssert.AreEqual(new[] { false, true, false }, b.ButtonHeld);
            Assert.AreEqual(65535, b.ButtonPresses[0]);
            Assert.AreEqual(3, b.ButtonReleases[2]);
            Assert.IsTrue(b.KeyHeld[(int)P2Key.Space]);
            Assert.IsFalse(b.KeyHeld[(int)P2Key.Escape]);
            Assert.AreEqual(9, b.KeyPresses[(int)P2Key.Alpha3]);
        }

        [Test]
        public void Tracker_FirstStateIsBaselineWithoutPhantomClicks()
        {
            var t = new P2InputTracker();
            var s = new P2InputState { Sequence = 1 };
            s.ButtonPresses[0] = 40; // 접속 전 이미 40번 누른 친구
            t.Receive(s);
            t.Step();
            Assert.IsFalse(t.ButtonDown[0]);
        }

        [Test]
        public void Tracker_QuickClickBetweenMessagesIsSeenOnce()
        {
            var t = new P2InputTracker();
            var s = new P2InputState { Sequence = 1 };
            t.Receive(s);
            t.Step();

            // 다음 메시지 전에 눌렀다 뗐다 (held는 false인데 횟수는 늘었다)
            var s2 = new P2InputState { Sequence = 2 };
            s2.ButtonPresses[0] = 1;
            s2.ButtonReleases[0] = 1;
            s2.KeyPresses[(int)P2Key.Alpha2] = 1;
            s2.WheelTotal = 1f;
            t.Receive(s2);
            t.Step();
            Assert.IsTrue(t.ButtonDown[0]);
            Assert.IsTrue(t.ButtonUp[0]);
            Assert.IsFalse(t.Held(0));
            Assert.IsTrue(t.KeyDown[(int)P2Key.Alpha2]);
            Assert.AreEqual(1f, t.WheelDelta);

            // 새 메시지가 없는 다음 호스트 프레임: 한 번만
            t.Step();
            Assert.IsFalse(t.ButtonDown[0]);
            Assert.IsFalse(t.KeyDown[(int)P2Key.Alpha2]);
            Assert.AreEqual(0f, t.WheelDelta);
        }

        [Test]
        public void Tracker_IgnoresOlderSequence_AndCounterWrapStillCounts()
        {
            var t = new P2InputTracker();
            var s = new P2InputState { Sequence = 10 };
            s.ButtonPresses[1] = 65535;
            t.Receive(s);
            t.Step();

            var old = new P2InputState { Sequence = 9 };
            old.ButtonHeld[1] = true;
            t.Receive(old);
            Assert.IsFalse(t.Held(1));

            var wrapped = new P2InputState { Sequence = 11 };
            wrapped.ButtonPresses[1] = 0; // 65535 → 0 (한 번 더 누름)
            wrapped.ButtonHeld[1] = true;
            t.Receive(wrapped);
            t.Step();
            Assert.IsTrue(t.ButtonDown[1]);
            Assert.IsTrue(t.Held(1));
        }

        [Test]
        public void Tracker_ResetForgetsState()
        {
            var t = new P2InputTracker();
            var s = new P2InputState { Sequence = 1 };
            s.ButtonHeld[0] = true;
            t.Receive(s);
            t.Step();
            t.Reset();
            Assert.IsFalse(t.HasState);
            Assert.IsFalse(t.Held(0));
        }

        [Test]
        public void HelloAndWelcome_RoundTrip()
        {
            var hello = HelloMessage.Write("벨");
            var r = new NetReader(hello, 1, hello.Length - 1);
            HelloMessage.Read(r, out ushort version, out string name);
            Assert.AreEqual(NetProtocol.Version, version);
            Assert.AreEqual("벨", name);

            var welcome = WelcomeMessage.Write(false, "버전이 달라요");
            var r2 = new NetReader(welcome, 1, welcome.Length - 1);
            WelcomeMessage.Read(r2, out ushort v2, out bool accepted, out string reason);
            Assert.AreEqual(NetProtocol.Version, v2);
            Assert.IsFalse(accepted);
            Assert.AreEqual("버전이 달라요", reason);
        }

        [Test]
        public void Writer_PatchFloatOverwritesEarlierValue()
        {
            var w = new NetWriter(8);
            w.WriteByte(7);
            int at = w.Length;
            w.WriteFloat(0f);
            w.WriteFloat(2.5f);
            w.PatchFloat(at, 123.25f);
            var r = new NetReader(w.ToArray());
            Assert.AreEqual(7, r.ReadByte());
            Assert.AreEqual(123.25f, r.ReadFloat());
            Assert.AreEqual(2.5f, r.ReadFloat(), "뒤 값은 그대로");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => w.PatchFloat(w.Length - 2, 1f));
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
        public void Tcp_LoopbackExchangesMessagesBothWays_AndRejectsThirdPlayer()
        {
            using (var host = TcpTransport.Host(0))
            using (var client = TcpTransport.Connect("127.0.0.1", host.ListenPort))
            {
                WaitFor(host, TransportEventKind.Connected);
                WaitFor(client, TransportEventKind.Connected);

                var big = new byte[300000]; // 큰 그림도 한 메시지로 온다
                for (int i = 0; i < big.Length; i++) big[i] = (byte)(i * 7);
                client.Send(new byte[] { 1, 2 });
                host.Send(big);
                host.Send(new byte[] { 5 });

                CollectionAssert.AreEqual(new byte[] { 1, 2 }, WaitFor(host, TransportEventKind.Data).Data);
                CollectionAssert.AreEqual(big, WaitFor(client, TransportEventKind.Data).Data);
                CollectionAssert.AreEqual(new byte[] { 5 }, WaitFor(client, TransportEventKind.Data).Data);

                using (var third = TcpTransport.Connect("127.0.0.1", host.ListenPort))
                {
                    // 접속은 되자마자 호스트가 닫는다
                    WaitFor(third, TransportEventKind.Disconnected);
                }
                Assert.IsTrue(host.IsConnected);

                client.Dispose();
                var gone = WaitFor(host, TransportEventKind.Disconnected);
                Assert.IsFalse(host.IsConnected);
                Assert.IsNotNull(gone.Reason);
            }
        }

        [Test]
        public void Tcp_ConnectToClosedPortFails()
        {
            int port;
            using (var probe = TcpTransport.Host(0)) port = probe.ListenPort;
            using (var client = TcpTransport.Connect("127.0.0.1", port))
            {
                var e = WaitFor(client, TransportEventKind.Disconnected);
                StringAssert.Contains("연결하지 못했어요", e.Reason);
                Assert.IsFalse(client.IsConnected);
            }
        }

        [Test]
        public void Compression_ShrinksBigRepetitiveMessages_AndRoundTrips()
        {
            var w = new NetWriter();
            w.WriteByte((byte)MsgType.Hud);
            for (int i = 0; i < 200; i++) w.WriteString("체력 100 / 100  마나 50");
            var original = w.ToArray();
            var packed = NetCompression.MaybeCompress(original);
            Assert.AreEqual((byte)MsgType.Compressed, packed[0]);
            Assert.Less(packed.Length, original.Length / 4);
            CollectionAssert.AreEqual(original, NetCompression.Unwrap(packed));
        }

        [Test]
        public void Compression_LeavesSmallMessagesAlone()
        {
            var small = new byte[] { (byte)MsgType.World, 1, 2, 3 };
            Assert.AreSame(small, NetCompression.MaybeCompress(small));
            Assert.AreSame(small, NetCompression.Unwrap(small));
        }
    }
}
