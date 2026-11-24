using UnityEngine;

namespace IBlameYou.Net
{
    // 친구(클라이언트) 화면: 게임을 돌리지 않고, 호스트가 보낸 그림·HUD·소리를 그대로 보여 준다.
    // 매 프레임 P2 입력(마우스 자리·버튼·휠·숫자줄·Space·Esc)을 호스트로 보낸다.
    // F10: 연결 끊고 시작 화면으로.
    public class NetClientView : MonoBehaviour
    {
        private static NetClientView instance;

        private readonly NetAssetReceiver assets = new NetAssetReceiver();
        private NetWorldView world;
        private HudReplay hud;
        private Camera viewCamera;
        private GameObject root;
        private readonly P2InputState input = new P2InputState();
        private bool unfocused; // 친구 창이 포커스를 잃은 동안 (Alt-Tab): 입력을 보내지 않는다
        private float lastHostMessage;
        private GUIStyle statusStyle;
        private GameObject hiddenCamera; // 친구 화면을 그리는 동안 꺼 둔 원래 카메라 (끝나면 다시 켠다)

        public static bool Active => instance != null;

        public static void Begin()
        {
            if (instance != null) return;
            var go = new GameObject("NetClientView");
            instance = go.AddComponent<NetClientView>();
        }

        public static void End()
        {
            if (instance == null) return;
            Destroy(instance.gameObject);
            instance = null;
        }

        private void Awake()
        {
            var existing = Camera.main;
            if (existing != null)
            {
                hiddenCamera = existing.gameObject; // 메뉴용 카메라가 있으면 끈다
                hiddenCamera.SetActive(false);
            }

            var camObject = new GameObject("Net Camera") { tag = "MainCamera" };
            camObject.transform.SetParent(transform, false);
            viewCamera = camObject.AddComponent<Camera>();
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = 6f;
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(0.165f, 0.13f, 0.31f);
            viewCamera.transform.position = new Vector3(0f, 0f, -10f);
            if (FindAnyObjectByType<AudioListener>() == null) camObject.AddComponent<AudioListener>();

            root = new GameObject("Net World");
            root.transform.SetParent(transform, false);
            world = new NetWorldView(assets, root.transform, viewCamera);
            hud = new HudReplay(assets);
            lastHostMessage = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            world?.Clear();
            assets.Clear();
            if (hiddenCamera != null) hiddenCamera.SetActive(true);
            if (instance == this) instance = null;
        }

        // NetSession이 클라이언트 모드에서 받은 메시지를 넘긴다
        public static void Receive(MsgType type, NetReader r)
        {
            if (instance == null) return;
            instance.Handle(type, r);
        }

        private void Handle(MsgType type, NetReader r)
        {
            lastHostMessage = Time.unscaledTime;
            switch (type)
            {
                case MsgType.ResetView:
                    world.Clear();
                    hud.Clear();
                    assets.Clear();
                    break;
                case MsgType.Texture: assets.ReadTexture(r); break;
                case MsgType.Sprite: assets.ReadSprite(r); break;
                case MsgType.World: world.Read(r); break;
                case MsgType.Hud: hud.Read(r); break;
                case MsgType.Audio: NetAudio.Play(r); break;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10) && NetSession.IsClient) // 다시 붙는 중(화면만 남김)에는 NetSession이 F10을 받는다
            {
                NetSession.Stop();
                return;
            }
            if (NetSession.IsClient && !unfocused) SendInput(); // 다시 붙는 중에는 호스트가 어차피 버리니 보내지 않는다 (송신 큐가 쌓이지 않게, 버그 점검 2026-11-24)
        }

        // 창이 포커스를 잃으면 Unity가 마우스 버튼을 뗀 것으로 보아, 호스트에서 그리던 마법진이 판정되고 다이얼이 발동하던 것 (버그 점검 2026-11-24).
        // 호스트 창의 같은 문제는 SpellCaster.OnApplicationFocus가 막지만 친구 P2는 빠져 있었다.
        // → 포커스를 잃는 순간 버튼은 누른 채로 두고 Esc(마법진·다이얼 취소)만 한 번 보낸 뒤, 돌아올 때까지 입력을 보내지 않는다.
        //   그 사이 뗀 것은 세지 않으므로 돌아와 보내는 상태에는 '뗌'이 없다 (누른 채 여부만 바뀐다).
        private void OnApplicationFocus(bool focused)
        {
            if (focused)
            {
                unfocused = false;
                return;
            }
            if (!NetSession.IsClient || unfocused) return;
            unfocused = true;
            if (P2Input.TryMap(KeyCode.Escape, out var esc))
            {
                input.Sequence++;
                input.KeyPresses[(int)esc]++;
                NetSession.Send(input.Write());
            }
        }

        private void SendInput()
        {
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);
            var mouse = Input.mousePosition;
            input.Sequence++;
            input.MouseX = Mathf.Clamp01(mouse.x / w);
            input.MouseY = Mathf.Clamp01(mouse.y / h);
            input.MouseInside = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= w && mouse.y <= h;
            for (int i = 0; i < P2InputState.ButtonCount; i++)
            {
                if (Input.GetMouseButtonDown(i)) input.ButtonPresses[i]++;
                if (Input.GetMouseButtonUp(i)) input.ButtonReleases[i]++;
                input.ButtonHeld[i] = Input.GetMouseButton(i);
            }
            input.WheelTotal += Input.mouseScrollDelta.y;
            for (int i = 0; i < P2InputState.KeyCount; i++)
            {
                var key = P2Input.KeyCodes[i];
                bool down = Input.GetKeyDown(key) || (key == KeyCode.Return && Input.GetKeyDown(KeyCode.KeypadEnter));
                if (down) input.KeyPresses[i]++;
                input.KeyHeld[i] = Input.GetKey(key) || (key == KeyCode.Return && Input.GetKey(KeyCode.KeypadEnter));
            }
            NetSession.Send(input.Write());
        }

        private void OnGUI()
        {
            hud.Draw();

            if (Event.current.type != EventType.Repaint) return;
            if (statusStyle == null)
                statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.LowerRight, richText = true };
            float silent = Mathf.Max(Time.unscaledTime - lastHostMessage, NetSession.PeerSilentSeconds);
            string line = NetSession.Reconnecting
                ? "<color=#ffa659>호스트와 끊김 — 다시 연결하는 중</color> · F10 그만두기"
                : silent > 2f
                ? $"<color=#ffa659>호스트 대답 없음 {silent:0}초</color> ({PingTracker.Timeout:0}초면 끊김) · F10 연결 끊기"
                : $"<color=#ffa659>P2</color> 온라인 · {LatencyText(NetSession.RttMs)}{NetSession.TrafficText()} · F10 연결 끊기";
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(0f, Screen.height - 22f, Screen.width - 8f, 20f), line, statusStyle);
            GUI.color = previous;
        }

        // "지연 45ms" (빠르면 초록, 보통 노랑, 느리면 빨강). 호스트 화면 표시도 같이 쓴다.
        public static string LatencyText(int rttMs)
        {
            if (rttMs < 0) return "지연 재는 중";
            string color = rttMs < 80 ? "#8fe388" : rttMs < 150 ? "#fff3a0" : "#ff7b6b";
            return $"<color={color}>지연 {rttMs}ms</color>";
        }
    }
}
