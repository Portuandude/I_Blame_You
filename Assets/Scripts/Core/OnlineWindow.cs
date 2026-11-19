using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using IBlameYou.Net;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // HUD를 온라인 친구 화면에도 (Net/NetGUI)

namespace IBlameYou.Core
{
    // 시작 화면의 "온라인 2인" 창 (Docs/OnlinePlay.md). 호스트 방식:
    //   호스트 하기 = 내 컴퓨터가 게임을 돌리고 P1(이동)을 맡는다. 방을 연 뒤 평소처럼 시작한다. 친구는 언제 들어와도 된다.
    //   참가하기   = 친구의 방에 P2(공격)로 들어간다. 호스트 IP를 넣는다.
    // 지금 연결은 직접 IP(TCP)라 같은 집 네트워크, 또는 Radmin VPN·ZeroTier 같은 가상 LAN, 또는 호스트 쪽 포트 열기가 필요하다.
    // 스팀 출시 때 스팀 친구 초대로 바꾼다.
    public class OnlineWindow
    {
        private const string AddressKey = "NetLastAddress";
        private const string PortKey = "NetLastPort";

        private string address;
        private string portText;
        private List<string> localAddresses;
        private int openedFrame = -1;
        private float copiedAt = -10f;
        private string addressError;

        public bool IsOpen { get; private set; }

