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
    }
}
