using System.Collections.Generic;
using IBlameYou.Core;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // HUD를 온라인 친구 화면에도 (Net/NetGUI)

namespace IBlameYou.UI
{
    // 화면 아래 조작 도움말 줄. 귀엽게: 키 이름은 작은 둥근 키캡(CuteButton 그림 재사용), 줄마다 반투명 알약 배경(Pill).
    // A/B 플레이어 묶음은 각자 색 이름표로 시작한다. 화면이 좁으면 항목 단위로 줄을 나눠 아래에서 위로 쌓는다.
    // 키 이름에 '+'가 있으면(W/A/S/D+Enter) 키캡을 나눠 그리고 사이에 작은 '+'를 둔다.
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
            // 그릴 때 쓰는 글자·너비도 만들 때 한 번만 잰다 (늘 보이는 줄이라 OnGUI마다 GUIContent·CalcSize를 하던 것, 버그 점검 2026-11-25)
            public GUIContent TagContent;
            public float TagWidth;
            public GUIContent[] KeyContents;
            public float[] CapWidths;
            public GUIContent DescContent;
            public float DescWidth;
        }

        private static readonly GUIContent HelpPlus = new GUIContent("+");
        private float helpPlusWidth;

        private GUIStyle helpCap;
        private GUIStyle helpText;
        private GUIStyle helpTag;
        private GUIStyle helpPill;
        private List<HelpItem> helpItems;
        private bool helpItemsDev;
        private int helpItemsKeys = -1; // IBlameYou.Player.KeyBindings.Revision (P1 키를 바꾸면 다시 만든다)
        private readonly List<int> helpRowStarts = new List<int>();

        private void DrawHelp()
        {
            EnsureHelpStyles();
            if (helpItems == null || helpItemsDev != GameSession.DevMode || helpItemsKeys != IBlameYou.Player.KeyBindings.Revision)
            {
                helpItemsDev = GameSession.DevMode;
                helpItemsKeys = IBlameYou.Player.KeyBindings.Revision;
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
            helpPlusWidth = helpText.CalcSize(HelpPlus).x;
            float width = 0f;
            if (!string.IsNullOrEmpty(item.Tag))
            {
                item.TagContent = new GUIContent(item.Tag);
                item.TagWidth = helpTag.CalcSize(item.TagContent).x;
                width += item.TagWidth + HelpInnerGap;
            }
            item.KeyContents = new GUIContent[item.Keys.Length];
            item.CapWidths = new float[item.Keys.Length];
            for (int k = 0; k < item.Keys.Length; k++)
            {
                if (k > 0) width += helpPlusWidth + 2f * HelpInnerGap;
                item.KeyContents[k] = new GUIContent(item.Keys[k]);
                item.CapWidths[k] = Mathf.Max(HelpCapMinWidth, helpCap.CalcSize(item.KeyContents[k]).x);
                width += item.CapWidths[k];
            }
            item.DescContent = new GUIContent(item.Desc);
            item.DescWidth = helpText.CalcSize(item.DescContent).x;
            width += HelpInnerGap + item.DescWidth;
            return width;
        }

        private void DrawHelpItem(HelpItem item, float x, float centerY)
        {
            var previous = GUI.color;
            if (item.TagContent != null)
            {
                GUI.color = item.TagColor;
                GUI.Label(new Rect(x, centerY - 10f, item.TagWidth, 20f), item.TagContent, helpTag);
                GUI.color = previous;
                x += item.TagWidth + HelpInnerGap;
            }

            for (int k = 0; k < item.KeyContents.Length; k++)
            {
                if (k > 0)
                {
                    x += HelpInnerGap;
                    GUI.Label(new Rect(x, centerY - 10f, helpPlusWidth, 20f), HelpPlus, helpText);
                    x += helpPlusWidth + HelpInnerGap;
                }
                float cw = item.CapWidths[k];
                // 버튼이 아니라 그림만: 마우스를 올려도 바뀌지 않게 보통 상태로만 그린다
                if (Event.current.type == EventType.Repaint)
                    GUI.StyleDraw(helpCap, new Rect(x, centerY - HelpCapHeight * 0.5f, cw, HelpCapHeight), item.KeyContents[k], false, false, false, false);
                x += cw;
            }

            x += HelpInnerGap;
            GUI.Label(new Rect(x, centerY - 10f, item.DescWidth + 2f, 20f), item.DescContent, helpText);
        }

        private static List<HelpItem> BuildHelpItems(bool dev)
        {
            var items = new List<HelpItem>();
            void Add(string tag, Color tagColor, string keys, string desc) =>
                items.Add(new HelpItem { Tag = tag, TagColor = tagColor, Keys = SplitKeys(keys), Desc = desc });

            // P1 키는 바꿀 수 있다 (KeyBindings). 기본값이면 "A/D", "Space", "Shift", "L.Ctrl", "W/A/S/D+Enter"
            Add(dev ? "A" : "P1·A", HelpColorA, IBlameYou.Player.KeyBindings.MoveKeys, "이동");
            Add(null, default, IBlameYou.Player.KeyBindings.Name(IBlameYou.Player.P1Action.Jump), "점프");
            Add(null, default, IBlameYou.Player.KeyBindings.Name(IBlameYou.Player.P1Action.Run), "달리기");
            Add(null, default, IBlameYou.Player.KeyBindings.Name(IBlameYou.Player.P1Action.Dash), "대쉬");
            Add(null, default, IBlameYou.Player.KeyBindings.TeleportKeys, "순간이동");

            Add(dev ? "B" : "P2·B", HelpColorB, "좌클릭", "마력탄(마나 5)");
            if (dev) Add(null, default, "1~4", "공격 스킬");
            else
            {
                Add(null, default, "1~4", "공격 스킬(→마법진 그리기)");
                Add(null, default, "우클릭 꾹", "다이얼로 공격 스킬");
            }

            if (dev)
            {
                Add("개발", HelpColorDev, "F1", "무적");
                Add(null, default, "F2", "마나 무한");
                Add(null, default, "F3", "적 처치");
                Add(null, default, "F4", "회복");
            }
            Add(null, default, "C", "유물 도감");
            Add(null, default, "Esc", "일시정지");
            return items;
        }

        // "W/A/S/D+Enter" → ["W/A/S/D", "Enter"]. '+' 하나뿐이거나 끝에 붙은 경우는 그대로 둔다.
        private static string[] SplitKeys(string keys)
        {
            var parts = keys.Split('+');
            foreach (var part in parts)
                if (part.Length == 0) return new[] { keys };
            return parts;
        }
    }
}
