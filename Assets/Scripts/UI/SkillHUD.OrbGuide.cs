using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 화면 밖 보상 구슬 길잡이: 아직 안 주운 하트 구슬(ArenaReward)이 카메라 밖에 있으면 화면 가장자리에 작은 분홍 하트와
    // 구슬 쪽을 가리키는 화살표를 띄운다. 가까운 것(OrbGuideRange 유닛 안 = 대개 같은 방·옆 방)만, 가장 가까운 MaxOrbGuides개까지.
    public partial class SkillHUD
    {
        private const float OrbGuideRange = 48f;   // 카메라 가운데에서 이 거리 안의 구슬만
        private const float OrbGuideMargin = 28f;  // 화면 가장자리에서 띄우는 픽셀
        private const float OrbGuideSize = 22f;
        private const int MaxOrbGuides = 3;
        private static readonly Color OrbGuideColor = new Color(1f, 0.55f, 0.7f);
        private readonly System.Collections.Generic.List<(float distance, Vector3 position)> orbGuideTargets =
            new System.Collections.Generic.List<(float, Vector3)>();

        private void DrawOrbGuides()
        {
            if (deathHandled || stageCleared || heartIcon == null) return;
            var cam = Camera.main;
            if (cam == null || ArenaReward.Active.Count == 0) return;

            Vector3 center = cam.transform.position;
            orbGuideTargets.Clear();
            foreach (var orb in ArenaReward.Active)
            {
                if (orb == null || orb.Collected) continue;
                var p = orb.OrbPosition;
                float d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(center.x, center.y));
                if (d > OrbGuideRange) continue;
                var vp = cam.WorldToViewportPoint(p);
                if (vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f) continue; // 화면 안이면 길잡이 없음
                orbGuideTargets.Add((d, p));
            }
            if (orbGuideTargets.Count == 0) return;
            orbGuideTargets.Sort((a, b) => a.distance.CompareTo(b.distance));

            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < orbGuideTargets.Count && i < MaxOrbGuides; i++)
            {
                // 화면 가운데에서 구슬 쪽으로 그은 선이 가장자리(여백 안쪽)와 만나는 점 (SkillHUD.EnemyGuide.cs)
                if (!TryEdgePoint(cam, orbGuideTargets[i].position, OrbGuideMargin, out var at, out var dir)) continue;

                GUI.color = new Color(OrbGuideColor.r, OrbGuideColor.g, OrbGuideColor.b, pulse);
                var heart = new Rect(at.x - OrbGuideSize / 2f, at.y - OrbGuideSize / 2f, OrbGuideSize, OrbGuideSize);
                GUI.DrawTexture(heart, heartIcon, ScaleMode.ScaleToFit, true);
                // 하트 바깥쪽(구슬 쪽)에 작은 화살표
                var n = dir.normalized;
                var arrowCenter = at + n * (OrbGuideSize * 0.75f);
                float angle = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(angle, arrowCenter);
                GUI.Label(new Rect(arrowCenter.x - 8f, arrowCenter.y - 10f, 16f, 20f), "<color=#ff8cb3><b>▶</b></color>",
                    new GUIStyle(small) { richText = true, alignment = TextAnchor.MiddleCenter });
                GUI.matrix = previousMatrix;
            }
            GUI.color = previousColor;
        }
    }
}
