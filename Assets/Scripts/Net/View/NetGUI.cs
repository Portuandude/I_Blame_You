using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Net
{
    // HUD(IMGUI)를 친구 화면에 옮기는 길. 게임의 OnGUI 파일 맨 위에 `using GUI = IBlameYou.Net.NetGUI;`를 두면
    // 그 파일의 GUI.Label·GUI.DrawTexture 등이 여기를 거친다: 평소처럼 그리고, 온라인 호스트일 때는 그리는 명령도 기록한다 (HudRecorder).
    // UnityEngine.GUI와 같은 이름·같은 모양만 둔다. 새로 쓰는 GUI 함수가 있으면 여기에 같은 모양으로 더한다.
    public static class NetGUI
    {
        // ---- 상태 (그대로 넘긴다. 기록은 그릴 때 실제 GUI 상태를 읽는다) ----
        public static Color color { get => GUI.color; set => GUI.color = value; }
        public static Color contentColor { get => GUI.contentColor; set => GUI.contentColor = value; }
        public static Color backgroundColor { get => GUI.backgroundColor; set => GUI.backgroundColor = value; }
        public static Matrix4x4 matrix { get => GUI.matrix; set => GUI.matrix = value; }
        public static bool enabled { get => GUI.enabled; set => GUI.enabled = value; }
        public static bool changed { get => GUI.changed; set => GUI.changed = value; }
        public static GUISkin skin { get => GUI.skin; set => GUI.skin = value; }
        public static int depth { get => GUI.depth; set => GUI.depth = value; }
        public static string tooltip { get => GUI.tooltip; set => GUI.tooltip = value; }

        // ---- 글자 ----
        public static void Label(Rect position, string text) => Label(position, Content(text), GUI.skin.label);
        public static void Label(Rect position, Texture image) => Label(position, Content(image), GUI.skin.label);
        public static void Label(Rect position, GUIContent content) => Label(position, content, GUI.skin.label);
        public static void Label(Rect position, string text, GUIStyle style) => Label(position, Content(text), style);
        public static void Label(Rect position, Texture image, GUIStyle style) => Label(position, Content(image), style);

        public static void Label(Rect position, GUIContent content, GUIStyle style)
        {
            HudRecorder.Content(HudOp.Label, position, content, style, 0);
            GUI.Label(position, content, style);
        }

        // ---- 상자 ----
        public static void Box(Rect position, string text) => Box(position, Content(text), GUI.skin.box);
        public static void Box(Rect position, Texture image) => Box(position, Content(image), GUI.skin.box);
        public static void Box(Rect position, GUIContent content) => Box(position, content, GUI.skin.box);
        public static void Box(Rect position, string text, GUIStyle style) => Box(position, Content(text), style);
        public static void Box(Rect position, Texture image, GUIStyle style) => Box(position, Content(image), style);

        public static void Box(Rect position, GUIContent content, GUIStyle style)
        {
            HudRecorder.Content(HudOp.Box, position, content, style, 0);
            GUI.Box(position, content, style);
        }

        // ---- 버튼 (친구 화면에는 그림만) ----
        public static bool Button(Rect position, string text) => Button(position, Content(text), GUI.skin.button);
        public static bool Button(Rect position, Texture image) => Button(position, Content(image), GUI.skin.button);
        public static bool Button(Rect position, GUIContent content) => Button(position, content, GUI.skin.button);
        public static bool Button(Rect position, string text, GUIStyle style) => Button(position, Content(text), style);
        public static bool Button(Rect position, Texture image, GUIStyle style) => Button(position, Content(image), style);

        public static bool Button(Rect position, GUIContent content, GUIStyle style)
        {
            if (HudRecorder.Active)
            {
                bool hover = GUI.enabled && position.Contains(Event.current.mousePosition);
                bool active = hover && Input.GetMouseButton(0);
                HudRecorder.Content(HudOp.StyleDraw, position, content, style, HudRecorder.StateFlags(hover, active, false, false));
            }
            return GUI.Button(position, content, style);
        }

        // GUIStyle.Draw를 직접 부르던 곳 (style.Draw(rect, ...)) 대신
        public static void StyleDraw(GUIStyle style, Rect position, bool isHover, bool isActive, bool on, bool hasKeyboardFocus) =>
            StyleDraw(style, position, GUIContent.none, isHover, isActive, on, hasKeyboardFocus);

        public static void StyleDraw(GUIStyle style, Rect position, GUIContent content, bool isHover, bool isActive, bool on, bool hasKeyboardFocus)
        {
            HudRecorder.Content(HudOp.StyleDraw, position, content, style, HudRecorder.StateFlags(isHover, isActive, on, hasKeyboardFocus));
            style.Draw(position, content, isHover, isActive, on, hasKeyboardFocus);
        }

        // ---- 그림 ----
        public static void DrawTexture(Rect position, Texture image) =>
            DrawTexture(position, image, ScaleMode.StretchToFill, true, 0f);

        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode) =>
            DrawTexture(position, image, scaleMode, true, 0f);

        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend) =>
            DrawTexture(position, image, scaleMode, alphaBlend, 0f);

        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend, float imageAspect)
        {
            HudRecorder.Texture(position, image, scaleMode, alphaBlend, imageAspect, false, Color.white, Vector4.zero, Vector4.zero);
            GUI.DrawTexture(position, image, scaleMode, alphaBlend, imageAspect);
        }

        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend, float imageAspect, Color color, float borderWidth, float borderRadius) =>
            DrawTexture(position, image, scaleMode, alphaBlend, imageAspect, color, Vector4.one * borderWidth, Vector4.one * borderRadius);

        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend, float imageAspect, Color color, Vector4 borderWidths, float borderRadius) =>
            DrawTexture(position, image, scaleMode, alphaBlend, imageAspect, color, borderWidths, Vector4.one * borderRadius);

        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend, float imageAspect, Color color, Vector4 borderWidths, Vector4 borderRadiuses)
        {
            HudRecorder.Texture(position, image, scaleMode, alphaBlend, imageAspect, true, color, borderWidths, borderRadiuses);
            GUI.DrawTexture(position, image, scaleMode, alphaBlend, imageAspect, color, borderWidths, borderRadiuses);
        }

        public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords) =>
            DrawTextureWithTexCoords(position, image, texCoords, true);

        public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords, bool alphaBlend)
        {
            HudRecorder.TexCoords(position, image, texCoords, alphaBlend);
            GUI.DrawTextureWithTexCoords(position, image, texCoords, alphaBlend);
        }

        // ---- 묶음 ----
        public static void BeginGroup(Rect position)
        {
            HudRecorder.Group(position);
            GUI.BeginGroup(position);
        }

        public static void BeginGroup(Rect position, GUIStyle style)
        {
            HudRecorder.Content(HudOp.Box, position, GUIContent.none, style, 0);
            HudRecorder.Group(position);
            GUI.BeginGroup(position, style);
        }

        public static void EndGroup()
        {
            HudRecorder.EndGroup();
            GUI.EndGroup();
        }

        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect) =>
            BeginScrollView(position, scrollPosition, viewRect, false, false);

        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect, bool alwaysShowHorizontal, bool alwaysShowVertical)
        {
            var result = GUI.BeginScrollView(position, scrollPosition, viewRect, alwaysShowHorizontal, alwaysShowVertical);
            // 친구 화면: 보이는 칸 안에서, 스크롤한 만큼 옮겨 그린다 (스크롤 막대는 빠짐)
            HudRecorder.Group(position);
            HudRecorder.Group(new Rect(-result.x, -result.y, Mathf.Max(viewRect.width, position.width), Mathf.Max(viewRect.height, position.height)));
            return result;
        }

        public static void EndScrollView() => EndScrollView(true);

        public static void EndScrollView(bool handleScrollWheel)
        {
            HudRecorder.EndGroup();
            HudRecorder.EndGroup();
            GUI.EndScrollView(handleScrollWheel);
        }

        // ---- 입력 칸·막대 (친구 화면에는 모양만) ----
        public static string TextField(Rect position, string text) => TextField(position, text, -1, GUI.skin.textField);
        public static string TextField(Rect position, string text, int maxLength) => TextField(position, text, maxLength, GUI.skin.textField);
        public static string TextField(Rect position, string text, GUIStyle style) => TextField(position, text, -1, style);

        public static string TextField(Rect position, string text, int maxLength, GUIStyle style)
        {
            HudRecorder.Content(HudOp.StyleDraw, position, Content(text), style, HudRecorder.StateFlags(false, false, false, true));
            return GUI.TextField(position, text, maxLength, style);
        }

        public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue) =>
            HorizontalSlider(position, value, leftValue, rightValue, GUI.skin.horizontalSlider, GUI.skin.horizontalSliderThumb);

        public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb)
        {
            HudRecorder.Slider(position, value, leftValue, rightValue, slider, thumb);
            return GUI.HorizontalSlider(position, value, leftValue, rightValue, slider, thumb);
        }

        public static bool Toggle(Rect position, bool value, string text) => Toggle(position, value, Content(text), GUI.skin.toggle);
        public static bool Toggle(Rect position, bool value, string text, GUIStyle style) => Toggle(position, value, Content(text), style);

        public static bool Toggle(Rect position, bool value, GUIContent content, GUIStyle style)
        {
            HudRecorder.Content(HudOp.StyleDraw, position, content, style, HudRecorder.StateFlags(false, false, value, false));
            return GUI.Toggle(position, value, content, style);
        }

        public static void SetNextControlName(string name) => GUI.SetNextControlName(name);
        public static void FocusControl(string name) => GUI.FocusControl(name);
        public static string GetNameOfFocusedControl() => GUI.GetNameOfFocusedControl();

        // GUIContent를 매번 만들지 않게 (GUI 안에서 한 번에 하나만 쓰므로 재사용해도 된다)
        private static readonly GUIContent textContent = new GUIContent();
        private static readonly GUIContent imageContent = new GUIContent();

        private static GUIContent Content(string text)
        {
            textContent.text = text;
            textContent.image = null;
            textContent.tooltip = null;
            return textContent;
        }

        private static GUIContent Content(Texture image)
        {
            imageContent.text = null;
            imageContent.image = image;
            imageContent.tooltip = null;
            return imageContent;
        }
    }

    // GUILayout도 같은 방식 (`using GUILayout = IBlameYou.Net.NetGUILayout;`). 자리 계산은 Unity에 맡기고, 그리는 것만 NetGUI로.
    public static class NetGUILayout
    {
        public static GUILayoutOption Width(float width) => GUILayout.Width(width);
        public static GUILayoutOption Height(float height) => GUILayout.Height(height);
        public static GUILayoutOption MinWidth(float width) => GUILayout.MinWidth(width);
        public static GUILayoutOption MinHeight(float height) => GUILayout.MinHeight(height);
        public static GUILayoutOption MaxWidth(float width) => GUILayout.MaxWidth(width);
        public static GUILayoutOption MaxHeight(float height) => GUILayout.MaxHeight(height);
        public static GUILayoutOption ExpandWidth(bool expand) => GUILayout.ExpandWidth(expand);
        public static GUILayoutOption ExpandHeight(bool expand) => GUILayout.ExpandHeight(expand);

        public static void Space(float pixels) => GUILayout.Space(pixels);
        public static void FlexibleSpace() => GUILayout.FlexibleSpace();

        public static void BeginHorizontal(params GUILayoutOption[] options) => GUILayout.BeginHorizontal(options);
        public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options) => GUILayout.BeginHorizontal(style, options);
        public static void EndHorizontal() => GUILayout.EndHorizontal();
        public static void BeginVertical(params GUILayoutOption[] options) => GUILayout.BeginVertical(options);
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) => GUILayout.BeginVertical(style, options);
        public static void EndVertical() => GUILayout.EndVertical();

        public static void BeginArea(Rect screenRect)
        {
            HudRecorder.Group(screenRect);
            GUILayout.BeginArea(screenRect);
        }

        public static void EndArea()
        {
            HudRecorder.EndGroup();
            GUILayout.EndArea();
        }

        // 레이아웃 스크롤 칸은 친구 화면에 자리를 알 수 없어 안을 그리지 않는다 (도시 아카데미 목록·시작 화면 기록 목록)
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options)
        {
            var result = GUILayout.BeginScrollView(scrollPosition, options);
            HudRecorder.Hide(true);
            return result;
        }

        public static void EndScrollView()
        {
            HudRecorder.Hide(false);
            GUILayout.EndScrollView();
        }

        public static void Label(string text, params GUILayoutOption[] options) => Label(new GUIContent(text), GUI.skin.label, options);
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) => Label(new GUIContent(text), style, options);
        public static void Label(Texture image, GUIStyle style, params GUILayoutOption[] options) => Label(new GUIContent(image), style, options);
        public static void Label(GUIContent content, params GUILayoutOption[] options) => Label(content, GUI.skin.label, options);

        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options) =>
            NetGUI.Label(GUILayoutUtility.GetRect(content, style, options), content, style);

        public static bool Button(string text, params GUILayoutOption[] options) => Button(new GUIContent(text), GUI.skin.button, options);
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => Button(new GUIContent(text), style, options);

        public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options) =>
            NetGUI.Button(GUILayoutUtility.GetRect(content, style, options), content, style);

        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => Toggle(value, text, GUI.skin.toggle, options);

        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            return NetGUI.Toggle(GUILayoutUtility.GetRect(content, style, options), value, content, style);
        }

        public static float HorizontalSlider(float value, float leftValue, float rightValue, params GUILayoutOption[] options)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.horizontalSlider, options);
            return NetGUI.HorizontalSlider(rect, value, leftValue, rightValue);
        }
    }
}
