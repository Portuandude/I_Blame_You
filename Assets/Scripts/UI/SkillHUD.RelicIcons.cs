using System.Collections.Generic;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.UI
{
    // 유물 아이콘 (Tools/ArtGen/generate_relic_icons.py → Resources/UI/HUD/Relics/<RelicId>.png, 24x24).
    // 유물 상자 카드 오른쪽 위와 레벨 막대 아래 유물 목록에서 이름 앞에 그린다. 그림이 없으면 글자만.
    public partial class SkillHUD
    {
        private const float RelicListIconSize = 18f;
        private const float RelicListY = 256f;

        private readonly Dictionary<RelicId, Texture2D> relicIcons = new Dictionary<RelicId, Texture2D>();
        private GUIStyle relicListText;

        private Texture2D RelicIcon(RelicId id)
        {
            if (!relicIcons.TryGetValue(id, out var icon))
            {
                icon = Resources.Load<Texture2D>("UI/HUD/Relics/" + id);
                relicIcons[id] = icon; // 없어도(null) 기억해서 매 프레임 다시 찾지 않는다
            }
            return icon;
        }

        private void DrawRelicIcon(RelicId id, Rect rect)
        {
            var icon = RelicIcon(id);
            if (icon != null) GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
        }

        // 레벨 막대 아래: "유물" + [아이콘 이름] 줄. 화면보다 길어지면 이름은 빼고 아이콘만 늘어놓는다.
        private void DrawRelicList()
        {
            if (relics == null || relics.Owned.Count == 0) return;
            if (relicListText == null)
                relicListText = new GUIStyle(small) { wordWrap = false, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) };

            const float gap = 8f;
            const float height = 22f;
            var header = new GUIContent("<color=#ffd84d>유물</color>");
            float headerWidth = relicListText.CalcSize(header).x;
            float x = 12f;
            float maxX = Screen.width - 12f;

            float full = headerWidth;
            for (int i = 0; i < relics.Owned.Count; i++)
                full += gap + RelicListIconSize + 3f + relicListText.CalcSize(new GUIContent(RelicBook.Name(relics.Owned[i]))).x;
            bool iconsOnly = x + full > maxX;

            GUI.Label(new Rect(x, RelicListY, headerWidth + 2f, height), header, relicListText);
            x += headerWidth;
            for (int i = 0; i < relics.Owned.Count; i++)
            {
                var id = relics.Owned[i];
                x += gap;
                bool hasIcon = RelicIcon(id) != null;
                if (hasIcon)
                {
                    DrawRelicIcon(id, new Rect(x, RelicListY + (height - RelicListIconSize) * 0.5f, RelicListIconSize, RelicListIconSize));
                    x += RelicListIconSize + 3f;
                }
                if (iconsOnly && hasIcon) continue;

                var name = new GUIContent(RelicBook.Name(id));
                float w = relicListText.CalcSize(name).x;
                GUI.Label(new Rect(x, RelicListY, w + 2f, height), name, relicListText);
                x += w;
            }
        }
    }
}
