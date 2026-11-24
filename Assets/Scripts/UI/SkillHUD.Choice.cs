using System;
using IBlameYou.Systems;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // HUD를 온라인 친구 화면에도 (Net/NetGUI)

namespace IBlameYou.UI
{
    // A·B 선택지 + 룰렛 창 (ChoiceRouletteRule, 2026-11-24 사람 결정). 게임을 멈추고, 아루(P1)는 숫자 키, 벨(P2)은 마우스로
    // 동시에 몰래 고른다 (누가 무엇을 골랐는지는 안 보이고 "골랐다"만). 같으면 그대로, 다르면 50:50 룰렛이 돌아 실제 행동을 정한다.
    // 선악도는 각자 고른 선택지대로 (SaveData.AddAlignment, 일반 모드만). 개발자 모드는 한 사람이 키로 아루 → 벨 순서로 고른다.
    // 지금은 기능 테스트 도구(F9 창)에서만 연다. NPC 방·선악도 방이 생기면 StageEvents.RaiseChoice로 부른다.
    public partial class SkillHUD
    {
        private bool choiceOpen;
        private string choiceTitle;
        private string[] choiceOptions;
        private int[] choiceAlignment;
        private Action<int> choiceDone;
        private int choiceA = -1, choiceB = -1, choiceResult = -1;
        private bool choiceSpun;
        private float choiceOpenedAt, choiceDecidedAt = -1f;
        private int choiceOpenedFrame;
        private readonly Rect[] choiceRects = new Rect[4];
        private const float ChoiceResultHold = 1.2f; // 결과를 보여 주고 닫기까지

        private void InitializeChoice() => StageEvents.ChoiceRequested = OpenChoice;

        private void ShutdownChoice()
        {
            if (StageEvents.ChoiceRequested == (Func<string, string[], int[], Action<int>, bool>)OpenChoice) StageEvents.ChoiceRequested = null;
        }

        private bool OpenChoice(string title, string[] options, int[] alignment, Action<int> done)
        {
            if (choiceOpen || options == null || options.Length < 2 || options.Length > choiceRects.Length || !CanOpenRelicWindow()) return false;
            choiceOpen = true;
            choiceTitle = title;
            choiceOptions = options;
            choiceAlignment = alignment;
            choiceDone = done;
            choiceA = choiceB = choiceResult = -1;
            choiceSpun = false;
            choiceDecidedAt = -1f;
            choiceOpenedAt = Time.unscaledTime;
            choiceOpenedFrame = Time.frameCount;
            control.Locked = true;
            Time.timeScale = 0f;
            GameAudio.PlaySfx("UiClick", 0.6f, 0f);
            return true;
        }

        private void CloseChoice(bool finished = false)
        {
            if (!choiceOpen) return;
            choiceOpen = false;
            var done = choiceDone;
            choiceDone = null;
            Time.timeScale = 1f;
            if (!deathHandled && !stageCleared) control.Locked = false;
            if (!finished) return; // 쓰러짐·클리어로 닫히면 행동도 선악도도 없던 일
            IBlameYou.Core.SaveData.AddAlignment("A", ChoiceRouletteRule.AlignmentDelta(choiceAlignment, choiceA));
            IBlameYou.Core.SaveData.AddAlignment("B", ChoiceRouletteRule.AlignmentDelta(choiceAlignment, choiceB));
            done?.Invoke(choiceResult);
        }

        private void UpdateChoiceInput()
        {
            if (!choiceOpen) return;
            if (choiceResult >= 0)
            {
                if (Time.unscaledTime - choiceDecidedAt >= (choiceSpun ? ChoiceRouletteRule.SpinSeconds : 0f) + ChoiceResultHold) CloseChoice(true);
                return;
            }
            if (Time.frameCount <= choiceOpenedFrame || Time.unscaledTime - choiceOpenedAt < 0.4f) return;
            int count = choiceOptions.Length;
            bool dev = IBlameYou.Core.GameSession.DevMode;
            bool remote = IBlameYou.Net.P2Input.Remote;

            // 아루(P1): 이 컴퓨터 숫자 키. 개발자 모드는 같은 키로 아루 다음 벨
            int key = 0;
            for (int i = 0; i < count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) key = i + 1;
            if (key > 0)
            {
                if (choiceA < 0) PickChoice(true, key - 1);
                else if (dev && choiceB < 0) PickChoice(false, key - 1);
            }

            // 벨(P2), 온라인: 친구 숫자 키·클릭 (친구 화면 버튼은 그림뿐)
            if (remote && choiceB < 0)
            {
                int number = PickRemoteNumber(count);
                if (number > 0) PickChoice(false, number - 1);
                else
                {
                    var click = PickRemoteClick();
                    if (click.HasValue)
                        for (int i = 0; i < count; i++)
                            if (choiceRects[i].Contains(click.Value)) { PickChoice(false, i); break; }
                }
            }
        }

