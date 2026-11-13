using System;
using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 장비 비교 카드 (Docs/EquipmentDesign.md 8절): 바닥의 장비(EquipmentPickup)에 닿으면 열린다.
    // 왼쪽 지금 장비, 오른쪽 새 장비, 능력치 차이를 초록(+)/빨강(-)으로. 1 = 바꿔 끼기, 2 = 두고 가기 (클릭도 됨).
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
            control.Locked = true;
            Time.timeScale = 0f;
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
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "장비", big);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 22f), "A와 B가 함께 쓰는 한 벌 · 1 바꿔 끼기 · 2 두고 가기",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            var oldStats = equipCardOld != null ? equipCardOld.Stats() : new List<StatValue>();
            var newStats = equipCardNew.Stats();
            var left = new Rect(rect.x + 20f, rect.y + 80f, cw, cardHeight);
            var right = new Rect(left.xMax + gap, left.y, cw, cardHeight);
            DrawEquipCardSide(left, "지금", equipCardOld, oldStats, null, 0);
            DrawEquipCardSide(right, "새 장비", equipCardNew, newStats, oldStats, 1);
            GUI.Label(new Rect(left.xMax, left.y + cardHeight / 2f - 16f, gap, 32f), "→", new GUIStyle(big) { alignment = TextAnchor.MiddleCenter });

            float by = rect.yMax - 48f;
            if (CuteButton.Button(new Rect(rect.center.x - 170f, by, 160f, 34f), "1. 바꿔 끼기", cardButton)) CloseEquipCard(true);
            else if (CuteButton.Button(new Rect(rect.center.x + 10f, by, 160f, 34f), "2. 두고 가기", cardButton)) CloseEquipCard(false);
        }

        // compare가 있으면 줄마다 지금 장비와의 차이를 붙인다 (새 장비에만 있는 능력치는 전부 +, 빠지는 능력치는 아래에 빨강으로).
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
                text.Append(EquipmentBook.Describe(s));
                if (compare != null)
                {
                    float diff = s.Value - ValueOf(compare, s.Stat);
                    if (Mathf.Abs(diff) > 0.0001f)
                        text.Append($"  <color={(diff > 0f ? "#7dff8a" : "#ff6b6b")}>({EquipmentBook.Describe(new StatValue(s.Stat, diff)).Substring(EquipmentBook.StatName(s.Stat).Length + 1)})</color>");
                }
                text.Append('\n');
            }

            if (compare != null)
                foreach (var s in compare)
                    if (!Has(stats, s.Stat))
                        text.Append($"<color=#ff6b6b>{EquipmentBook.Describe(new StatValue(s.Stat, -s.Value))}</color>\n");

            GUI.Label(new Rect(card.x + 10f, card.y + 62f, card.width - 20f, card.height - 70f), text.ToString(), small);
            GUI.matrix = previousMatrix;
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
