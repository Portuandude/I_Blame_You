using UnityEngine;

namespace IBlameYou.UI
{
    // IMGUI 테두리 글자: 같은 글자를 8방향으로 밀어 그려 테두리를 만든 뒤 가운데에 본 글자를 그린다.
    // 바깥(진한 외곽선) → 안쪽(gold 등) 두 겹 + 본 글자. GUI.color의 알파를 곱해 쓰고, 끝나면 GUI.color를 되돌린다.
    // 스타일은 richText = false로 넘길 것 (색 태그가 있으면 테두리까지 물든다).
    public static class OutlinedLabel
    {
        private static readonly Vector2[] Directions =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f),
            new Vector2(-0.7f, -0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, 0.7f),
        };

        public static readonly Color Outline = new Color(0.09f, 0.08f, 0.1f);
        public static readonly Color Gold = new Color(1f, 0.85f, 0.3f);    // palette.py gold
        public static readonly Color Cream = new Color(1f, 0.96f, 0.84f);  // palette.py cream

        public static void Draw(Rect rect, string text, GUIStyle style, Color fill, Color rim, float rimWidth, float outlineWidth)
        {
            var previous = GUI.color;
            float alpha = previous.a;
            Layer(rect, text, style, WithAlpha(Outline, alpha), rimWidth + outlineWidth);
            Layer(rect, text, style, WithAlpha(rim, alpha), rimWidth);
            GUI.color = WithAlpha(fill, alpha);
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private static void Layer(Rect rect, string text, GUIStyle style, Color color, float radius)
        {
            GUI.color = color;
            foreach (var direction in Directions)
            {
                var offset = direction * radius;
                GUI.Label(new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height), text, style);
            }
        }

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);
    }
}