        private void PickChoice(bool playerA, int index)
        {
            if (playerA) choiceA = index;
            else choiceB = index;
            GameAudio.PlaySfx("UiClick", 0.7f, 0.04f);
            if (choiceA < 0 || choiceB < 0) return;

            // 둘 다 골랐다: 행동은 같으면 그대로·다르면 룰렛 (선악도는 결과를 보여 주고 닫을 때 각자 고른 대로, CloseChoice)
            choiceResult = ChoiceRouletteRule.Resolve(choiceA, choiceB, UnityEngine.Random.value, out choiceSpun);
            choiceDecidedAt = Time.unscaledTime;
            if (choiceSpun) GameAudio.PlaySfx("ChestCreak", 0.5f, 0f); // 룰렛 도는 소리 (있는 소리)
        }

        private void DrawChoice()
        {
            if (!choiceOpen || choiceOptions == null) return;
            int count = choiceOptions.Length;
            float width = Mathf.Min(560f, Screen.width - 32f);
            float rowHeight = 46f;
            float height = 110f + count * (rowHeight + 8f) + 50f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(16f, (Screen.height - height) / 2f), width, height);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((Time.unscaledTime - choiceOpenedAt) / 0.15f));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), choiceTitle, big);

            bool dev = IBlameYou.Core.GameSession.DevMode;
            string aState = choiceA >= 0 ? "<color=#73d9ff>아루: 골랐다 ✓</color>" : (dev ? "<color=#73d9ff>아루: 숫자 키</color>" : "<color=#73d9ff>아루: 숫자 키로 몰래</color>");
            string bState = choiceB >= 0 ? "<color=#ffa659>벨: 골랐다 ✓</color>"
                : dev ? (choiceA >= 0 ? "<color=#ffa659>벨: 숫자 키·클릭</color>" : "<color=#ffa659>벨: 아루 다음</color>")
                : "<color=#ffa659>벨: 마우스로 몰래</color>";
            GUI.Label(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 22f),
                choiceResult >= 0 ? (choiceSpun ? "둘이 다르게 골랐다 — 룰렛!" : "둘이 같은 것을 골랐다") : $"{aState}  ·  {bState}", smallCenter);

            float t = Time.unscaledTime - choiceDecidedAt;
            int lit = -1; // 강조할 줄
            if (choiceResult >= 0)
            {
                if (choiceSpun)
                {
                    bool landsOnB = choiceResult == choiceB && choiceResult != choiceA;
                    lit = ChoiceRouletteRule.Pointer(t, landsOnB) == 0 ? choiceA : choiceB;
                }
                else lit = choiceResult;
            }

            for (int i = 0; i < count; i++)
            {
                var row = new Rect(rect.x + 20f, rect.y + 84f + i * (rowHeight + 8f), rect.width - 40f, rowHeight);
                choiceRects[i] = row;
                if (i == lit) CuteButton.DrawSelection(row);
                bool clickable = choiceResult < 0 && choiceB < 0 && (dev ? choiceA >= 0 : !IBlameYou.Net.P2Input.Remote)
                    && Time.frameCount > choiceOpenedFrame && Time.unscaledTime - choiceOpenedAt >= 0.4f; // 창이 열릴 때 누르고 있던 클릭으로 고르지 않게 (키와 같은 0.4초, 버그 점검 2026-11-24)
                // 버튼 글자에는 누가 골랐는지 넣지 않는다 (몰래 고르기). 결과가 나오면 아루·벨이 고른 것을 보여 준다
                string text = $"{i + 1}. {choiceOptions[i]}";
                if (choiceResult >= 0 && (t >= (choiceSpun ? ChoiceRouletteRule.SpinSeconds : 0f)))
                {
                    if (i == choiceA) text += "  <color=#73d9ff>[아루]</color>";
                    if (i == choiceB) text += "  <color=#ffa659>[벨]</color>";
                }
                if (CuteButton.Button(row, text, cardButton) && clickable) PickChoice(false, i); // 벨(P2)은 마우스 (온라인이면 호스트 마우스는 안 받음)
            }
        }
    }
}
