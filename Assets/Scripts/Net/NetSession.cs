using System;
using UnityEngine;

namespace IBlameYou.Net
{
    // 온라인 2인 연결 하나를 들고 있는다 (Docs/OnlinePlay.md).
    //   호스트: 게임 전체를 돌리고 P1(이동)을 맡는다. 친구가 들어오면 P2 입력은 친구 컴퓨터에서 받는다 (P2Input).
    //           매 프레임 그림·HUD·소리를 친구에게 보낸다 (NetHostStreamer).
    //   클라이언트(친구): 게임을 돌리지 않는다. P2 입력을 보내고, 받은 것을 그대로 그린다 (NetClientView).
    // 다른 스크립트보다 먼저 돌아서(실행 순서 -1000) 이번 프레임의 P2 입력을 먼저 맞춰 둔다.
    [DefaultExecutionOrder(-1000)]
    public class NetSession : MonoBehaviour
    {
        public enum Mode { Offline, Hosting, Joining, Client }

        private static NetSession instance;
        private static readonly P2InputTracker tracker = new P2InputTracker();
        private static readonly P2InputState scratchInput = new P2InputState();

        private ITransport transport;
        private Mode mode;
        private bool peerReady; // 호스트: 친구가 Hello를 보내고 받아들여짐
        private string status = "";
        private string peerName = "";
        private bool previousRunInBackground;

        public static Mode CurrentMode => instance != null ? instance.mode : Mode.Offline;
        public static bool IsHost => CurrentMode == Mode.Hosting;
        public static bool IsClient => CurrentMode == Mode.Client;
        public static bool IsJoining => CurrentMode == Mode.Joining;

        // 호스트이고 친구가 들어와 있다: P2 입력은 친구 것, 그림을 보낸다.
        public static bool RemoteP2Active => instance != null && instance.mode == Mode.Hosting && instance.peerReady && instance.transport != null && instance.transport.IsConnected;

        public static P2InputTracker Tracker => tracker;
        public static ITransport Transport => instance != null ? instance.transport : null;
        public static string Status => instance != null ? instance.status : "";
        public static string PeerName => instance != null ? instance.peerName : "";
        public static int Port { get; private set; } = NetProtocol.DefaultPort;

        // 클라이언트: 호스트가 받아 줌 / 연결이 끝남 (PlaytestBootstrap이 시작 화면을 감추고 되살린다)
        public static event Action ClientStarted;
        public static event Action<string> ClientEnded;
        // 호스트: 친구가 들어옴 (그림 표를 처음부터 다시 보낸다)
        public static event Action PeerJoined;
        public static event Action PeerLeft;

        private static NetSession Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("NetSession");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NetSession>();
            go.AddComponent<NetHostStreamer>();
            return instance;
        }

        public static void StartHost(int port)
        {
            var s = Ensure();
            s.Shutdown(null);
            try
            {
                var tcp = TcpTransport.Host(port);
                s.transport = tcp;
                Port = tcp.ListenPort;
            }
            catch (Exception ex)
            {
                s.status = "호스트를 열지 못했어요: " + ex.Message;
                return;
            }
            s.mode = Mode.Hosting;
            s.KeepRunningInBackground();
            s.status = $"친구를 기다리는 중 (포트 {Port})";
        }

        public static void Join(string address, int port)
        {
            var s = Ensure();
            s.Shutdown(null);
            s.transport = TcpTransport.Connect(address, port);
            Port = port;
            s.mode = Mode.Joining;
            s.KeepRunningInBackground();
            s.status = $"{address}:{port}에 연결하는 중...";
        }

        public static void Stop()
        {
            if (instance != null) instance.Shutdown("연결을 끊었어요.");
        }

        public static void Send(byte[] message)
        {
            if (instance != null && instance.transport != null) instance.transport.Send(message);
        }

