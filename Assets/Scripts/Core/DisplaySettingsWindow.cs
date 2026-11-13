using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 화면 설정 창 (IMGUI). 시작 화면과 일시정지 메뉴가 같은 창을 쓴다. ◀ ▶로 해상도, 버튼으로 창 모드를 고르고 "적용".
    // 2026-11-13 다듬기:
    // - 키보드: ↑↓(W·S)로 줄 고르기 (첫 화살표는 지금 줄을 보여 주기만), ←→(A·D)로 값 바꾸기, Enter·Space로 누르기.
    //   창을 연 그 프레임의 키와 열 때 누르고 있던 Enter·Space는 무시한다 (일시정지 메뉴에서 Enter로 열자마자 적용되지 않게).
    // - 열림: 배경 어둠이 서서히, 창이 0.9배에서 톡 커짐 + PauseOpen. 사람이 닫을 때만(Esc·닫기) PauseClose.
    // - 제목 아래 "지금: 해상도 · 창 모드", 지금 값과 다르게 고른 줄 왼쪽에 금색 점.
    // - 해상도 맨 끝이면 ◀/▶가 흐려지고, 키로 더 밀면 값 글자가 좌우로 흔들리며 Fizzle.
    // - 적용하면 "적용됨!" 금색 띠가 잠깐 보이고 닫힌다 (바뀐 것이 없으면 바로 닫힘). 그동안은 입력을 받지 않는다.
    // 시간은 모두 Time.unscaledTime (일시정지 중 timeScale 0).
    public class DisplaySettingsWindow
    {
        private const float OpenFade = 0.2f;      // 배경 어둠이 다 깔리는 시간
        private const float OpenPop = 0.22f;      // 창이 톡 커지는 시간
        private const float OpenScale = 0.9f;     // 처음 크기
        private const float BumpTime = 0.3f;      // 끝 값 흔들림
        private const float AppliedTime = 0.55f;  // "적용됨!" 띠가 보이는 시간 (그 뒤 닫힘)

        // 키보드 줄: 해상도 · 창 모드 · 적용 · 닫기
        private const int RowResolution = 0;
        private const int RowMode = 1;
        private const int RowApply = 2;
        private const int RowClose = 3;
        private const int RowCount = 4;

        private int resolution;
        private int mode;

        private float openedAt = -10f;
        private int openedFrame = -1;
        private bool keyboard;        // 화살표를 한 번이라도 눌렀는지 (그 전엔 테두리를 그리지 않고 Enter도 받지 않는다)
        private int selection;
        private bool activateHeld;    // Enter·Space를 누른 채 (키 반복·창을 연 키가 새지 않게)
        private float bumpAt = -10f;
        private int bumpDir;
        private float appliedAt = -10f;

        public bool IsOpen { get; private set; }

        private bool Applying => appliedAt >= 0f;

        public void Show()
        {
            IsOpen = true;
            resolution = DisplaySettings.Resolution;
            mode = DisplaySettings.Mode;
            openedAt = Time.unscaledTime;
            openedFrame = Time.frameCount;
            keyboard = false;
            selection = RowResolution;
            activateHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
            bumpAt = -10f;
            appliedAt = -10f;
            GameAudio.PlaySfx("PauseOpen", 0.5f, 0.03f);
        }

        // playSound: 사람이 닫을 때(Esc·닫기)만 "통". 일시정지 메뉴가 같이 닫히거나 적용 뒤 닫힐 때는 조용히.
        public void Close(bool playSound = false)
        {
            if (!IsOpen) return;
            if (playSound) GameAudio.PlaySfx("PauseClose", 0.6f);
            IsOpen = false;
            appliedAt = -10f;
        }

        public void Draw(GUIStyle panel, GUIStyle body, GUIStyle button)
        {
            if (!IsOpen) return;
            float now = Time.unscaledTime;
            if (Applying && now - appliedAt >= AppliedTime)
            {
                Close();
                return;
            }

            var available = DisplayRules.Available(DisplaySettings.MonitorWidth, DisplaySettings.MonitorHeight);
            int at = Mathf.Max(0, available.IndexOf(resolution));
            HandleKeys(Event.current, available.Count, ref at);
            if (!IsOpen) return;
            resolution = available[at];

            float open = now - openedAt;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01(open / OpenFade));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            const float width = 440f;
            const float height = 324f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(10f, (Screen.height - height) / 2f), width, height);
            var previousMatrix = GUI.matrix;
            float pop = PopScale(open);
            if (pop != 1f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), rect.center);
            bool wasEnabled = GUI.enabled;
            if (Applying) GUI.enabled = false; // "적용됨!" 동안은 누를 수 없게

            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게
            var head = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            var center = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            var note = new GUIStyle(body) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 30f), "화면 설정", head);

            int current = DisplaySettings.Resolution;
            string currentText = current >= 0 && current < DisplayRules.Resolutions.Length ? DisplayRules.ResolutionName(current) : "?";
            previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(rect.x, rect.y + 40f, rect.width, 20f), $"지금: {currentText} · {DisplayRules.ModeNames[DisplaySettings.Mode]}", note);
            GUI.color = previous;

            // 해상도 줄
            float y = rect.y + 70f;
            if (resolution != DisplaySettings.Resolution) DrawChangedDot(rect.x + 12f, y + 18f);
            GUI.Label(new Rect(rect.x + 24f, y, 90f, 36f), "해상도", body);
            var left = new Rect(rect.x + 120f, y, 44f, 36f);
            var right = new Rect(rect.x + 348f, y, 44f, 36f);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && at > 0;
            if (CuteButton.Button(left, "◀", button)) at = Mathf.Max(0, at - 1);
            GUI.enabled = enabled && at < available.Count - 1;
            if (CuteButton.Button(right, "▶", button)) at = Mathf.Min(available.Count - 1, at + 1);
            GUI.enabled = enabled;
            resolution = available[at];
            var valueRect = new Rect(rect.x + 164f + BumpOffset(now), y, 184f, 36f);
            GUI.Label(valueRect, DisplayRules.ResolutionName(resolution), center);
            if (ShowSelection(RowResolution)) CuteButton.DrawSelection(new Rect(left.x, y, right.xMax - left.x, 36f));

            // 창 모드 줄
            y += 50f;
            if (mode != DisplaySettings.Mode) DrawChangedDot(rect.x + 12f, y + 22f);
            float modeWidth = (rect.width - 48f - 16f) / 3f;
            for (int i = 0; i < DisplayRules.ModeNames.Length; i++)
            {
                var modeRect = new Rect(rect.x + 24f + i * (modeWidth + 8f), y, modeWidth, 44f);
                if (ShowSelection(RowMode) && i == mode) CuteButton.DrawSelection(modeRect);
                string label = (i == mode ? "● " : "") + DisplayRules.ModeNames[i];
                if (CuteButton.Button(modeRect, label, button)) mode = i;
            }

            y += 56f;
            GUI.Label(new Rect(rect.x + 24f, y, rect.width - 48f, 40f),
                "화면 비율은 16:9로 고정 (창 비율이 다르면 검은 띠). 테두리 없는 창은 모니터 해상도로 보일 수 있다. 에디터에서는 해상도·창 모드가 바뀌지 않는다",
                note);

            y += 50f;
            float half = (rect.width - 48f - 8f) / 2f;
            var applyRect = new Rect(rect.x + 24f, y, half, 44f);
            var closeRect = new Rect(rect.x + 32f + half, y, half, 44f);
            if (ShowSelection(RowApply)) CuteButton.DrawSelection(applyRect);
            if (ShowSelection(RowClose)) CuteButton.DrawSelection(closeRect);
            if (CuteButton.Button(applyRect, "적용", button)) Apply();
            if (CuteButton.Button(closeRect, "닫기 (Esc)", button)) Close(true);

            GUI.enabled = wasEnabled;
            if (Applying) DrawApplied(rect, now - appliedAt);
            GUI.matrix = previousMatrix;
        }

        private bool ShowSelection(int row) => keyboard && !Applying && selection == row;

        // 키보드 처리. at = 지금 고른 해상도의 available 안 번호.
        private void HandleKeys(Event e, int count, ref int at)
        {
            if (e.type == EventType.KeyUp && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space))
                activateHeld = false;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;

            if (Applying)
            {
                e.Use(); // 닫히는 중엔 키를 삼킨다 (Esc로 일시정지 메뉴까지 닫히지 않게)
                return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
                Close(true);
                e.Use();
                return;
            }

            if (Time.frameCount == openedFrame) return; // 창을 연 그 키

            int vertical = 0;
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W) vertical = -1;
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S) vertical = 1;
            if (vertical != 0)
            {
                if (keyboard) selection = (selection + vertical + RowCount) % RowCount;
                keyboard = true;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                e.Use();
                return;
            }

            int horizontal = 0;
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.A) horizontal = -1;
            else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.D) horizontal = 1;
            if (horizontal != 0)
            {
                e.Use();
                if (!keyboard)
                {
                    keyboard = true; // 첫 키는 지금 줄을 보여 주기만
                    GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                    return;
                }
                switch (selection)
                {
                    case RowResolution:
                        int next = at + horizontal;
                        if (next < 0 || next >= count)
                        {
                            bumpAt = Time.unscaledTime;
                            bumpDir = horizontal;
                            GameAudio.PlaySfx("Fizzle", 0.4f, 0.04f);
                        }
                        else
                        {
                            at = next;
                            GameAudio.PlaySfx("UiClick", 0.5f, 0.04f);
                        }
                        break;
                    case RowMode:
                        mode = (mode + horizontal + DisplayRules.ModeNames.Length) % DisplayRules.ModeNames.Length;
                        GameAudio.PlaySfx("UiClick", 0.5f, 0.04f);
                        break;
                    default:
                        selection = selection == RowApply ? RowClose : RowApply;
                        GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                        break;
                }
                return;
            }

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space)
            {
                e.Use();
                if (activateHeld || !keyboard) return; // 누른 채 키 반복이거나, 화살표로 고르기 전
                activateHeld = true;
                GameAudio.PlaySfx("UiClick", 0.7f, 0.04f);
                if (selection == RowClose) Close(true);
                else Apply(); // 해상도·창 모드 줄에서 Enter도 적용
            }
        }

        private void Apply()
        {
            if (Applying) return;
            bool changed = resolution != DisplaySettings.Resolution || mode != DisplaySettings.Mode;
            if (!changed)
            {
                Close();
                return;
            }
            DisplaySettings.Set(resolution, mode);
            appliedAt = Time.unscaledTime;
            GameAudio.PlaySfx("RelicSlot", 0.5f, 0.03f);
        }

        // 0.9배에서 살짝 넘쳤다가 1배로 (ease-out-back).
        private static float PopScale(float t)
        {
            if (t >= OpenPop) return 1f;
            float k = Mathf.Clamp01(t / OpenPop) - 1f;
            const float s = 1.70158f;
            float back = 1f + k * k * ((s + 1f) * k + s);
            return OpenScale + (1f - OpenScale) * back;
        }

        // 끝 값에서 더 밀었을 때 값 글자 좌우 흔들림 (민 쪽으로 먼저).
        private float BumpOffset(float now)
        {
            float t = now - bumpAt;
            if (t < 0f || t >= BumpTime) return 0f;
            float fade = 1f - t / BumpTime;
            return bumpDir * 6f * fade * Mathf.Sin(t * 40f);
        }

        private static void DrawChangedDot(float cx, float cy)
        {
            if (Event.current.type != EventType.Repaint) return;
            const float size = 8f;
            var previous = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
            GUI.DrawTexture(new Rect(cx - size / 2f, cy - size / 2f, size, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // "적용됨!" 금색 띠: 가운데서 좌우로 펼쳐지고, 끝무렵 흐려진다.
        private static void DrawApplied(Rect rect, float t)
        {
            if (Event.current.type != EventType.Repaint) return;
            float grow = Mathf.Clamp01(t / 0.12f);
            float alpha = 1f - Mathf.Clamp01((t - (AppliedTime - 0.15f)) / 0.15f);
            float w = rect.width * grow;
            var band = new Rect(rect.center.x - w / 2f, rect.center.y - 26f, w, 52f);
            var previous = GUI.color;
            GUI.color = new Color(0.25f, 0.12f, 0.2f, 0.92f * alpha);
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.3f, alpha);
            GUI.DrawTexture(new Rect(band.x, band.y, band.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(band.x, band.yMax - 2f, band.width, 2f), Texture2D.whiteTexture);
            if (grow >= 1f)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold
                };
                style.normal.textColor = new Color(1f, 0.85f, 0.3f, alpha);
                GUI.color = Color.white;
                GUI.Label(band, "적용됨!", style);
            }
            GUI.color = previous;
        }
    }
}
