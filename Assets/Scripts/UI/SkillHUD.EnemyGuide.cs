using System.Collections.Generic;
using IBlameYou.Enemies;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 화면 밖 적 길잡이: 문이 닫힌 전투 구역(ArenaZone.Current)에서 살아 있는 적이 카메라 밖에 있으면
    // 화면 가장자리에 그 적 쪽을 가리키는 붉은 화살표를 띄운다 (보상 구슬 길잡이 SkillHUD.OrbGuide.cs와 같은 방식).
    // 가장 가까운 MaxEnemyGuides개까지. 구역 밖의 적(지나온 방 등)은 세지 않는다.
    public partial class SkillHUD
    {
        private const float EnemyGuideMargin = 22f;
        private const int MaxEnemyGuides = 4;
        private const float EnemyGuideBodyLift = 0.6f; // 발 위치에서 몸 가운데쯤으로
        private readonly List<(float distance, Vector3 position)> enemyGuideTargets = new List<(float, Vector3)>();
        private GUIStyle enemyGuideArrow;

        // 화면 밖 궁수 경고: 시위를 당기는 궁수(GoblinArcherController.Aiming)가 카메라 밖(AimWarnRange 안)에 있으면
        // 그 쪽 화면 가장자리에 노란 "!"가 빠르게 깜빡인다 (전투 구역 밖에서도). 곧 화살이 날아온다는 예고.
        private const float AimWarnRange = 24f;
        private const float AimWarnMargin = 30f;
        private static readonly Color AimWarnColor = new Color(1f, 0.85f, 0.2f); // 적 발견 "!"와 같은 노랑
        private GUIStyle aimWarnText;

        private void DrawAimWarnings()
        {
            if (deathHandled || stageCleared || GoblinArcherController.Aiming.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (aimWarnText == null)
                aimWarnText = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };

            Vector3 center = cam.transform.position;
            var mid = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 14f)); // 빠른 깜빡임
            foreach (var archer in GoblinArcherController.Aiming)
            {
                if (archer == null) continue;
                var p = archer.AimPoint;
                if (Vector2.Distance(p, center) > AimWarnRange) continue;
                var vp = cam.WorldToViewportPoint(p);
                if (vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f) continue; // 화면 안이면 화살 끝 반짝으로 충분
                var screen = cam.WorldToScreenPoint(p);
                var dir = new Vector2(screen.x, Screen.height - screen.y) - mid;
                if (screen.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 0.01f) continue;
                float halfW = mid.x - AimWarnMargin, halfH = mid.y - AimWarnMargin;
                float scale = Mathf.Min(halfW / Mathf.Max(0.001f, Mathf.Abs(dir.x)), halfH / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                var at = mid + dir * scale;
                OutlinedLabel.Draw(new Rect(at.x - 16f, at.y - 18f, 32f, 36f), "!", aimWarnText, AimWarnColor, OutlinedLabel.Outline, 0f, 2f);
            }
            GUI.color = previousColor;
        }

        private void DrawEnemyGuides()
        {
            var zone = ArenaZone.Current;
            if (zone == null || deathHandled || stageCleared) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 center = cam.transform.position;
            enemyGuideTargets.Clear();
            foreach (var enemy in zone.AliveEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                var p = enemy.transform.position + Vector3.up * EnemyGuideBodyLift;
                var vp = cam.WorldToViewportPoint(p);
                if (vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f) continue; // 화면 안이면 길잡이 없음
                enemyGuideTargets.Add((Vector2.Distance(p, center), p));
            }
            if (enemyGuideTargets.Count == 0) return;
            enemyGuideTargets.Sort((a, b) => a.distance.CompareTo(b.distance));

            if (enemyGuideArrow == null)
                enemyGuideArrow = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };

            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
            var mid = new Vector2(Screen.width / 2f, Screen.height / 2f);
            for (int i = 0; i < enemyGuideTargets.Count && i < MaxEnemyGuides; i++)
            {
                var screen = cam.WorldToScreenPoint(enemyGuideTargets[i].position);
                var dir = new Vector2(screen.x, Screen.height - screen.y) - mid; // GUI는 y가 아래로
                if (screen.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 0.01f) continue;
                // 화면 가운데에서 적 쪽으로 그은 선이 가장자리(여백 안쪽)와 만나는 점
                float halfW = mid.x - EnemyGuideMargin, halfH = mid.y - EnemyGuideMargin;
                float scale = Mathf.Min(halfW / Mathf.Max(0.001f, Mathf.Abs(dir.x)), halfH / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                var at = mid + dir * scale;

                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(angle, at);
                GUI.color = new Color(1f, 1f, 1f, pulse);
                OutlinedLabel.Draw(new Rect(at.x - 14f, at.y - 14f, 28f, 28f), "▶", enemyGuideArrow, ArenaCountColor, OutlinedLabel.Outline, 0f, 1.5f);
                GUI.matrix = previousMatrix;
            }
            GUI.color = previousColor;
        }
    }
}
