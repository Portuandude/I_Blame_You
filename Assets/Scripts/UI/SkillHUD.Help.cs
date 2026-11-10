using System.Collections.Generic;
using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.UI
{
    // 화면 아래 조작 도움말 줄. 귀엽게: 키 이름은 작은 둥근 키캡(CuteButton 그림 재사용), 줄마다 반투명 알약 배경(Pill).
    // A/B 플레이어 묶음은 각자 색 이름표로 시작한다. 화면이 좁으면 항목 단위로 줄을 나눠 아래에서 위로 쌓는다.
    // 키 이름에 '+'가 있으면(방향키+Enter) 키캡을 나눠 그리고 사이에 작은 '+'를 둔다.
    public partial class SkillHUD
    {
        private const float HelpRowHeight = 26f;
        private const float HelpRowGap = 4f;
        private const float HelpCapHeight = 19f;
        private const float HelpCapMinWidth = 22f;
        private const float HelpInnerGap = 4f;   // 키캡 ↔ 설명, 키캡 ↔ '+'
        private const float HelpItemGap = 12f;   // 항목 사이
        private const float HelpPillPadding = 12f;
        private const float HelpMargin = 12f;

        private static readonly Color HelpColorA = new Color(0.45f, 0.85f, 1f);   // #73d9ff (상태 패널과 같은 A 색)
        private static readonly Color HelpColorB = new Color(1f, 0.65f, 0.35f);   // #ffa659
        private static readonly Color HelpColorDev = new Color(1f, 0.85f, 0.3f);  // gold

        private sealed class HelpItem
        {
            public string Tag;       // 묶음 첫 항목에만 (A / B / 개발)
            public Color TagColor;
            public string[] Keys;
            public string Desc;
            public float Width;      // 미리 잰 전체 너비
        }

        private GUIStyle helpCap;
        private GUIStyle helpText;
        private GUIStyle helpTag;
        private GUIStyle helpPill;
        private List<HelpItem> helpItems;
        private bool helpItemsDev;
        private readonly List<int> helpRowStarts = new List<int>();

        private void DrawHelp()
        {
            EnsureHelpStyles();
            if (helpItems == null || helpItemsDev != GameSession.DevMode)
            {
                helpItemsDev = GameSession.DevMode;
                helpItems = BuildHelpItems(helpItemsDev);
                foreach (var item in helpItems) item.Width = MeasureHelpItem(item);
            }

            // 항목 단위로 줄 나누기 (한 항목이 화면보다 넓으면 그 줄에 혼자 둔다)
            float maxWidth = Screen.width - 2f * HelpMargin - 2f * HelpPillPadding;
            helpRowStarts.Clear();
            float rowWidth = 0f;
            for (int i = 0; i < helpItems.Count; i++)
            {
                float add = (rowWidth > 0f ? HelpItemGap : 0f) + helpItems[i].Width;
                if (rowWidth > 0f && rowWidth + add > maxWidth)
                {
                    helpRowStarts.Add(i);
                    rowWidth = helpItems[i].Width;
                }
                else
                {
                    if (i == 0) helpRowStarts.Add(0);
                    rowWidth += add;
                }
            }

            int rows = helpRowStarts.Count;
            for (int r = 0; r < rows; r++)
            {
                int start = helpRowStarts[r];
                int end = r + 1 < rows ? helpRowStarts[r + 1] : helpItems.Count;
                float width = 0f;
                for (int i = start; i < end; i++) width += (i > start ? HelpItemGap : 0f) + helpItems[i].Width;

                // 마지막 줄이 화면 맨 아래, 앞 줄일수록 위
                float y = Screen.height - HelpMargin + 4f - (rows - r) * (HelpRowHeight + HelpRowGap) + HelpRowGap;
                var pill = new Rect(HelpMargin, y, Mathf.Min(width + 2f * HelpPillPadding, Screen.width - 2f * HelpMargin), HelpRowHeight);
                DrawStyle(helpPill, pill);

                float x = pill.x + HelpPillPadding;
                for (int i = start; i < end; i++)
                {
                    if (i > start) x += HelpItemGap;
                    DrawHelpItem(helpItems[i], x, pill.y + pill.height * 0.5f);
                    x += helpItems[i].Width;
                }
            }
        }

        private void EnsureHelpStyles()
        {
            if (helpCap != null) return;
            helpCap = CuteButton.Create(11);
            helpCap.alignment = TextAnchor.MiddleCenter;
            helpCap.padding = new RectOffset(6, 6, 0, 3); // 아래 3픽셀은 버튼 두께
            helpCap.margin = new RectOffset(0, 0, 0, 0);
            helpCap.wordWrap = false;

            helpText = new GUIStyle(label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset(0, 0, 0, 0) };
            helpText.normal.textColor = OutlinedLabel.Cream;
            helpTag = new GUIStyle(helpText) { fontStyle = FontStyle.Bold };

            var pillTexture = Resources.Load<Texture2D>("UI/HUD/Pill");
            helpPill = new GUIStyle();
            helpPill.normal.background = pillTexture != null ? pillTexture : SolidTexture(new Color(0.1f, 0.08f, 0.19f, 0.7f));
            if (pillTexture != null) helpPill.border = new RectOffset(11, 11, 0, 0);
        }

        private float MeasureHelpItem(HelpItem item)
        {
            float width = 0f;
            if (!string.IsNullOrEmpty(item.Tag)) width += helpTag.CalcSize(new GUIContent(item.Tag)).x + HelpInnerGap;
            for (int k = 0; k < item.Keys.Length; k++)
            {
                if (k > 0) width += helpText.CalcSize(new GUIContent("+")).x + 2f * HelpInnerGap;
                width += CapWidth(item.Keys[k]);
            }
            width += HelpInnerGap + helpText.CalcSize(new GUIContent(item.Desc)).x;
            return width;
        }

        private float CapWidth(string key) => Mathf.Max(HelpCapMinWidth, helpCap.CalcSize(new GUIContent(key)).x);

        private void DrawHelpItem(HelpItem item, float x, float centerY)
        {
            var previous = GUI.color;
            if (!string.IsNullOrEmpty(item.Tag))
            {
                var tag = new GUIContent(item.Tag);
                float w = helpTag.CalcSize(tag).x;
                GUI.color = item.TagColor;
                GUI.Label(new Rect(x, centerY - 10f, w, 20f), tag, helpTag);
                GUI.color = previous;
                x += w + HelpInnerGap;
            }

            for (int k = 0; k < item.Keys.Length; k++)
            {
                if (k > 0)
                {
                    var plus = new GUIContent("+");
                    float pw = helpText.CalcSize(plus).x;
                    x += HelpInnerGap;
                    GUI.Label(new Rect(x, centerY - 10f, pw, 20f), plus, helpText);
                    x += pw + HelpInnerGap;
                }
                var key = new GUIContent(item.Keys[k]);
                float cw = CapWidth(item.Keys[k]);
                // 버튼이 아니라 그림만: 마우스를 올려도 바뀌지 않게 보통 상태로만 그린다
                if (Event.current.type == EventType.Repaint)
                    helpCap.Draw(new Rect(x, centerY - HelpCapHeight * 0.5f, cw, HelpCapHeight), key, false, false, false, false);
                x += cw;
            }

            var desc = new GUIContent(item.Desc);
            x += HelpInnerGap;
            GUI.Label(new Rect(x, centerY - 10f, helpText.CalcSize(desc).x + 2f, 20f), desc, helpText);
        }

        private static List<HelpItem> BuildHelpItems(bool dev)
        {
            var items = new List<HelpItem>();
            void Add(string tag, Color tagColor, string keys, string desc) =>
                items.Add(new HelpItem { Tag = tag, TagColor = tagColor, Keys = SplitKeys(keys), Desc = desc });

            Add("A", HelpColorA, "A/D", "이동");
            Add(null, default, "Space", "점프");
            Add(null, default, "Shift", "달리기");
            Add(null, default, "L.Ctrl", "대쉬");
            Add(null, default, "방향키+Enter", "순간이동");
            if (dev) Add(null, default, "1~5", "방어막");
            else Add(null, default, "넘패드 1~5", "방어막(→W/A/S/D)");

            Add("B", HelpColorB, "좌클릭", "마력탄(마나 5)");
            if (dev) Add(null, default, "6~9", "공격 스킬");
            else Add(null, default, "1~4", "공격 스킬(→영창 타이핑+Enter)");

            if (dev)
            {
                Add("개발", HelpColorDev, "F1", "무적");
                Add(null, default, "F2", "마나 무한");
                Add(null, default, "F3", "적 처치");
                Add(null, default, "F4", "회복");
            }
            Add(null, default, "Esc", "일시정지");
            return items;
        }

        // "방향키+Enter" → ["방향키", "Enter"]. '+' 하나뿐이거나 끝에 붙은 경우는 그대로 둔다.
        private static string[] SplitKeys(string keys)
        {
            var parts = keys.Split('+');
            foreach (var part in parts)
                if (part.Length == 0) return new[] { keys };
            return parts;
        }
    }
}
