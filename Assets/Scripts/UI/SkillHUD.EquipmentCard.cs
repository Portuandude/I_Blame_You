using System;
using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 장비 비교 카드 (Docs/EquipmentDesign.md 8절): 바닥의 장비(EquipmentPickup)에 닿으면 열린다.
    // 왼쪽 지금 장비, 오른쪽 새 장비, 능력치 차이를 초록(+)/빨강(-)으로. 1 = 바꿔 끼기, 2 = 두고 가기 (클릭도 됨).
    // 키보드: ←→·A/D로 두 버튼 중 하나를 고르고(첫 화살표는 "바꿔 끼기"를 보여 주기만) Enter·Space로 누른다 (유물 창과 같은 방식).
    // 열 때 "또롱"(PauseOpen)과 함께 배경 어둠이 0.15초에 깔린다.
    // 유물 창과 같은 방식으로 게임을 멈추고 조작을 잠근다. 영창·미니게임·다른 창이 열려 있으면 열지 않는다.
    public partial class SkillHUD
    {
        private bool equipCardOpen;
        private EquipmentItem equipCardNew;
        private EquipmentItem equipCardOld;
        private Action<bool> equipCardDecided;
        private int equipCardOpenedFrame;
        private float equipCardOpenedAt;
        private int equipCardUnlockFrame = -1;
        private bool equipCardKeyboard;
        private int equipCardChoice; // 0 = 바꿔 끼기, 1 = 두고 가기
        private const float EquipCardKeysDelay = 0.4f; // A/D·Space는 이동·점프 키라 카드가 다 뜨기 전엔 안 받는다
        private const float EquipCardFadeTime = 0.15f;

        private void InitializeEquipmentCard()
        {
            EquipmentPickup.OpenRequested = OpenEquipCard;
        }

        private void ShutdownEquipmentCard()
        {
            if (EquipmentPickup.OpenRequested == (Func<EquipmentItem, EquipmentItem, Action<bool>, bool>)OpenEquipCard) EquipmentPickup.OpenRequested = null;
        }

        private bool OpenEquipCard(EquipmentItem item, EquipmentItem current, Action<bool> decided)
        {
            if (item == null || equipCardOpen || equipCardUnlockFrame >= 0 || !CanOpenRelicWindow()) return false;
            equipCardOpen = true;
            equipCardNew = item;
            equipCardOld = current;
            equipCardDecided = decided;
            equipCardOpenedFrame = Time.frameCount;
            equipCardOpenedAt = Time.unscaledTime;
            equipCardKeyboard = false;
            equipCardChoice = 0;
            control.Locked = true;
            Time.timeScale = 0f;
            GameAudio.PlaySfx("PauseOpen", 0.6f, 0.03f);
            return true;
        }

        // take: 바꿔 끼기. keepLockThisFrame: 키로 고른 경우 그 키가 스킬 입력으로 새지 않게 잠금은 다음 프레임에 푼다.
        private void CloseEquipCard(bool take = false, bool keepLockThisFrame = false)
        {
            if (!equipCardOpen) return;
            equipCardOpen = false;
            var decided = equipCardDecided;
            equipCardNew = null;
            equipCardOld = null;
            equipCardDecided = null;
            Time.timeScale = 1f;
            if (keepLockThisFrame) equipCardUnlockFrame = Time.frameCount + 1;
            else if (!deathHandled && !stageCleared) control.Locked = false;
            decided?.Invoke(take);
        }

        private void UpdateEquipCardInput()
        {
            if (equipCardUnlockFrame >= 0 && Time.frameCount >= equipCardUnlockFrame)
            {
                equipCardUnlockFrame = -1;
                if (!deathHandled && !stageCleared && !paused && !traitWindowOpen && !relicWindowOpen) control.Locked = false;
            }

            if (!equipCardOpen || Time.frameCount <= equipCardOpenedFrame) return;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) CloseEquipCard(true, true);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) CloseEquipCard(false, true);
            else if (Time.unscaledTime - equipCardOpenedAt >= EquipCardKeysDelay)
            {
                bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
                bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
                if (left || right)
                {
                    if (equipCardKeyboard) equipCardChoice = right ? 1 : 0;
                    equipCardKeyboard = true;
                    GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                }
                else if (equipCardKeyboard && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
                {
                    GameAudio.PlaySfx("UiClick", 0.7f, 0.04f); // 마우스로 누를 때와 같은 소리
                    CloseEquipCard(equipCardChoice == 0, true);
                }
            }
        }

        private void DrawEquipCard()
        {
            if (!equipCardOpen || equipCardNew == null) return;

            const float cardWidth = 250f;
            const float cardHeight = 200f;
            const float gap = 40f;
            float width = Mathf.Min(cardWidth * 2f + gap + 40f, Screen.width - 32f);
            float cw = (width - 40f - gap) / 2f;
            float height = 80f + cardHeight + 60f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(16f, (Screen.height - height) / 2f), width, height);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((Time.unscaledTime - equipCardOpenedAt) / EquipCardFadeTime));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "장비", big);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 22f), equipCardKeyboard ? "A와 B가 함께 쓰는 한 벌 · ←→ 고르기 · Enter 누르기" : "A와 B가 함께 쓰는 한 벌 · 1 바꿔 끼기 · 2 두고 가기 · ←→ 고르기",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            var oldStats = equipCardOld != null ? equipCardOld.Stats() : new List<StatValue>();
            var newStats = equipCardNew.Stats();
            var left = new Rect(rect.x + 20f, rect.y + 80f, cw, cardHeight);
            var right = new Rect(left.xMax + gap, left.y, cw, cardHeight);
            DrawEquipCardSide(left, "지금", equipCardOld, oldStats, null, 0);
            DrawEquipCardSide(right, "새 장비", equipCardNew, newStats, oldStats, 1);
            GUI.Label(new Rect(left.xMax, left.y + cardHeight / 2f - 16f, gap, 32f), "→", new GUIStyle(big) { alignment = TextAnchor.MiddleCenter });

            float by = rect.yMax - 48f;
            var takeRect = new Rect(rect.center.x - 170f, by, 160f, 34f);
            var leaveRect = new Rect(rect.center.x + 10f, by, 160f, 34f);
            if (equipCardKeyboard) CuteButton.DrawSelection(equipCardChoice == 0 ? takeRect : leaveRect);
            if (CuteButton.Button(takeRect, "1. 바꿔 끼기", cardButton)) CloseEquipCard(true);
            else if (CuteButton.Button(leaveRect, "2. 두고 가기", cardButton)) CloseEquipCard(false);
        }

        // compare가 있으면 줄마다 지금 장비와의 차이를 붙인다 (새 장비에만 있는 능력치는 전부 +, 빠지는 능력치는 아래에).
        // 좋아지면 초록, 나빠지면 빨강 (마나 소모처럼 낮을수록 좋은 능력치는 거꾸로).
        private void DrawEquipCardSide(Rect card, string title, EquipmentItem item, List<StatValue> stats, List<StatValue> compare, int index)
        {
            var previousMatrix = BeginCardPop(card, equipCardOpenedAt, index);
            DrawCard(card, "공용");
            GUI.Label(new Rect(card.x + 10f, card.y + 8f, card.width - 20f, 20f), $"<color=#bbbbbb>{title}</color>", small);
            if (item == null)
            {
                GUI.Label(new Rect(card.x + 10f, card.y + 36f, card.width - 20f, 26f), $"<color=#8a8a8a>{(compare == null ? "없음" : "")}</color>", label);
                GUI.matrix = previousMatrix;
                return;
            }

            var icon = EquipmentIcon(item.Id);
            if (icon != null) GUI.DrawTexture(new Rect(card.xMax - 46f, card.y + 8f, 36f, 36f), icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(card.x + 10f, card.y + 30f, card.width - 60f, 26f), $"<b><color={GradeColor(item.Grade)}>{item.Name}</color></b>", label);

            var text = new System.Text.StringBuilder();
            foreach (var s in stats)
            {
                text.Append(StatLineText(s));
                if (compare != null)
                {
                    float diff = s.Value - ValueOf(compare, s.Stat);
                    if (Mathf.Abs(diff) > 0.0001f)
                        text.Append($"  <color={((diff > 0f) != EquipmentBook.LowerIsBetter(s.Stat) ? "#7dff8a" : "#ff6b6b")}>({EquipmentBook.Describe(new StatValue(s.Stat, diff)).Substring(EquipmentBook.StatName(s.Stat).Length + 1)})</color>");
                }
                text.Append('\n');
            }

            if (compare != null)
                foreach (var s in compare)
                    if (!Has(stats, s.Stat))
                        text.Append($"<color={((-s.Value > 0f) != EquipmentBook.LowerIsBetter(s.Stat) ? "#7dff8a" : "#ff6b6b")}>{EquipmentBook.Describe(new StatValue(s.Stat, -s.Value))}</color>\n");

            GUI.Label(new Rect(card.x + 10f, card.y + 62f, card.width - 20f, card.height - 70f), text.ToString(), small);
            GUI.matrix = previousMatrix;
        }

        // 능력치 한 줄 글. 수식어가 주는 마법 공격·마나 소모(EquipmentModifier, 장비 기본 능력치에는 없음)는 좋으면 초록, 나쁘면 빨강으로 (2026-11-14).
        // 상점 진열·비교 카드·일시정지 장비 줄이 같이 쓴다.
        private static string StatLineText(StatValue s)
        {
            string text = EquipmentBook.Describe(s);
            if (s.Stat != EquipStat.MagicPower && s.Stat != EquipStat.ManaCost) return text;
            bool good = (s.Value > 0f) != EquipmentBook.LowerIsBetter(s.Stat);
            return $"<color={(good ? "#7dff8a" : "#ff6b6b")}>{text}</color>";
        }

        private static float ValueOf(List<StatValue> stats, EquipStat stat)
        {
            foreach (var s in stats) if (s.Stat == stat) return s.Value;
            return 0f;
        }

        private static bool Has(List<StatValue> stats, EquipStat stat)
        {
            foreach (var s in stats) if (s.Stat == stat) return true;
            return false;
        }
    }
}
