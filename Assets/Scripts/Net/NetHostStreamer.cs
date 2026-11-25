using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Net
{
    // 호스트 쪽: 친구가 들어와 있으면 매 프레임 끝에 그림·HUD·소리를 보낸다.
    // 모든 게임 스크립트의 LateUpdate(카메라 따라가기 포함) 뒤에 돈다.
    // 친구 쪽 인터넷이 느려 보낼 것이 밀리면(1.5MB 넘게) 새 그림을 잠시 건너뛴다. 이미 기록한 HUD는 버리지 않는다
    // (스타일 정의가 그 안에 있어서). 대신 그 프레임은 HUD 기록을 하지 않는다.
    [DefaultExecutionOrder(32000)]
    public class NetHostStreamer : MonoBehaviour
    {
        private const long BacklogLimit = 1536 * 1024;
        // 지워진 그림이 이만큼 쌓이면 그림 표를 비우고 친구 화면을 처음부터 다시 보낸다 (판을 여러 번 다시 시작해도 표가 끝없이 커지지 않게).
        // 지운 그림 확인은 표 전체를 도므로 몇 초에 한 번만.
        internal const int DeadAssetLimit = 128;
        private const float DeadCheckInterval = 5f;
        private float nextDeadCheck;

        private readonly NetAssetSender assets = new NetAssetSender(NetSession.Send);
        private NetWorldCapture world;
        private readonly List<byte[]> hudFrames = new List<byte[]>();
        private byte[] lastHud;
        private bool sentEmptyHud;
        private bool wasActive;

        public static long BytesSent { get; private set; }

        private void Awake()
        {
            world = new NetWorldCapture(assets);
            NetSession.PeerJoined += OnPeerJoined;
        }

        private void OnDestroy()
        {
            NetSession.PeerJoined -= OnPeerJoined;
            HudRecorder.Recording = false;
        }

        private void OnPeerJoined()
        {
            // 새 친구: 그림 표·스타일 표·지난 그림을 다 잊고 처음부터 보낸다
            ResetStream();
        }

        private void ResetStream()
        {
            assets.Reset();
            world.Reset();
            HudRecorder.ResetTables();
            HudRecorder.Assets = assets;
            NetAudio.Clear();
            lastHud = null;
            sentEmptyHud = false;
            Send(new[] { (byte)MsgType.ResetView });
            var music = GameAudio.CurrentMusic;
            if (!string.IsNullOrEmpty(music))
            {
                NetAudio.RecordMusic(music);
                // 광폭화로 빨라진 곡·보스 방 앞에서 줄인 크기도 이어 받게 (곡 이름만 보내면 친구는 1로 듣던 것, 버그 점검 2026-11-24)
                if (!Mathf.Approximately(GameAudio.MusicPitch, 1f)) NetAudio.Record(AudioOp.MusicPitch, GameAudio.MusicPitch);
            }
            if (!Mathf.Approximately(GameAudio.MusicDuck, 1f)) NetAudio.Record(AudioOp.MusicDuck, GameAudio.MusicDuck);
        }

        private static void Send(byte[] message)
        {
            if (message == null) return;
            var packed = NetCompression.MaybeCompress(message);
            BytesSent += packed.Length;
            NetSession.Send(packed);
        }

        private void LateUpdate()
        {
            bool active = NetSession.RemoteP2Active;
            if (!active)
            {
                if (wasActive)
                {
                    HudRecorder.Recording = false;
                    HudRecorder.ResetTables();
                    NetAudio.Clear();
                }
                wasActive = false;
                return;
            }
            wasActive = true;

            // 지난 프레임에 기록된 HUD (스타일 정의가 들어 있을 수 있어 꼭 보낸다)
            hudFrames.Clear();
            HudRecorder.TakeCompleted(hudFrames);
            if (hudFrames.Count > 0)
            {
                foreach (var frame in hudFrames)
                {
                    // 바뀐 것이 없으면 건너뛴다. 단 스타일 정의가 있는 프레임은 내용이 달라서 걸리지 않는다
                    if (lastHud != null && SameBytes(frame, lastHud)) continue;
                    Send(frame);
                    lastHud = frame;
                }
                sentEmptyHud = false;
            }
            else if (HudRecorder.Recording && !sentEmptyHud && HudRecorder.LastRecordedFrame < Time.frameCount - 1)
            {
                // 아무것도 그리지 않은 프레임: 친구 화면의 HUD도 지운다 (한 번만)
                var w = new NetWriter(8);
                w.WriteByte((byte)MsgType.Hud);
                w.WriteVarUInt((uint)Screen.width);
                w.WriteVarUInt((uint)Screen.height);
                Send(w.ToArray());
                lastHud = null;
                sentEmptyHud = true;
            }

            Send(NetAudio.Take());

            if (Time.unscaledTime >= nextDeadCheck)
            {
                nextDeadCheck = Time.unscaledTime + DeadCheckInterval;
                if (assets.DeadCount() >= DeadAssetLimit)
                {
                    // 이번 프레임 HUD는 이미 보냈다. 다시 보내기 뒤 HUD 스타일은 다음 프레임 기록부터 새 표로 정의된다.
                    // 월드는 아래에서 같은 프레임에 전체를 다시 보낸다 (return하면 친구 화면이 한 프레임 비어 깜빡이던 것, 버그 점검 2026-11-24)
                    ResetStream();
                }
            }

            var transport = NetSession.Transport;
            bool behind = transport != null && transport.PendingSendBytes > BacklogLimit;
            HudRecorder.Recording = !behind;
            if (behind) return;
            Send(world.Capture());
        }

        // 호스트 화면에 친구(P2) 커서 자리를 주황 십자로 보여 준다 (P1이 친구가 어디를 겨누는지 알게).
        // UnityEngine.GUI를 직접 써서 기록하지 않는다 (친구 화면에는 자기 커서가 있다).
        private static readonly Color P2Orange = new Color(1f, 0.65f, 0.35f, 0.95f);

        private GUIStyle statusStyle;

        private void OnGUI()
        {
            if (!NetSession.RemoteP2Active || Event.current.type != EventType.Repaint) return;
            GUI.depth = -1000;
            DrawPeerStatus();
            if (!NetSession.Tracker.HasState) return; // 첫 입력 전: 지난 친구 값·(0, 0) 구석에 십자를 그리지 않는다
            var s = NetSession.Tracker.Latest;
            if (!s.MouseInside) return;
            float x = s.MouseX * Screen.width;
            float y = (1f - s.MouseY) * Screen.height;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            DrawCross(x + 1f, y + 1f);
            GUI.color = P2Orange;
            DrawCross(x, y);
            GUI.color = previous;
        }

        // 오른쪽 아래 작은 줄: 친구 이름과 왕복 지연. 친구 응답이 2초 넘게 없으면 몇 초째인지 (10초면 끊는다).
        private void DrawPeerStatus()
        {
            if (statusStyle == null)
                statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.LowerRight, richText = true };
            // 0.25초마다만 다시 만든다 (친구 화면 상태줄과 같음 — Repaint마다 문자열 여러 개를 만들던 것)
            if (statusLine == null || Time.unscaledTime >= statusLineAt)
            {
                statusLineAt = Time.unscaledTime + 0.25f;
                statusLine = BuildPeerStatus();
            }
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(0f, Screen.height - 22f, Screen.width - 8f, 20f), statusLine, statusStyle);
            GUI.color = previous;
        }

        private string statusLine;
        private float statusLineAt;

        private static string BuildPeerStatus()
        {
            float silent = NetSession.PeerSilentSeconds;
            return silent > 2f
                ? $"<color=#ffa659>{NetSession.PeerName} 응답 없음 {silent:0}초</color> ({PingTracker.Timeout:0}초면 끊김)"
                : $"<color=#ffa659>P2</color> {NetSession.PeerName} · {NetClientView.LatencyText(NetSession.RttMs)}{NetSession.TrafficText()}";
        }

        private static void DrawCross(float x, float y)
        {
            const float arm = 9f;
            const float gap = 3f;
            const float t = 2f;
            var white = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(x - arm - gap, y - t * 0.5f, arm, t), white);
            GUI.DrawTexture(new Rect(x + gap, y - t * 0.5f, arm, t), white);
            GUI.DrawTexture(new Rect(x - t * 0.5f, y - arm - gap, t, arm), white);
            GUI.DrawTexture(new Rect(x - t * 0.5f, y + gap, t, arm), white);
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