        public void Show()
        {
            IsOpen = true;
            openedFrame = Time.frameCount;
            address = PlayerPrefs.GetString(AddressKey, "127.0.0.1");
            portText = PlayerPrefs.GetInt(PortKey, NetProtocol.DefaultPort).ToString();
            localAddresses = null;
            addressError = null;
            GameAudio.PlaySfx("PauseOpen", 0.5f, 0.03f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GameAudio.PlaySfx("PauseClose", 0.6f);
        }

        // 메뉴 버튼에 붙일 한 줄 (방을 열었거나 접속 중일 때)
        public static string ButtonLabel()
        {
            if (NetSession.Reconnecting)
                return $"온라인 2인 — 호스트에 다시 연결하는 중 ({NetSession.ReconnectAttempt}/{ReconnectPlan.MaxAttempts}) · F10 그만두기";
            switch (NetSession.CurrentMode)
            {
                case NetSession.Mode.Hosting:
                    return NetSession.RemoteP2Active ? $"온라인 2인 — {NetSession.PeerName} 접속 (P2)" : $"온라인 2인 — 친구 기다리는 중 (포트 {NetSession.Port})";
                case NetSession.Mode.Joining:
                    return "온라인 2인 — 연결하는 중...";
                default:
                    return "온라인 2인 (호스트 하기 / 참가하기)";
            }
        }

        private int Port()
        {
            return int.TryParse(portText, out int p) && p > 0 && p < 65536 ? p : NetProtocol.DefaultPort;
        }

        public void Draw(GUIStyle panel, GUIStyle body, GUIStyle button)
        {
            if (!IsOpen) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && Time.frameCount != openedFrame)
            {
                e.Use();
                Close();
                return;
            }

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            const float width = 560f;
            const float height = 420f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(10f, (Screen.height - height) / 2f), width, height);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);
            var head = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            var note = new GUIStyle(body) { fontSize = 13, wordWrap = true, richText = true };
            var field = new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 30f), "온라인 2인 (컴퓨터 두 대)", head);

            float x = rect.x + 24f;
            float inner = rect.width - 48f;
            float y = rect.y + 52f;
            var mode = NetSession.CurrentMode;

            if (mode == NetSession.Mode.Hosting)
            {
                GUI.Label(new Rect(x, y, inner, 44f), $"<b>방을 열었어요.</b> 나는 <color=#73d9ff>P1</color> (키보드: 이동·순간이동). 친구는 <color=#ffa659>P2</color> (마우스: 공격).", note);
                y += 48f;
                GUI.Label(new Rect(x, y, inner, 22f), "친구에게 알려 줄 주소 (포트 " + NetSession.Port + "):", note);
                y += 24f;
                if (localAddresses == null) localAddresses = LocalAddresses();
                string list = localAddresses.Count > 0 ? string.Join("   ", localAddresses) : "(찾지 못함)";
                bool canCopy = localAddresses.Count > 0;
                float labelWidth = canCopy ? inner - 150f : inner;
                GUI.Label(new Rect(x, y, labelWidth, 44f), "<b>" + list + "</b>", note);
                if (canCopy)
                {
                    // 메신저에 바로 붙여 넣게: 첫 주소:포트 (가상 LAN이면 그 주소가 목록에 같이 보인다 — 다른 것을 알려 줘도 된다)
                    bool justCopied = Time.unscaledTime - copiedAt < 1.5f;
                    if (CuteButton.Button(new Rect(x + inner - 140f, y, 140f, 36f), justCopied ? "복사했어요!" : "주소 복사", button))
                    {
                        GUIUtility.systemCopyBuffer = localAddresses[0] + ":" + NetSession.Port;
                        copiedAt = Time.unscaledTime;
                        GameAudio.PlaySfx("PauseOpen", 0.4f, 0.05f);
                    }
                }
                y += 48f;
                GUI.Label(new Rect(x, y, inner, 70f),
                    "같은 집 네트워크가 아니면: 두 사람 모두 Radmin VPN 같은 가상 LAN에 들어가 그 주소를 쓰거나, 호스트 공유기에서 이 포트(TCP)를 열어 주세요. " +
                    "Windows 방화벽이 물으면 허용을 눌러 주세요.", note);
                y += 74f;
                GUI.Label(new Rect(x, y, inner, 40f), "<color=#fff3a0>" + NetSession.Status + "</color>", note);
                y += 46f;
                if (CuteButton.Button(new Rect(x, rect.yMax - 64f, inner * 0.5f - 6f, 44f), "방 닫기", button)) NetSession.Stop();
                if (CuteButton.Button(new Rect(x + inner * 0.5f + 6f, rect.yMax - 64f, inner * 0.5f - 6f, 44f), "닫고 게임 시작하기", button)) Close();
                return;
            }

            if (mode == NetSession.Mode.Joining)
            {
                GUI.Label(new Rect(x, y, inner, 60f), "<color=#fff3a0>" + NetSession.Status + "</color>", note);
                if (CuteButton.Button(new Rect(x, rect.yMax - 64f, inner, 44f), "취소", button)) NetSession.Stop();
                return;
            }

            // 꺼져 있음: 호스트 하기 / 참가하기
            GUI.Label(new Rect(x, y, inner, 44f),
                "한 사람이 방을 열고(호스트, <color=#73d9ff>P1</color> 이동), 다른 사람이 들어온다(<color=#ffa659>P2</color> 공격). 두 사람 모두 같은 빌드여야 해요.", note);
            y += 52f;

            GUI.Label(new Rect(x, y, 70f, 34f), "포트", body);
            portText = GUI.TextField(new Rect(x + 70f, y, 120f, 34f), portText ?? "", 5, field);
            if (CuteButton.Button(new Rect(x + 210f, y - 2f, inner - 210f, 40f), "호스트 하기 (방 열기)", button))
            {
                PlayerPrefs.SetInt(PortKey, Port());
                NetSession.StartHost(Port());
                localAddresses = null;
            }
            y += 60f;

            GUI.Label(new Rect(x, y, 70f, 34f), "주소", body);
            address = GUI.TextField(new Rect(x + 70f, y, 260f, 34f), address ?? "", 64, field);
            if (CuteButton.Button(new Rect(x + 340f, y - 2f, inner - 340f, 40f), "참가하기", button))
            {
                // "주소:포트"(호스트 창 "주소 복사")도 받는다
                if (NetAddress.TryParse(address, Port(), out string target, out int joinPort))
                {
                    PlayerPrefs.SetString(AddressKey, (address ?? "").Trim());
                    PlayerPrefs.SetInt(PortKey, joinPort);
                    portText = joinPort.ToString();
                    NetSession.Join(target, joinPort);
                    addressError = null;
                }
                else addressError = "주소를 읽을 수 없어요. 192.168.0.12 또는 192.168.0.12:27777처럼 넣어 주세요.";
            }
            y += 50f;
            GUI.Label(new Rect(x, y, inner, 40f), "주소: 호스트 창에 보이는 숫자 (예: 192.168.0.12 또는 가상 LAN 주소, 호스트가 \"주소 복사\"로 보낸 192.168.0.12:27777도 그대로). 같은 컴퓨터로 시험하면 127.0.0.1.", note);
            y += 44f;
            if (!string.IsNullOrEmpty(addressError))
                GUI.Label(new Rect(x, y, inner, 40f), "<color=#ff7b6b>" + addressError + "</color>", note);
            else if (!string.IsNullOrEmpty(NetSession.Status))
                GUI.Label(new Rect(x, y, inner, 40f), "<color=#fff3a0>" + NetSession.Status + "</color>", note);

            if (CuteButton.Button(new Rect(x, rect.yMax - 64f, inner, 44f), "닫기 (Esc)", button)) Close();
        }

        private static List<string> LocalAddresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                    string text = ip.ToString();
                    if (!result.Contains(text)) result.Add(text);
                }
            }
            catch (System.Exception) { }
            return result;
        }
    }
}
