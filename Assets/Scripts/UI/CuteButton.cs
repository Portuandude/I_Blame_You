using UnityEngine;

namespace IBlameYou.UI
{
    // 귀여운 둥근 버튼 스타일 (IMGUI). 일시정지·유물·특성 창과 시작 화면이 같이 쓴다.
    // 그림은 Tools/ArtGen/generate_hud.py가 만든 Resources/UI/HUD/Button(Hover/Active).png (9분할 사방 10픽셀).
    // 보통 / 올림(조금 밝게, 글자 흰색) / 누름(윗면이 2픽셀 내려가고 조금 어둡게, 글자 gold). 그림이 없으면 기본 버튼 스타일.
    // GUI.enabled = false인 버튼은 IMGUI가 반투명하게 그린다.
    public static class CuteButton
    {
        private static Texture2D normal;
        private static Texture2D hover;
        private static Texture2D active;
        private static bool loaded;

        // GUI.skin을 쓰므로 OnGUI 안에서만 부른다.
        public static GUIStyle Create(int fontSize)
        {
            if (!loaded)
            {
                loaded = true;
                normal = Resources.Load<Texture2D>("UI/HUD/Button");
                hover = Resources.Load<Texture2D>("UI/HUD/ButtonHover");
                active = Resources.Load<Texture2D>("UI/HUD/ButtonActive");
            }

            var style = new GUIStyle(GUI.skin.button) { fontSize = fontSize, fontStyle = FontStyle.Bold, richText = true };
            if (normal == null) return style;

            style.border = new RectOffset(10, 10, 10, 10);
            style.padding = new RectOffset(12, 12, 2, 5); // 아래 3픽셀은 버튼 두께라 글자를 살짝 위로 (28픽셀 버튼에도 15pt가 들어가게)
            style.normal.background = normal;
            style.hover.background = hover != null ? hover : normal;
            style.active.background = active != null ? active : normal;
            style.focused.background = normal;
            style.onNormal.background = normal;
            style.onHover.background = style.hover.background;
            style.onActive.background = style.active.background;
            style.onFocused.background = normal;

            var cream = new Color(1f, 0.96f, 0.84f);
            style.normal.textColor = cream;
            style.focused.textColor = cream;
            style.onNormal.textColor = cream;
            style.onFocused.textColor = cream;
            style.hover.textColor = Color.white;
            style.onHover.textColor = Color.white;
            style.active.textColor = new Color(1f, 0.85f, 0.3f); // gold
            style.onActive.textColor = style.active.textColor;
            return style;
        }
    }
}
