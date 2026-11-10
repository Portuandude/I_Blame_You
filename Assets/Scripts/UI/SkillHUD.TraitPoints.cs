using UnityEngine;

namespace IBlameYou.UI
{
    // 레벨 막대 아래 "특성 포인트 N (Tab)" 안내를 눈에 띄게 (귀엽지만 화려하게).
    // gold 캡슐(BarFill 그림을 gold로 물들임) 위에 진한 글자 + 바깥 gold 빛 테두리(BarFrame)가 숨 쉬듯 밝아졌다 어두워진다.
    // 포인트가 늘어난 순간 한 번 팝(0.6배 → 1.2배 → 1배). 시간은 unscaledTime이라 특성 창(timeScale 0)이 열려 있어도 움직인다.
    public partial class SkillHUD
    {
        private const float TraitPillHeight = 20f;    // 레벨 막대(204+30) 아래 ~ 유물 목록(256) 사이에 들어가는 높이
        private const float TraitPillPopTime = 0.4f;
        private const float TraitPillBreathPeriod = 1.6f;
        private static readonly Color TraitPillGold = new Color(1f, 0.85f, 0.3f);      // palette.py gold
        private static readonly Color TraitPillInk = new Color(0.16f, 0.1f, 0.25f);    // 진한 보라 글자

        private GUIStyle traitPillText;
        private int traitPillLastPoints = -1; // -1: 아직 안 봄 (불러온 런에서 처음부터 있던 포인트는 팝하지 않는다)
        private float traitPillPopStart = -10f;

        private void DrawTraitPointsPill(Vector2 topLeft)
        {
            int points = progression.Points;
            if (traitPillLastPoints >= 0 && points > traitPillLastPoints) traitPillPopStart = Time.unscaledTime;
            traitPillLastPoints = points;
            if (points <= 0) return;

            EnsureBarArt(); // barFillStyle·barFrameStyle (SkillHUD.Bars.cs)
            if (traitPillText == null)
            {
                traitPillText = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, richText = false, padding = new RectOffset(0, 0, 0, 0) };
                traitPillText.normal.textColor = TraitPillInk;
            }

            var content = new GUIContent($"특성 포인트 {points} (Tab)");
            float width = traitPillText.CalcSize(content).x + 26f;
            var pill = new Rect(topLeft.x + BarGlow, topLeft.y, width, TraitPillHeight);

            // 숨쉬기: 0~1을 오가는 값
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / TraitPillBreathPeriod));

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            GUIUtility.ScaleAroundPivot(Vector2.one * TraitPillPopScale(Time.unscaledTime - traitPillPopStart), pill.center);

            GUI.color = new Color(TraitPillGold.r, TraitPillGold.g, TraitPillGold.b, Mathf.Lerp(0.25f, 0.9f, breath));
            DrawStyle(barFrameStyle, new Rect(pill.x - BarGlow, pill.y - BarGlow, pill.width + 2f * BarGlow, pill.height + 2f * BarGlow));
            GUI.color = Color.Lerp(TraitPillGold * 0.88f, Color.Lerp(TraitPillGold, Color.white, 0.35f), breath);
            DrawStyle(barFillStyle, pill);

            GUI.color = previousColor;
            GUI.Label(pill, content, traitPillText);
            GUI.matrix = previousMatrix;
        }

        // 0.6배에서 1.2배까지 커졌다가 1배로 (TraitPillPopTime 동안). 그 뒤는 1배.
        private static float TraitPillPopScale(float age)
        {
            if (age < 0f || age >= TraitPillPopTime) return 1f;
            float t = age / TraitPillPopTime;
            if (t < 0.55f) return Mathf.Lerp(0.6f, 1.2f, Mathf.SmoothStep(0f, 1f, t / 0.55f));
            return Mathf.Lerp(1.2f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
        }
    }
}
