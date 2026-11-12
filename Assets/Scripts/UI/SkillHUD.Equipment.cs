using System.Collections.Generic;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.UI
{
    // 일시정지 요약의 장비 3칸 (Docs/EquipmentDesign.md 8절): 부위 아이콘 + 수식어 색 이름 + 능력치.
    // 그림: Tools/ArtGen/generate_equipment_icons.py → Resources/UI/HUD/Equipment/<EquipmentId>.png (24x24).
    public partial class SkillHUD
    {
        private const float EquipmentIconSize = 20f;

        private readonly Dictionary<EquipmentId, Texture2D> equipmentIcons = new Dictionary<EquipmentId, Texture2D>();
        private PlayerEquipment equipment;

        private PlayerEquipment Equipment
        {
            get
            {
                if (equipment == null && health != null) equipment = health.GetComponent<PlayerEquipment>();
                return equipment;
            }
        }

        private Texture2D EquipmentIcon(EquipmentId id)
        {
            if (!equipmentIcons.TryGetValue(id, out var icon))
            {
                icon = Resources.Load<Texture2D>("UI/HUD/Equipment/" + id);
                equipmentIcons[id] = icon; // 없어도(null) 기억해서 매 프레임 다시 찾지 않는다
            }
            return icon;
        }

        // 수식어 등급 색: 부서진·낡은 회색, 보통 흰색, 튼튼한 초록, 빛나는 하늘색, 전설의 금색
        public static string GradeColor(int grade)
        {
            if (grade < 0) return "#9a9a9a";
            switch (grade)
            {
                case 1: return "#7dff8a";
                case 2: return "#7fd8ff";
                case 3: return "#ffd84d";
                default: return "#fff4dc";
            }
        }

        private static string SlotName(EquipSlot slot) => slot == EquipSlot.Robe ? "로브" : slot == EquipSlot.Staff ? "지팡이" : "신발";

        // 장비 머리글 + 칸마다 한 줄. 반환: 다음 y.
        private float DrawEquipmentRows(float x, float y, float width, float bottom)
        {
            if (y >= bottom) return y;
            var head = new GUIContent("<b>장비</b>");
            float headHeight = small.CalcHeight(head, width);
            GUI.Label(new Rect(x, y, width, headHeight), head, small);
            y += headHeight;

            var worn = Equipment;
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                if (y >= bottom) break;
                var item = worn != null ? worn.Get(slot) : null;
                string text;
                if (item == null)
                {
                    text = $"<color=#8a8a8a>{SlotName(slot)}: 없음</color>";
                }
                else
                {
                    var stats = new List<string>();
                    foreach (var stat in item.Stats()) stats.Add(EquipmentBook.Describe(stat));
                    text = $"<color={GradeColor(item.Grade)}>{item.Name}</color> — {string.Join(", ", stats)}";
                }

                var line = new GUIContent(text);
                float textX = x + EquipmentIconSize + 4f;
                float textWidth = width - EquipmentIconSize - 4f;
                float lineHeight = Mathf.Min(small.CalcHeight(line, textWidth), bottom - y);
                if (item != null)
                {
                    var icon = EquipmentIcon(item.Id);
                    if (icon != null) GUI.DrawTexture(new Rect(x, y, EquipmentIconSize, EquipmentIconSize), icon, ScaleMode.ScaleToFit);
                }
                GUI.Label(new Rect(textX, y, textWidth, lineHeight), line, small);
                y += Mathf.Max(lineHeight, EquipmentIconSize + 1f) + 1f;
            }

            return y + 6f;
        }

        // 결과 창 유물 줄: 오른쪽 끝에 이번 판 마지막 장비 3칸 아이콘 (Docs/EquipmentDesign.md 8절), 남은 폭에 유물 줄.
        // 장비가 하나도 없으면 유물 줄만 (SkillHUD.RelicIcons.cs DrawEndRelics).
        private const float EndEquipmentGap = 6f;

        private void DrawEndRelicsAndEquipment(Rect rect)
        {
            var worn = Equipment;
            var items = new List<EquipmentItem>();
            if (worn != null)
                foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
                {
                    var item = worn.Get(slot);
                    if (item != null && EquipmentIcon(item.Id) != null) items.Add(item);
                }
            if (items.Count == 0)
            {
                DrawEndRelics(rect);
                return;
            }

            var style = new GUIStyle(small) { wordWrap = false, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), richText = true };
            var header = new GUIContent("<color=#7fd8ff>장비</color>");
            float headerWidth = style.CalcSize(header).x;
            float width = headerWidth + items.Count * (EndEquipmentGap + RelicListIconSize);
            var equipRect = new Rect(rect.xMax - width, rect.y, width, rect.height);
            DrawEndRelics(new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - width - 14f), rect.height));

            float x = equipRect.x;
            GUI.Label(new Rect(x, rect.y, headerWidth + 2f, rect.height), header, style);
            x += headerWidth;
            var mouse = Event.current.mousePosition;
            EquipmentItem hovered = null;
            float hoveredCenter = 0f;
            foreach (var item in items)
            {
                x += EndEquipmentGap;
                var iconRect = new Rect(x, rect.y + (rect.height - RelicListIconSize) * 0.5f, RelicListIconSize, RelicListIconSize);
                GUI.DrawTexture(iconRect, EquipmentIcon(item.Id), ScaleMode.ScaleToFit);
                if (iconRect.Contains(mouse)) { hovered = item; hoveredCenter = iconRect.center.x; }
                x += RelicListIconSize;
            }

            if (hovered == null) return;
            // 마우스를 올린 장비 이름 (등급 색) 말풍선: 줄 바로 위
            var tip = new GUIStyle(small) { wordWrap = false, alignment = TextAnchor.MiddleCenter, richText = true, padding = new RectOffset(8, 8, 3, 3) };
            tip.normal.textColor = OutlinedLabel.Cream;
            var content = new GUIContent($"<color={GradeColor(hovered.Grade)}>{hovered.Name}</color>");
            var size = tip.CalcSize(content);
            float left = Mathf.Clamp(hoveredCenter - size.x * 0.5f, rect.x, rect.xMax - size.x);
            var box = new Rect(left, rect.y - size.y - 2f, size.x, size.y);
            var previous = GUI.color;
            GUI.color = new Color(0.08f, 0.06f, 0.1f, 0.88f * previous.a);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(box, content, tip);
        }
    }
}
