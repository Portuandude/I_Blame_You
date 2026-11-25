using System;
using IBlameYou.Player;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // HUD를 온라인 친구 화면에도 (Net/NetGUI)

namespace IBlameYou.Core
{
    // P1(플레이어 A) 조작 키 창 (IMGUI, 2026-11-23 스팀 출시 준비). 시작 화면과 일시정지 메뉴가 같은 창을 쓴다. 화면 설정 창과 같은 모양·조작.
    // - 줄을 누르면(클릭·Enter·Space) "키를 누르세요"로 바뀌고, 다음에 누른 키보드 키가 그 동작의 키가 된다. Esc는 기다리기만 취소.
    // - 다른 동작이 쓰던 키면 두 동작의 키를 맞바꾼다. 쓸 수 없는 키(Esc·숫자줄·F키·Tab·C·R·M·`)는 Fizzle + 흔들림.
    // - 바꿀 때마다 바로 저장 (KeyBindings → PlayerPrefs). "기본값"은 처음 키로.
    // - 키보드 고르기는 ↑↓(W·S)·Enter·Space (바꾼 키와 상관없이 메뉴 키는 그대로). 창을 연 프레임의 키와 열 때 누르던 Enter·Space는 무시.
    // 새 키는 Update처럼 Input.GetKeyDown으로 훑는다 (IMGUI 이벤트는 Shift·Ctrl만 눌렀을 때 keyCode가 안 오는 경우가 있다).
    public class KeyBindingsWindow
    {
        private readonly DerivedStyle headStyle = new DerivedStyle();
        private readonly DerivedStyle noteStyle = new DerivedStyle();
        private readonly DerivedStyle keyStyle = new DerivedStyle();
        private const float OpenFade = 0.2f;
        private const float OpenPop = 0.22f;
        private const float OpenScale = 0.9f;
        private const float BumpTime = 0.3f;
        private const float NoticeTime = 2.2f;

        private const int ActionRows = KeyBindingRules.ActionCount;
        private const int RowReset = ActionRows;
        private const int RowClose = ActionRows + 1;
        private const int RowCount = ActionRows + 2;

        private static KeyCode[] captureKeys; // 훑을 키보드 키 (마우스·패드 제외)

        private float openedAt = -10f;
        private int openedFrame = -1;
        private bool keyboard;
        private int selection;
        private bool activateHeld;
        private int waiting = -1;          // 키를 기다리는 동작 (-1 = 없음)
        private int waitingFrame = -1;     // 기다리기 시작한 프레임 (그 프레임의 키는 새 키로 잡지 않는다)
        private int capturedFrame = -1;    // 새 키를 잡은 프레임 (그 키가 메뉴 Enter·Space로 새지 않게)
        private int escapeFrame = -1;      // Esc를 처리한 프레임 (Update와 OnGUI가 같은 Esc를 두 번 보지 않게)
        private int tickFrame = -1;
        private float bumpAt = -10f;
        private int bumpRow = -1;
        private float noticeAt = -10f;
        private string notice;

        public bool IsOpen { get; private set; }
        public bool Waiting => waiting >= 0;

        public void Show()
        {
            IsOpen = true;
            openedAt = Time.unscaledTime;
            openedFrame = Time.frameCount;
            keyboard = false;
            selection = 0;
            activateHeld = MenuActivateHeld();
            waiting = -1;
            noticeAt = -10f;
            bumpAt = -10f;
            GameAudio.PlaySfx("PauseOpen", 0.5f, 0.03f);
        }

        public void Close(bool playSound = false)
        {
            if (!IsOpen) return;
            if (playSound) GameAudio.PlaySfx("PauseClose", 0.6f);
            IsOpen = false;
            waiting = -1;
        }

        // Esc 한 번: 키를 기다리는 중이면 기다리기만 취소, 아니면 창을 닫는다. 한 프레임에 한 번만 (일시정지 Update와 창 OnGUI가 같은 Esc를 본다).
        public void HandleEscape()
        {
            if (!IsOpen || Time.frameCount == escapeFrame) return;
            escapeFrame = Time.frameCount;
            if (Waiting)
            {
                waiting = -1;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
            }
            else Close(true);
        }

