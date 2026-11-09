using UnityEngine;

namespace IBlameYou.UI
{
    // 왼쪽 위 상태 패널 첫 줄의 체력·마나 바 (귀엽지만 화려하게).
    // 하트/별 아이콘 + 둥근 캡슐 바 + 빛나는 테두리. 그림은 Tools/ArtGen/generate_hud.py가 만든
    // Resources/UI/HUD/*.png를 9분할(GUIStyle.border)로 늘려 그리고, 없으면 단색 사각형으로 그린다.
    // 줄어든 부분은 흰색 잔상으로 잠깐 남았다가 따라 내려간다 (회복하면 잔상은 바로 맞춰진다).
    // 체력이 낮으면(LowHealth 경고와 같은 기준) 하트가 두근거린다.
    public partial class SkillHUD
    {
        // 아래 BarHeight·BarGlow와 9분할 테두리(9, 14) 값은 generate_hud.py의 BAR_H·GLOW와 손으로 맞춘 값이다. 한쪽만 바꾸지 말 것.
        private const float BarHeight = 18f;
        private const float BarIconSize = 24f;
        private const float BarGlow = 5f;            // BarFrame.png 바깥 빛 여백 (generate_hud.py GLOW)

        private static readonly Color HealthFillColor = new Color(1f, 0.37f, 0.59f);   // berry보다 조금 진하게
        private static readonly Color HealthGlowColor = new Color(1f, 0.55f, 0.75f);
        private static readonly Color ManaFillColor = new Color(0.35f, 0.7f, 1f);
        private static readonly Color ManaGlowColor = new Color(0.5f, 0.96f, 1f);        // aqua
        private static readonly Color InfiniteManaColor = new Color(1f, 0.85f, 0.3f);    // gold

        private bool barArtLoaded;
        private GUIStyle barBackStyle;
        private GUIStyle barFillStyle;
        private GUIStyle barFrameStyle;
        private Texture2D heartIcon;
        private Texture2D starIcon;
        private Texture2D crownIcon;
        private Texture2D flagIcon;
        private GUIStyle barText;

        private BarTrail healthTrail; // 잔상 계산은 CapsuleBar.cs의 BarTrail (머리 위 바와 공용)
        private BarTrail manaTrail;

        private void EnsureBarArt()
        {
            if (barArtLoaded) return;
            barArtLoaded = true;

            barBackStyle = NineSlice("UI/HUD/BarBack", new RectOffset(9, 9, 0, 0), new Color(0.08f, 0.06f, 0.15f, 0.92f));
            barFillStyle = NineSlice("UI/HUD/BarFill", new RectOffset(9, 9, 0, 0), Color.white);
            barFrameStyle = NineSlice("UI/HUD/BarFrame", new RectOffset(14, 14, 14, 14), Color.clear);
            heartIcon = Resources.Load<Texture2D>("UI/HUD/Heart");
            starIcon = Resources.Load<Texture2D>("UI/HUD/Star");
            crownIcon = Resources.Load<Texture2D>("UI/HUD/Crown");
            flagIcon = Resources.Load<Texture2D>("UI/HUD/Flag");

            barText = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
            barText.normal.textColor = Color.white;
        }

        // 그림이 없으면 단색 텍스처로 대신한다 (테두리 없음).
        private static GUIStyle NineSlice(string path, RectOffset border, Color fallback)
        {
            var texture = Resources.Load<Texture2D>(path);
            var style = new GUIStyle();
            if (texture != null)
            {
                style.normal.background = texture;
                style.border = border;
            }
            else
            {
                style.normal.background = SolidTexture(fallback);
            }

            return style;
        }

