using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Net
{
    // 호스트: 판 도중 친구(P2) 연결이 끊기면 게임을 일시정지 메뉴로 멈추고, 메뉴 위에 안내 한 줄을 띄운다.
    //   "친구 연결이 끊겼어요" → 친구가 다시 들어오면 "다시 들어왔어요! 계속하기를 누르세요".
    //   계속하기를 누르면 혼자 이어 한다 (친구가 없으면 P2 입력은 이 컴퓨터 마우스로 돌아온다).
    // 바로 멈출 수 없는 때(영창·마법진 중, 유물·장비·마을 창 등)에는 멈출 수 있게 되는 첫 프레임에 멈춘다.
    // 안내 줄은 UnityEngine.GUI로 그려서 친구 화면에는 보내지 않는다 (친구는 이미 끊겼다).
    public class NetPeerLossPause : MonoBehaviour
    {
        private bool pending;   // 끊겼는데 아직 멈추지 못했다
        private bool showNote;  // 이번 일시정지는 친구가 끊겨서 열렸다
        private bool rejoined;  // 멈춰 있는 동안 친구가 다시 들어왔다
        private SkillHUD hud;
        private GUIStyle noteStyle;

        private void Awake()
        {
            NetSession.PeerLeft += OnPeerLeft;
            NetSession.PeerJoined += OnPeerJoined;
        }

        private void OnDestroy()
        {
            NetSession.PeerLeft -= OnPeerLeft;
            NetSession.PeerJoined -= OnPeerJoined;
        }

        private void OnPeerLeft()
        {
            // 방을 스스로 닫았을 때(NetSession.Stop)는 멈추지 않는다
            if (!NetSession.IsHost) return;
            hud = FindAnyObjectByType<SkillHUD>();
            if (hud == null) return; // 판 밖 (시작 화면·도시)
            pending = true;
            rejoined = false;
        }

        private void OnPeerJoined()
        {
            pending = false; // 아직 못 멈췄으면 그대로 이어 간다
            if (showNote) rejoined = true;
        }

        private void Update()
        {
            if (showNote && (hud == null || !hud.Paused))
            {
                showNote = false; // 계속하기·다시 시작 등으로 창이 닫혔다
                rejoined = false;
            }
            if (!pending) return;
            if (hud == null || !NetSession.IsHost)
            {
                pending = false;
                return;
            }
            if (hud.TryPauseForLostPeer(out bool giveUp))
            {
                pending = false;
                showNote = true;
            }
            else if (giveUp) pending = false;
        }

        private void OnGUI()
        {
            if (!showNote || !NetSession.IsHost || Event.current.type != EventType.Repaint) return; // 방을 닫았으면 "친구 끊김" 안내는 맞지 않다
            if (noteStyle == null)
                noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold };
            GUI.depth = -1100;
            string text = rejoined
                ? $"<color=#8fe388>{NetSession.PeerName} 다시 들어왔어요!</color> 계속하기를 누르면 같이 이어 해요."
                : "<color=#ffa659>친구 연결이 끊겼어요.</color> 친구가 다시 들어오길 기다리거나, 계속하기로 혼자 이어 해요.";
            var rect = new Rect(0f, 14f, Screen.width, 34f);
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(Screen.width * 0.12f, rect.y - 4f, Screen.width * 0.76f, rect.height + 8f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, text, noteStyle);
        }
    }
}