        private void KeepRunningInBackground()
        {
            // 두 창을 번갈아 보거나 친구 창이 뒤에 있어도 게임이 멈추면 안 된다.
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        private void Shutdown(string reason)
        {
            var oldMode = mode;
            bool hadPeer = peerReady;
            transport?.Dispose();
            transport = null;
            peerReady = false;
            peerName = "";
            tracker.Reset();
            if (oldMode != Mode.Offline) Application.runInBackground = previousRunInBackground;
            mode = Mode.Offline;
            if (reason != null) status = reason;
            if (oldMode == Mode.Hosting && hadPeer) PeerLeft?.Invoke();
            if (oldMode == Mode.Client)
            {
                NetClientView.End();
                ClientEnded?.Invoke(reason ?? "");
            }
        }

        private void OnDestroy()
        {
            transport?.Dispose();
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (transport == null) return;
            int handled = 0;
            while (transport != null && transport.TryReceive(out var e))
            {
                Handle(e);
                if (++handled > 500) break; // 한 프레임에 너무 오래 붙잡지 않는다
            }
            if (mode == Mode.Hosting) tracker.Step();
        }

        private void Handle(TransportEvent e)
        {
            switch (e.Kind)
            {
                case TransportEventKind.Connected:
                    if (mode == Mode.Joining)
                    {
                        status = "연결됨, 호스트 대답을 기다리는 중...";
                        transport.Send(HelloMessage.Write(SystemInfo.deviceName));
                    }
                    else if (mode == Mode.Hosting) status = "누군가 들어오는 중...";
                    break;

                case TransportEventKind.Disconnected:
                    if (mode == Mode.Hosting)
                    {
                        bool had = peerReady;
                        peerReady = false;
                        peerName = "";
                        tracker.Reset();
                        status = $"친구가 나갔어요. 다시 기다리는 중 (포트 {Port})";
                        if (had) PeerLeft?.Invoke();
                    }
                    else Shutdown(e.Reason ?? "연결이 끊겼어요.");
                    break;

                case TransportEventKind.Data:
                    if (e.Data == null || e.Data.Length == 0) return;
                    try
                    {
                        HandleMessage(NetCompression.Unwrap(e.Data));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[NetSession] 메시지를 읽다가 실패: " + ex);
                    }
                    break;
            }
        }

        private void HandleMessage(byte[] data)
        {
            var type = (MsgType)data[0];
            var r = new NetReader(data, 1, data.Length - 1);
            if (mode == Mode.Hosting)
            {
                switch (type)
                {
                    case MsgType.Hello:
                        HelloMessage.Read(r, out ushort version, out string name);
                        if (version != NetProtocol.Version)
                        {
                            transport.Send(WelcomeMessage.Write(false, $"게임 버전이 달라요 (호스트 {NetProtocol.Version}, 친구 {version}). 같은 빌드로 해 주세요."));
                            return;
                        }
                        peerReady = true;
                        peerName = string.IsNullOrEmpty(name) ? "친구" : name;
                        tracker.Reset();
                        transport.Send(WelcomeMessage.Write(true, null));
                        status = $"{peerName} 접속! P2(공격)는 친구 마우스로 해요.";
                        PeerJoined?.Invoke();
                        break;
                    case MsgType.P2Input:
                        if (!peerReady) return;
                        scratchInput.Read(r);
                        tracker.Receive(scratchInput);
                        break;
                }
                return;
            }

            if (mode == Mode.Joining && type == MsgType.Welcome)
            {
                WelcomeMessage.Read(r, out _, out bool accepted, out string reason);
                if (!accepted)
                {
                    Shutdown(reason ?? "호스트가 거절했어요.");
                    return;
                }
                mode = Mode.Client;
                status = "접속했어요! 나는 P2(공격): 마우스로 조준·공격";
                NetClientView.Begin();
                ClientStarted?.Invoke();
                return;
            }

            if (mode == Mode.Client) NetClientView.Receive(type, r);
        }
    }
}
