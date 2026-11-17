using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace IBlameYou.Net
{
    public enum TransportEventKind { Connected, Data, Disconnected }

    public struct TransportEvent
    {
        public TransportEventKind Kind;
        public byte[] Data;    // Data일 때 메시지 하나 (길이 머리 없이)
        public string Reason;  // Disconnected일 때 이유
    }

    // 두 컴퓨터 사이 연결 하나. 지금은 TCP(직접 IP)만, 스팀 출시 때 스팀 중계 전송을 같은 모양으로 붙인다.
    public interface ITransport : IDisposable
    {
        bool IsConnected { get; }
        long PendingSendBytes { get; }
        void Send(byte[] message);
        bool TryReceive(out TransportEvent e);
    }

    // TCP 전송. 호스트는 포트를 열고 한 명만 받는다 (2인 게임). 보내기·받기는 각자 스레드에서 돌고,
    // 게임(메인 스레드)은 Send로 넣고 TryReceive로 꺼내기만 한다. 순수 C# — EditMode 테스트가 같은 컴퓨터 안에서 붙여 본다.
    public sealed class TcpTransport : ITransport
    {
        private readonly ConcurrentQueue<TransportEvent> events = new ConcurrentQueue<TransportEvent>();
        private readonly object sendLock = new object();
        private readonly Queue<byte[]> sendQueue = new Queue<byte[]>();
        private readonly AutoResetEvent sendSignal = new AutoResetEvent(false);
        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;
        private volatile bool disposed;
        private volatile bool connected;
        private long pendingBytes;
        private int connectionId; // 끊긴 연결의 스레드가 새 연결을 건드리지 않게

        public bool IsConnected => connected;
        public long PendingSendBytes => Interlocked.Read(ref pendingBytes);
        public int ListenPort { get; private set; }

        private TcpTransport() { }

        // 호스트: port를 열고 기다린다. 0이면 빈 포트 아무거나 (테스트용).
        public static TcpTransport Host(int port)
        {
            var t = new TcpTransport();
            t.listener = new TcpListener(IPAddress.Any, port);
            t.listener.Start();
            t.ListenPort = ((IPEndPoint)t.listener.LocalEndpoint).Port;
            StartThread(t.AcceptLoop, "Net Accept");
            return t;
        }

        // 클라이언트: 호스트 주소로 붙는다 (백그라운드에서, 결과는 Connected/Disconnected 사건으로).
        public static TcpTransport Connect(string address, int port)
        {
            var t = new TcpTransport();
            StartThread(() => t.ConnectLoop(address, port), "Net Connect");
            return t;
        }

        private static void StartThread(ThreadStart body, string name)
        {
            var thread = new Thread(body) { IsBackground = true, Name = name };
            thread.Start();
        }

        private void AcceptLoop()
        {
            while (!disposed)
            {
                TcpClient incoming;
                try
                {
                    incoming = listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    if (disposed) return;
                    Thread.Sleep(200);
                    continue;
                }

                if (connected)
                {
                    // 이미 친구가 들어와 있다: 셋째 사람은 바로 닫는다.
                    try { incoming.Close(); } catch (Exception) { }
                    continue;
                }
                Attach(incoming);
            }
        }

        private void ConnectLoop(string address, int port)
        {
            var tcp = new TcpClient();
            try
            {
                var result = tcp.BeginConnect(address, port, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(8)))
                {
                    tcp.Close();
                    Fail("호스트에 연결하지 못했어요 (시간 초과). 주소·포트와 호스트 방화벽을 확인하세요.");
                    return;
                }
                tcp.EndConnect(result);
            }
            catch (Exception ex)
            {
                try { tcp.Close(); } catch (Exception) { }
                Fail("호스트에 연결하지 못했어요: " + ex.Message);
                return;
            }
            if (disposed)
            {
                tcp.Close();
                return;
            }
            Attach(tcp);
        }

        private void Fail(string reason)
        {
            events.Enqueue(new TransportEvent { Kind = TransportEventKind.Disconnected, Reason = reason });
        }

        private void Attach(TcpClient tcp)
        {
            tcp.NoDelay = true; // 입력·그림은 작고 자주 가니 모아 보내지 않는다
            tcp.SendBufferSize = 1 << 20;
            tcp.ReceiveBufferSize = 1 << 20;
            int id;
            lock (sendLock)
            {
                client = tcp;
                stream = tcp.GetStream();
                sendQueue.Clear();
                Interlocked.Exchange(ref pendingBytes, 0);
                id = ++connectionId;
                connected = true;
            }
            events.Enqueue(new TransportEvent { Kind = TransportEventKind.Connected });
            var s = stream;
            StartThread(() => ReceiveLoop(s, id), "Net Receive");
            StartThread(() => SendLoop(s, id), "Net Send");
        }

        private void ReceiveLoop(NetworkStream s, int id)
        {
            var assembler = new FrameAssembler();
            var buffer = new byte[64 * 1024];
            var frames = new List<byte[]>();
            string reason = "상대가 연결을 끊었어요.";
            try
            {
                while (!disposed)
                {
                    int read = s.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    frames.Clear();
                    assembler.Push(buffer, 0, read, frames);
                    foreach (var frame in frames)
                        events.Enqueue(new TransportEvent { Kind = TransportEventKind.Data, Data = frame });
                }
            }
            catch (Exception ex)
            {
                reason = "연결이 끊겼어요: " + ex.Message;
            }
            Drop(id, reason);
        }

        private void SendLoop(NetworkStream s, int id)
        {
            try
            {
                while (!disposed)
                {
                    byte[] next = null;
                    lock (sendLock)
                    {
                        if (id != connectionId) return;
                        if (sendQueue.Count > 0) next = sendQueue.Dequeue();
                    }
                    if (next == null)
                    {
                        sendSignal.WaitOne(100);
                        continue;
                    }
                    s.Write(next, 0, next.Length);
                    Interlocked.Add(ref pendingBytes, -next.Length);
                }
            }
            catch (Exception ex)
            {
                Drop(id, "보내다가 연결이 끊겼어요: " + ex.Message);
            }
        }

        private void Drop(int id, string reason)
        {
            lock (sendLock)
            {
                if (id != connectionId || !connected) return;
                connected = false;
                sendQueue.Clear();
                Interlocked.Exchange(ref pendingBytes, 0);
                try { client?.Close(); } catch (Exception) { }
                client = null;
                stream = null;
            }
            if (!disposed) events.Enqueue(new TransportEvent { Kind = TransportEventKind.Disconnected, Reason = reason });
        }

        public void Send(byte[] message)
        {
            var framed = FrameAssembler.Frame(message);
            lock (sendLock)
            {
                if (!connected) return;
                sendQueue.Enqueue(framed);
                Interlocked.Add(ref pendingBytes, framed.Length);
            }
            sendSignal.Set();
        }

        public bool TryReceive(out TransportEvent e) => events.TryDequeue(out e);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lock (sendLock)
            {
                connected = false;
                connectionId++;
                try { client?.Close(); } catch (Exception) { }
                client = null;
                stream = null;
            }
            try { listener?.Stop(); } catch (Exception) { }
            sendSignal.Set();
        }
    }
}
