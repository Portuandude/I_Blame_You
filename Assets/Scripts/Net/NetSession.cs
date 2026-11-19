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
        private readonly PingTracker ping = new PingTracker(); // 지연 재기·응답 없는 연결 끊기 (Net/Core/NetPing)
        private readonly TrafficMeter traffic = new TrafficMeter(); // 초당 보낸·받은 양 (Net/Core/TrafficMeter)
        private readonly ReconnectPlan reconnect = new ReconnectPlan(); // 친구: 판 도중 끊기면 같은 호스트에 다시 붙기 (Net/Core/ReconnectPlan)
        private string joinAddress = "";
        private int joinPort;
        private GUIStyle reconnectStyle;

        private ITransport transport;
        private Mode mode;
        private bool peerReady; // 호스트: 친구가 Hello를 보내고 받아들여짐
        private string status = "";
        private string peerName = "";
        private bool previousRunInBackground;
        private bool holdingBackground; // runInBackground를 켜 둔 동안 (다시 붙기 사이에도 유지)
        private bool clientViewKept; // 친구: 끊긴 뒤 다시 붙는 동안 마지막 화면을 그대로 둔다 (시작 화면으로 나가지 않음)
        private float joinStartedAt;
        private const float JoinTimeout = 15f; // 연결 시작부터 호스트 대답(Welcome)까지 (TCP 연결 8초 포함)

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

        // 친구: 호스트와 끊겨 같은 주소에 다시 붙으려는 중 (기다리거나 시도 중)
        public static bool Reconnecting => instance != null && instance.reconnect.InProgress && instance.mode != Mode.Client;
        // 상태 줄용 " · ↑120KB/s ↓3KB/s" (연결돼 있을 때만, 아니면 빈 문자열)
        public static string TrafficText()
        {
            if (instance == null || instance.transport == null || !instance.transport.IsConnected) return "";
            var t = instance.traffic;
            return $" · ↑{TrafficMeter.Format(t.SentPerSecond)} ↓{TrafficMeter.Format(t.ReceivedPerSecond)}";
        }

        public static int ReconnectAttempt => instance != null ? instance.reconnect.Attempt : 0;

        // 왕복 지연 (밀리초, 아직 모르면 -1). 호스트는 직접 재고, 친구는 호스트가 잰 값을 받는다.
        public static int RttMs => instance != null && (instance.peerReady || instance.mode == Mode.Client) ? instance.ping.RttMs : -1;
        // 상대에게서 마지막으로 무엇이든 받은 뒤 지난 초
        public static float PeerSilentSeconds => instance != null && (instance.peerReady || instance.mode == Mode.Client) ? instance.ping.SilentFor(Time.unscaledTime) : 0f;

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
            go.AddComponent<NetPeerLossPause>(); // 판 도중 친구가 끊기면 호스트를 일시정지
            return instance;
        }

        public static void StartHost(int port)
        {
            var s = Ensure();
            s.reconnect.Forget();
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
            s.reconnect.Forget(); // 사람이 고른 새 참가: 예전 호스트로 다시 붙기는 그만
            s.Connect(address, port, false);
            s.status = $"{address}:{port}에 연결하는 중...";
        }

        private void Connect(string address, int port, bool retry)
        {
            Shutdown(null, retry);
            joinStartedAt = Time.unscaledTime;
            transport = TcpTransport.Connect(address, port);
            Port = port;
            joinAddress = address;
            joinPort = port;
            mode = Mode.Joining;
            KeepRunningInBackground();
        }

        public static void Stop()
        {
            if (instance == null) return;
            instance.reconnect.Forget();
            instance.Shutdown("연결을 끊었어요.");
        }

        // 친구 쪽 연결이 사람 뜻과 상관없이 끊겼다 (호스트 응답 없음·TCP 끊김·다시 붙기 시도 실패).
        // 한 번 받아 준 호스트면 잠시 뒤 같은 주소로 다시 붙는다.
        private void LoseConnection(string reason)
        {
            bool wasRetrying = reconnect.InProgress;
            // 다음 시도를 먼저 잡는다: 잡혔으면 친구 화면을 그대로 두고(ClientEnded 없음) 위에 안내만 띄운다.
            bool retry = reconnect.Lost(Time.unscaledTime);
            Shutdown(reason, retry);
            if (retry)
                status = $"{reason}\n다시 연결하는 중 ({reconnect.Attempt}/{ReconnectPlan.MaxAttempts})...";
            else if (wasRetrying)
                status = $"호스트에 다시 연결하지 못했어요 ({ReconnectPlan.MaxAttempts}번 시도). {reason}";
        }

        public static void Send(byte[] message)
        {
            if (instance != null && instance.transport != null) instance.transport.Send(message);
        }

        private void KeepRunningInBackground()
        {
            // 두 창을 번갈아 보거나 친구 창이 뒤에 있어도 게임이 멈추면 안 된다.
            if (!holdingBackground)
            {
                previousRunInBackground = Application.runInBackground;
                holdingBackground = true;
            }
            Application.runInBackground = true;
        }

        private void ReleaseBackground()
        {
            if (!holdingBackground) return;
            Application.runInBackground = previousRunInBackground;
            holdingBackground = false;
        }

        // keepClientView: 친구가 다시 붙으려는 중이라 화면·runInBackground를 그대로 둔다
        private void Shutdown(string reason, bool keepClientView = false)
        {
            var oldMode = mode;
            bool hadPeer = peerReady;
            transport?.Dispose();
            transport = null;
            traffic.Reset();
            peerReady = false;
            peerName = "";
            tracker.Reset();
            if (!keepClientView) ReleaseBackground();
            mode = Mode.Offline;
            if (reason != null) status = reason;
            if (oldMode == Mode.Hosting && hadPeer) PeerLeft?.Invoke();
            if (oldMode == Mode.Client || clientViewKept)
            {
                if (keepClientView) clientViewKept = true;
                else
                {
                    clientViewKept = false;
                    NetClientView.End();
                    ClientEnded?.Invoke(reason ?? "");
                }
            }
        }

        private void OnDestroy()
        {
            transport?.Dispose();
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (reconnect.InProgress && mode != Mode.Client && Input.GetKeyDown(KeyCode.F10))
            {
                Stop(); // 다시 붙기 그만두기 (연결 중 시도도 닫는다)
                status = "다시 연결을 그만뒀어요.";
                return;
            }
            if (mode == Mode.Offline && reconnect.Due(Time.unscaledTime))
            {
                Connect(reconnect.Address, reconnect.Port, true);
                status = $"호스트에 다시 연결하는 중 ({reconnect.Attempt}/{ReconnectPlan.MaxAttempts})...";
            }
            if (mode == Mode.Joining && Time.unscaledTime - joinStartedAt > JoinTimeout)
            {
                // TCP는 붙었는데 호스트 게임이 대답하지 않는다 (멈췄거나 다른 프로그램)
                LoseConnection($"호스트가 {JoinTimeout:0}초 동안 대답하지 않아요.");
                return;
            }
            if (transport == null) return;
            int handled = 0;
            while (transport != null && transport.TryReceive(out var e))
            {
                Handle(e);
                if (++handled > 500) break; // 한 프레임에 너무 오래 붙잡지 않는다
            }
            if (mode == Mode.Hosting) tracker.Step();
            if (transport != null) traffic.Sample(Time.unscaledTime, transport.BytesSent, transport.BytesReceived);
            KeepAlive(Time.unscaledTime);
        }

        private void KeepAlive(float now)
        {
            if (transport == null) return;
            if (mode == Mode.Hosting && peerReady)
            {
                if (ping.TimedOut(now))
                {
                    // 친구 컴퓨터가 꺼졌거나 인터넷이 끊겼다: 자리를 비워 친구가 다시 들어올 수 있게 한다
                    transport.DropPeer($"친구 응답이 {PingTracker.Timeout:0}초 없어요");
                    return;
                }
                if (ping.ShouldSendPing(now)) transport.Send(PingMessage.Write(now, ping.RttMs));
            }
            else if (mode == Mode.Client && ping.TimedOut(now))
            {
                LoseConnection($"호스트 응답이 {PingTracker.Timeout:0}초 없어 연결을 끊었어요.");
            }
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
                        status = string.IsNullOrEmpty(e.Reason)
                            ? $"친구가 나갔어요. 다시 기다리는 중 (포트 {Port})"
                            : $"친구가 나갔어요 ({e.Reason}). 다시 기다리는 중 (포트 {Port})";
                        if (had) PeerLeft?.Invoke();
                    }
                    else LoseConnection(e.Reason ?? "연결이 끊겼어요.");
                    break;

                case TransportEventKind.Data:
                    if (e.Data == null || e.Data.Length == 0) return;
                    if (mode == Mode.Client || (mode == Mode.Hosting && peerReady)) ping.Heard(Time.unscaledTime);
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

        // 친구 화면: 다시 붙는 동안 위에 안내 (호스트 HUD가 아니라 이 컴퓨터에만 그린다)
        private void OnGUI()
        {
            if (!Reconnecting || Event.current.type != EventType.Repaint) return;
            GUI.depth = -100; // 남겨 둔 친구 화면 HUD 위에
            if (reconnectStyle == null)
                reconnectStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            float wait = reconnect.SecondsLeft(Time.unscaledTime);
            string line = reconnect.Waiting
                ? $"호스트와 연결이 끊겼어요. {wait:0}초 뒤 다시 연결 ({reconnect.Attempt}/{ReconnectPlan.MaxAttempts})"
                : $"호스트에 다시 연결하는 중... ({reconnect.Attempt}/{ReconnectPlan.MaxAttempts})";
            float w = Mathf.Min(620f, Screen.width - 20f);
            GUI.Box(new Rect((Screen.width - w) / 2f, 14f, w, 56f), line + "\n<size=13>F10: 그만두기</size>", reconnectStyle);
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
                        ping.Reset(Time.unscaledTime);
                        status = $"{peerName} 접속! P2(공격)는 친구 마우스로 해요.";
                        PeerJoined?.Invoke();
                        break;
                    case MsgType.Pong:
                        if (!peerReady) return;
                        ping.PongReceived(PongMessage.Read(r), Time.unscaledTime);
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
                    reconnect.Forget(); // 버전이 다르면 다시 해 봐야 같다
                    Shutdown(reason ?? "호스트가 거절했어요.");
                    return;
                }
                bool again = reconnect.InProgress;
                reconnect.Remember(joinAddress, joinPort);
                clientViewKept = false;
                mode = Mode.Client;
                ping.Reset(Time.unscaledTime);
                status = again ? "다시 접속했어요! 나는 P2(공격)" : "접속했어요! 나는 P2(공격): 마우스로 조준·공격";
                NetClientView.Begin();
                ClientStarted?.Invoke();
                return;
            }

            if (mode != Mode.Client) return;
            if (type == MsgType.Ping)
            {
                PingMessage.Read(r, out float sentAt, out int rttMs);
                ping.SetRemoteRtt(rttMs);
                transport.Send(PongMessage.Write(sentAt));
                return;
            }
            NetClientView.Receive(type, r);
        }
    }
}