        private static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        // 상태 패널 안 한 줄(높이 BarIconSize)에 체력·마나 바를 나란히 그린다.
        private void DrawVitalBars(Rect line)
        {
            EnsureBarArt();

            float healthRatio = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
            float manaRatio = mana.Max > 0f ? Mathf.Clamp01(mana.Current / mana.Max) : 0f;
            if (mana.Infinite) manaRatio = 1f;

            if (Event.current.type == EventType.Repaint)
            {
                float now = Time.unscaledTime;
                float dt = Time.unscaledDeltaTime;
                healthTrail.Step(healthRatio, now, dt);
                manaTrail.Step(manaRatio, now, dt);
            }

            float half = (line.width - 8f) / 2f;
            float severity = LowHealthSeverity();
            float beat = severity > 0f ? 1f + 0.18f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * (1.2f + severity * 1.5f))) : 1f;

            DrawVitalBar(new Rect(line.x, line.y, half, line.height), heartIcon, beat, healthRatio, healthTrail.Shown,
                HealthFillColor, HealthGlowColor, $"{health.Current:0}/{health.Max:0}");

            string manaText = mana.Infinite ? "무한" : $"{mana.Current:0}/{mana.Max:0}";
            DrawVitalBar(new Rect(line.x + half + 8f, line.y, half, line.height), starIcon, 1f, manaRatio, manaTrail.Shown,
                mana.Infinite ? InfiniteManaColor : ManaFillColor, mana.Infinite ? InfiniteManaColor : ManaGlowColor, manaText);
        }

        private void DrawVitalBar(Rect area, Texture2D icon, float iconScale, float ratio, float trail, Color fill, Color glow, string text)
        {
            float barX = area.x + BarIconSize + 4f;
            var bar = new Rect(barX, area.y + (area.height - BarHeight) / 2f, area.xMax - barX - BarGlow, BarHeight);
            DrawCapsuleBar(bar, ratio, trail, fill, glow, text);
            DrawIcon(icon, new Rect(area.x, area.y + (area.height - BarIconSize) / 2f, BarIconSize, BarIconSize), iconScale);
        }

        // 둥근 캡슐 바 하나 (빛 테두리 → 바탕 → 흰 잔상 → 채움 → 글자). 보스 체력바도 쓴다.
        private void DrawCapsuleBar(Rect bar, float ratio, float trail, Color fill, Color glow, string text)
        {
            EnsureBarArt();
            var previous = GUI.color;

            // 빛나는 테두리 (바깥 빛 여백만큼 크게)
            GUI.color = glow;
            DrawStyle(barFrameStyle, new Rect(bar.x - BarGlow, bar.y - BarGlow, bar.width + 2f * BarGlow, bar.height + 2f * BarGlow));
            GUI.color = previous;
            DrawStyle(barBackStyle, bar);

            // 흰 잔상 → 채움 순서. 전체 너비로 그리고 비율만큼 잘라서(그룹) 왼쪽 둥근 끝을 유지한다.
            DrawClipped(bar, trail, new Color(1f, 1f, 1f, 0.95f));
            DrawClipped(bar, ratio, fill);

            if (string.IsNullOrEmpty(text)) return;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(bar.x + 1f, bar.y + 1f, bar.width, bar.height), text, barText);
            GUI.color = previous;
            GUI.Label(bar, text, barText);
        }

        // 아이콘을 칸 가운데에 scale배 크기로 (두근거림 등).
        private static void DrawIcon(Texture2D icon, Rect cell, float scale)
        {
            if (icon == null) return;
            float w = cell.width * scale;
            float h = cell.height * scale;
            GUI.DrawTexture(new Rect(cell.center.x - w / 2f, cell.center.y - h / 2f, w, h), icon, ScaleMode.ScaleToFit, true);
        }

        private void DrawClipped(Rect bar, float ratio, Color color)
        {
            if (ratio <= 0f) return;

            var previous = GUI.color;
            GUI.color = color;
            GUI.BeginGroup(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(ratio), bar.height));
            DrawStyle(barFillStyle, new Rect(0f, 0f, bar.width, bar.height));
            GUI.EndGroup();
            GUI.color = previous;
        }

        private static void DrawStyle(GUIStyle style, Rect rect)
        {
            if (Event.current.type == EventType.Repaint) style.Draw(rect, false, false, false, false);
        }
    }
}
