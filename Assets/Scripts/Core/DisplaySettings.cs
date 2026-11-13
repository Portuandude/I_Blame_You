using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 화면 설정 (2026-11-12 플레이테스트 21번): 화면 비율 16:9 고정(레터박스), 해상도, 창 / 테두리 없는 창 / 전체 화면.
    // PlayerPrefs에 저장하고, 실행 뒤 처음 시작 화면에서(SaveData.LoadSettings) 한 번 적용한다. 계산 규칙은 Chunks/DisplayRules.
    public static class DisplaySettings
    {
        private const string KeyResolution = "Settings.Resolution";
        private const string KeyWindowMode = "Settings.WindowMode";

        public static int Resolution { get; private set; } = -1; // DisplayRules.Resolutions 번호
        public static int Mode { get; private set; } = DisplayRules.Borderless;

        // 모니터 해상도 (창 모드에서는 Screen.currentResolution이 모니터, Screen.width가 창 크기).
        public static int MonitorWidth => Display.main != null && Display.main.systemWidth > 0 ? Display.main.systemWidth : Screen.currentResolution.width;
        public static int MonitorHeight => Display.main != null && Display.main.systemHeight > 0 ? Display.main.systemHeight : Screen.currentResolution.height;

        private static bool loaded;

        // 실행마다 한 번만 (시작 화면으로 돌아올 때마다 다시 적용하면 창이 깜빡이거나 손으로 바꾼 크기가 되돌아간다).
        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            int saved = PlayerPrefs.GetInt(KeyResolution, -1);
            if (saved < 0) saved = System.Array.IndexOf(DisplayRules.Resolutions, (Screen.width, Screen.height)); // 처음 실행: 지금 창 크기가 목록에 있으면 그것을 보여 준다
            Resolution = DisplayRules.Sanitize(saved, MonitorWidth, MonitorHeight);
            int defaultMode = Screen.fullScreenMode == FullScreenMode.Windowed ? DisplayRules.Windowed
                : Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen ? DisplayRules.Fullscreen : DisplayRules.Borderless;
            Mode = DisplayRules.SanitizeMode(PlayerPrefs.GetInt(KeyWindowMode, defaultMode));
            if (PlayerPrefs.HasKey(KeyResolution)) Apply(); // 처음 실행(저장 없음)은 Unity 기본 화면을 그대로 둔다
            EnsureLetterbox();
        }

        public static void Set(int resolution, int mode)
        {
            Resolution = DisplayRules.Sanitize(resolution, MonitorWidth, MonitorHeight);
            Mode = DisplayRules.SanitizeMode(mode);
            PlayerPrefs.SetInt(KeyResolution, Resolution);
            PlayerPrefs.SetInt(KeyWindowMode, Mode);
            PlayerPrefs.Save();
            Apply();
        }

        private static void Apply()
        {
            if (Application.isEditor) return; // 에디터 Game 창 크기는 바꿀 수 없다 (레터박스만 적용)
            var r = DisplayRules.Resolutions[Resolution];
            Screen.SetResolution(r.width, r.height, ToUnity(Mode));
        }

        private static FullScreenMode ToUnity(int mode)
        {
            switch (mode)
            {
                case DisplayRules.Windowed: return FullScreenMode.Windowed;
                case DisplayRules.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        private static void EnsureLetterbox()
        {
            if (Object.FindAnyObjectByType<AspectLetterbox>() != null) return;
            var go = new GameObject("AspectLetterbox");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AspectLetterbox>();
        }
    }

    // 창 비율이 16:9가 아니면 메인 카메라 영역을 16:9로 줄여 가운데 두고, 남는 곳은 검은 띠로 칠한다.
    // 검은 띠: 아무것도 그리지 않는 뒤 카메라(깊이 -100)가 화면 전체를 검게 지운다. IMGUI(HUD)는 창 전체 기준 그대로.
    public class AspectLetterbox : MonoBehaviour
    {
        private Camera backdrop;
        private int lastWidth = -1;
        private int lastHeight = -1;
        private Camera lastCamera;

        private void Awake()
        {
            backdrop = gameObject.AddComponent<Camera>();
            backdrop.depth = -100f;
            backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = Color.black;
            backdrop.orthographic = true;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || cam == backdrop) return;
            if (cam == lastCamera && Screen.width == lastWidth && Screen.height == lastHeight) return;
            lastCamera = cam;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            var box = DisplayRules.Letterbox(Screen.width, Screen.height);
            cam.rect = new Rect(box.x, box.y, box.width, box.height);
            if (cam.depth <= backdrop.depth) cam.depth = backdrop.depth + 1f;
        }
    }
}

namespace IBlameYou.Core
{
    // 화면 설정 창 (IMGUI). 시작 화면과 일시정지 메뉴가 같은 창을 쓴다. ◀ ▶로 해상도, 버튼으로 창 모드를 고르고 "적용".
    public class DisplaySettingsWindow
    {
        private int resolution;
        private int mode;

        public bool IsOpen { get; private set; }

        public void Show()
        {
            IsOpen = true;
            resolution = DisplaySettings.Resolution;
            mode = DisplaySettings.Mode;
        }

        public void Close() => IsOpen = false;

        public void Draw(GUIStyle panel, GUIStyle body, GUIStyle button)
        {
            if (!IsOpen) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
                return;
            }

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            const float width = 440f;
            const float height = 300f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(10f, (Screen.height - height) / 2f), width, height);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게
            var head = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            var center = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 30f), "화면 설정", head);

            var available = DisplayRules.Available(DisplaySettings.MonitorWidth, DisplaySettings.MonitorHeight);
            int at = Mathf.Max(0, available.IndexOf(resolution));
            float y = rect.y + 56f;
            GUI.Label(new Rect(rect.x + 24f, y, 90f, 36f), "해상도", body);
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 120f, y, 44f, 36f), "◀", button)) at = Mathf.Max(0, at - 1);
            GUI.Label(new Rect(rect.x + 164f, y, 184f, 36f), DisplayRules.ResolutionName(available[at]), center);
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 348f, y, 44f, 36f), "▶", button)) at = Mathf.Min(available.Count - 1, at + 1);
            resolution = available[at];

            y += 50f;
            float modeWidth = (rect.width - 48f - 16f) / 3f;
            for (int i = 0; i < DisplayRules.ModeNames.Length; i++)
            {
                string label = (i == mode ? "● " : "") + DisplayRules.ModeNames[i];
                if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 24f + i * (modeWidth + 8f), y, modeWidth, 44f), label, button)) mode = i;
            }

            y += 56f;
            GUI.Label(new Rect(rect.x + 24f, y, rect.width - 48f, 40f),
                "화면 비율은 16:9로 고정 (창 비율이 다르면 검은 띠). 테두리 없는 창은 모니터 해상도로 보일 수 있다. 에디터에서는 해상도·창 모드가 바뀌지 않는다",
                new GUIStyle(body) { fontSize = 13, alignment = TextAnchor.MiddleCenter });

            y += 50f;
            float half = (rect.width - 48f - 8f) / 2f;
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 24f, y, half, 44f), "적용", button))
            {
                DisplaySettings.Set(resolution, mode);
                Close();
            }
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 32f + half, y, half, 44f), "닫기 (Esc)", button)) Close();
        }
    }
}
