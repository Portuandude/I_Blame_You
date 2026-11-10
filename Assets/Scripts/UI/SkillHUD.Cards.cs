using UnityEngine;

namespace IBlameYou.UI
{
    // 특성 창·유물 창 카드 공용 (귀엽지만 화려하게).
    // 카드 = 둥근 패널 + 소유자 색 테두리(CardFrame.png 9분할, A 하늘·B 주황·공용 크림), 창이 열리면 카드가 차례로 팝.
    // 창이 열려 있는 동안 Time.timeScale = 0이라 시간은 unscaledTime으로 잰다.
    public partial class SkillHUD
    {
        private const float CardPopTime = 0.25f;
        private const float CardPopStagger = 0.06f;
        private static readonly Color CardOwnerA = new Color(0.45f, 0.85f, 1f);
        private static readonly Color CardOwnerB = new Color(1f, 0.65f, 0.35f);
        private static readonly Color CardOwnerShared = new Color(1f, 0.96f, 0.84f);

        private GUIStyle cardFrameStyle;
        private bool cardArtLoaded;
        private float traitWindowOpenedAt;
        private float relicWindowOpenedAt;

        private static Color CardOwnerColor(string owner) =>
            owner == "A" ? CardOwnerA : owner == "B" ? CardOwnerB : CardOwnerShared;

        // 둥근 패널 + 소유자 색 테두리. 그림이 없으면 패널만.
        private void DrawCard(Rect card, string owner)
        {
            if (!cardArtLoaded)
            {
                cardArtLoaded = true;
                var texture = Resources.Load<Texture2D>("UI/HUD/CardFrame");
                if (texture != null)
                {
                    cardFrameStyle = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
                    cardFrameStyle.normal.background = texture;
                }
            }

            GUI.Box(card, GUIContent.none, panel);
            if (cardFrameStyle == null) return;
            var previous = GUI.color;
            GUI.color = CardOwnerColor(owner);
            DrawStyle(cardFrameStyle, card);
            GUI.color = previous;
        }

        // index번째 카드의 팝 크기 (0.6배 → 1.08배 → 1배). 창을 연 시각 openedAt 기준.
        private static float CardPopScale(float openedAt, int index)
        {
            float t = (Time.unscaledTime - openedAt - index * CardPopStagger) / CardPopTime;
            if (t >= 1f) return 1f;
            if (t <= 0f) return 0.6f;
            if (t < 0.65f) return Mathf.Lerp(0.6f, 1.08f, Mathf.SmoothStep(0f, 1f, t / 0.65f));
            return Mathf.Lerp(1.08f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.65f) / 0.35f));
        }

        // 카드 하나를 그리기 전에 부른다. 돌려받은 행렬을 그린 뒤 GUI.matrix에 되돌린다.
        private static Matrix4x4 BeginCardPop(Rect card, float openedAt, int index)
        {
            var previous = GUI.matrix;
            float scale = CardPopScale(openedAt, index);
            if (scale != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * scale, card.center);
            return previous;
        }

        // 마일스톤 달성 줄 앞의 작은 별 (HUD 마나 별 그림 재사용)
        private void DrawMilestoneStar(Rect line, bool unlocked)
        {
            EnsureBarArt();
            if (starIcon == null) return;
            var previous = GUI.color;
            GUI.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.55f, 0.6f);
            DrawIcon(starIcon, new Rect(line.x, line.y + 1f, 14f, 14f), 1f);
            GUI.color = previous;
        }
    }
}