        private static bool MenuActivateHeld() =>
            Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);

        public void Draw(GUIStyle panel, GUIStyle body, GUIStyle button)
        {
            if (!IsOpen) return;
            Tick();
            if (!IsOpen) return;
            HandleKeys(Event.current);
            if (!IsOpen) return;

            float now = Time.unscaledTime;
            float open = now - openedAt;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01(open / OpenFade));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            const float width = 460f;
            const float rowHeight = 34f;
            const float rowGap = 4f;
            float height = 96f + ActionRows * (rowHeight + rowGap) + 64f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(10f, Mathf.Min((Screen.height - height) / 2f, Screen.height - height - 10f)), width, height); // 낮은 창에서도 [닫기]가 보이게 아래도 맞춘다
            var previousMatrix = GUI.matrix;
            float pop = PopScale(open);
            if (pop != 1f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), rect.center);

            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);
            var head = headStyle.Get(body, s => { s.alignment = TextAnchor.MiddleCenter; s.fontSize = 22; s.fontStyle = FontStyle.Bold; });
            var note = noteStyle.Get(body, s => { s.fontSize = 13; s.alignment = TextAnchor.MiddleCenter; });
            var keyLabel = keyStyle.Get(button, s => { s.fontStyle = FontStyle.Bold; });
            GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 30f), "조작 키 (P1 · A)", head);

            previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            bool showNotice = notice != null && now - noticeAt < NoticeTime;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 42f, rect.width - 24f, 40f),
                showNotice ? notice
                : Waiting ? $"{KeyBindingRules.ActionName((P1Action)waiting)}에 쓸 키를 누르세요 (Esc 취소)"
                : "줄을 누르고 새 키를 누른다. 다른 동작이 쓰던 키면 서로 맞바꾼다. P2(B)는 마우스라 바꿀 것이 없다",
                note);
            GUI.color = previous;

            float y = rect.y + 88f;
            float labelWidth = 200f;
            for (int i = 0; i < ActionRows; i++)
            {
                var action = (P1Action)i;
                GUI.Label(new Rect(rect.x + 24f, y, labelWidth, rowHeight), KeyBindingRules.ActionName(action), body);
                var keyRect = new Rect(rect.x + 24f + labelWidth + BumpOffset(i, now), y, rect.width - 48f - labelWidth, rowHeight);
                if (ShowSelection(i) || waiting == i) CuteButton.DrawSelection(keyRect);
                string text = waiting == i ? (Mathf.FloorToInt(now * 3f) % 2 == 0 ? "키를 누르세요…" : "") : KeyBindings.Name(action);
                if (CuteButton.Button(keyRect, text, keyLabel) && Time.frameCount != capturedFrame) BeginWaiting(i);
                y += rowHeight + rowGap;
            }

            y += 10f;
            float half = (rect.width - 48f - 8f) / 2f;
            var resetRect = new Rect(rect.x + 24f, y, half, 44f);
            var closeRect = new Rect(rect.x + 32f + half, y, half, 44f);
            if (ShowSelection(RowReset)) CuteButton.DrawSelection(resetRect);
            if (ShowSelection(RowClose)) CuteButton.DrawSelection(closeRect);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !KeyBindings.IsDefault;
            if (CuteButton.Button(resetRect, "기본값", button)) ResetDefaults();
            GUI.enabled = enabled;
            if (CuteButton.Button(closeRect, "닫기 (Esc)", button)) Close(true);

            GUI.matrix = previousMatrix;
        }

        private bool ShowSelection(int row) => keyboard && !Waiting && selection == row;

        private void BeginWaiting(int row)
        {
            waiting = row;
            waitingFrame = Time.frameCount;
            notice = null;
            GameAudio.PlaySfx("UiClick", 0.6f, 0.04f);
        }

        // 프레임마다 한 번: 기다리는 중이면 이번 프레임에 누른 키보드 키를 새 키로 잡는다.
        private void Tick()
        {
            if (tickFrame == Time.frameCount) return;
            tickFrame = Time.frameCount;
            if (!Waiting || Time.frameCount == waitingFrame || !Input.anyKeyDown) return;
            if (captureKeys == null) captureKeys = BuildCaptureKeys();

            foreach (var key in captureKeys)
            {
                if (!Input.GetKeyDown(key)) continue;
                if (key == KeyCode.Escape) return; // Esc는 HandleEscape (취소)
                Capture(key);
                return;
            }
        }

        private void Capture(KeyCode key)
        {
            var action = (P1Action)waiting;
            var result = KeyBindings.Set(action, key, out P1Action swapped);
            if (result == KeyBindingRules.AssignResult.Forbidden)
            {
                bumpAt = Time.unscaledTime;
                bumpRow = waiting;
                notice = $"{key}는(은) 다른 기능에 쓰는 키라 고를 수 없다 (Esc·숫자줄·F키·Tab·C·R·M·`)";
                noticeAt = Time.unscaledTime;
                GameAudio.PlaySfx("Fizzle", 0.4f, 0.04f);
                return; // 계속 기다린다
            }

            waiting = -1;
            capturedFrame = Time.frameCount;
            activateHeld = MenuActivateHeld(); // 새 키가 Enter·Space면 뗄 때까지 메뉴 누르기로 치지 않는다
            if (result == KeyBindingRules.AssignResult.Swapped)
            {
                notice = $"{KeyBindingRules.ActionName(action)} ↔ {KeyBindingRules.ActionName(swapped)} 키를 맞바꿨다";
                noticeAt = Time.unscaledTime;
            }
            GameAudio.PlaySfx("RelicSlot", 0.45f, 0.03f);
        }

        private void ResetDefaults()
        {
            KeyBindings.ResetDefaults();
            notice = "기본 키로 되돌렸다";
            noticeAt = Time.unscaledTime;
            GameAudio.PlaySfx("RelicSlot", 0.45f, 0.03f);
        }

        private static KeyCode[] BuildCaptureKeys()
        {
            var all = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            var list = new System.Collections.Generic.List<KeyCode>();
            foreach (var key in all)
            {
                int code = (int)key;
                if (code < KeyBindingRules.MinKey || code >= KeyBindingRules.FirstMouseOrJoystick) continue;
                if (!list.Contains(key)) list.Add(key);
            }
            return list.ToArray();
        }

        private void HandleKeys(Event e)
        {
            bool activateKey = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space;
            if (e.type == EventType.KeyUp && activateKey) activateHeld = false;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;

            if (e.keyCode == KeyCode.Escape)
            {
                HandleEscape();
                e.Use();
                return;
            }
            if (Waiting || Time.frameCount == capturedFrame || Time.frameCount == openedFrame)
            {
                e.Use(); // 기다리는 중의 키는 Tick이 잡는다 (메뉴 이동으로 쓰지 않는다)
                return;
            }

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

            if ((e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.A || e.keyCode == KeyCode.D)
                && keyboard && selection >= RowReset)
            {
                selection = selection == RowReset ? RowClose : RowReset;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                e.Use();
                return;
            }

            if (activateKey)
            {
                e.Use();
                if (activateHeld || !keyboard) return;
                activateHeld = true;
                if (selection < ActionRows) BeginWaiting(selection);
                else if (selection == RowReset)
                {
                    if (!KeyBindings.IsDefault) ResetDefaults();
                }
                else
                {
                    GameAudio.PlaySfx("UiClick", 0.7f, 0.04f);
                    Close(true);
                }
            }
        }

        private static float PopScale(float t)
        {
            if (t >= OpenPop) return 1f;
            float k = Mathf.Clamp01(t / OpenPop) - 1f;
            const float s = 1.70158f;
            float back = 1f + k * k * ((s + 1f) * k + s);
            return OpenScale + (1f - OpenScale) * back;
        }

        private float BumpOffset(int row, float now)
        {
            if (row != bumpRow) return 0f;
            float t = now - bumpAt;
            if (t < 0f || t >= BumpTime) return 0f;
            return 6f * (1f - t / BumpTime) * Mathf.Sin(t * 40f);
        }
    }
}
