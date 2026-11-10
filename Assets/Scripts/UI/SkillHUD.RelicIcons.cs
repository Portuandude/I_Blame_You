using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
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
        private GUIStyle relicBadgeText;
        private FallRecovery fallRecovery;

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

        // 횟수가 있는 유물의 남은 횟수: 되감기 모래시계 = 남은 무료 낙하 복귀, 불사조 깃털 = 남은 쓰러짐 버티기
        // (생명 20 특성의 버티기와 합친 값). 횟수가 없는 유물은 -1.
        private int RelicCharges(RelicId id)
        {
            switch (id)
            {
                case RelicId.Hourglass:
                    if (fallRecovery == null && health != null) fallRecovery = health.GetComponent<FallRecovery>();
                    return fallRecovery != null ? fallRecovery.FreeFalls : -1;
                case RelicId.PhoenixFeather:
                    return health != null ? health.DeathSaves : -1;
                default:
                    return -1;
            }
        }

        // HUD 유물 목록 아이콘: 횟수 유물은 오른쪽 아래에 테두리 숫자 배지, 다 쓰면(0) 아이콘을 어둡게.
        private void DrawRelicIconWithCharges(RelicId id, Rect rect)
        {
            int charges = RelicCharges(id);
            var previous = GUI.color;
            if (charges == 0) GUI.color = new Color(0.4f, 0.4f, 0.45f, previous.a * 0.7f);
            DrawRelicIcon(id, rect);
            GUI.color = previous;
            if (charges < 0) return;

            if (relicBadgeText == null)
                relicBadgeText = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11, fontStyle = FontStyle.Bold, richText = false, wordWrap = false,
                    alignment = TextAnchor.LowerRight, padding = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Overflow
                };
            var badge = new Rect(rect.x + rect.width * 0.3f, rect.y + rect.height * 0.35f, rect.width * 0.8f, rect.height * 0.8f);
            var fill = charges > 0 ? OutlinedLabel.Cream : new Color(0.75f, 0.75f, 0.78f);
            OutlinedLabel.Draw(badge, charges.ToString(), relicBadgeText, fill, charges > 0 ? OutlinedLabel.Gold : new Color(0.35f, 0.35f, 0.4f), 1f, 1f);
        }

        // 게임오버·클리어 창: 가운데 정렬로 [아이콘 이름] 줄. 넓이를 넘으면 이름은 빼고 아이콘만, 그래도 넘으면 글자 목록(RelicNames)으로.
        private void DrawEndRelics(Rect rect)
        {
            var style = new GUIStyle(small) { wordWrap = false, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) };
            bool anyIcon = false;
            for (int i = 0; i < RelicCount; i++) anyIcon |= RelicIcon(relics.Owned[i]) != null;
            if (RelicCount == 0 || !anyIcon)
            {
                GUI.Label(rect, RelicNames(), new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            const float gap = 10f;
            var header = new GUIContent("<color=#ffd84d>유물</color>");
            float headerWidth = style.CalcSize(header).x;
            float withNames = headerWidth, iconsOnly = headerWidth;
            for (int i = 0; i < RelicCount; i++)
            {
                var id = relics.Owned[i];
                bool hasIcon = RelicIcon(id) != null;
                float nameWidth = style.CalcSize(new GUIContent(RelicBook.Name(id))).x;
                float icon = hasIcon ? RelicListIconSize + 3f : 0f;
                withNames += gap + icon + nameWidth;
                iconsOnly += gap + (hasIcon ? RelicListIconSize : nameWidth);
            }

            bool showNames = withNames <= rect.width;
            float total = showNames ? withNames : iconsOnly;
            if (total > rect.width)
            {
                GUI.Label(rect, RelicNames(), new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            float x = rect.x + (rect.width - total) * 0.5f;
            GUI.Label(new Rect(x, rect.y, headerWidth + 2f, rect.height), header, style);
            x += headerWidth;
            for (int i = 0; i < RelicCount; i++)
            {
                var id = relics.Owned[i];
                x += gap;
                bool hasIcon = RelicIcon(id) != null;
                if (hasIcon)
                {
                    DrawRelicIcon(id, new Rect(x, rect.y + (rect.height - RelicListIconSize) * 0.5f, RelicListIconSize, RelicListIconSize));
                    x += RelicListIconSize + (showNames ? 3f : 0f);
                }
                if (!showNames && hasIcon) continue;

                var name = new GUIContent(RelicBook.Name(id));
                float w = style.CalcSize(name).x;
                GUI.Label(new Rect(x, rect.y, w + 2f, rect.height), name, style);
                x += w;
            }
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
                    DrawRelicIconWithCharges(id, new Rect(x, RelicListY + (height - RelicListIconSize) * 0.5f, RelicListIconSize, RelicListIconSize));
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
