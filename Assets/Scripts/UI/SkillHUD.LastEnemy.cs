using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 마지막 적 표시: 전투 구역(ArenaZone.Current)에 적이 하나만 남으면(더 나올 적도 없을 때) 그 적 머리 위에
    // 붉은 ▼가 통통 튄다. 적이 화면 밖이면 화면 밖 적 길잡이(SkillHUD.EnemyGuide.cs)가 대신 가리킨다. 보스 구역은 보스 체력바로 충분해 그리지 않는다.
    public partial class SkillHUD
    {
        private const float LastEnemyMarkLift = 0.55f;  // 몸 윗면에서 띄우는 높이 (월드)
        private const float LastEnemyBobPixels = 5f;
        private GUIStyle lastEnemyMark;

        private void DrawLastEnemyMark()
        {
            var zone = ArenaZone.Current;
            if (zone == null || zone.HasBoss || zone.Remaining != 1 || deathHandled || stageCleared) return;
            var cam = Camera.main;
            if (cam == null) return;

            HealthSystem last = null;
            foreach (var enemy in zone.AliveEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                last = enemy;
                break;
            }
            if (last == null) return;

            var body = last.GetComponent<Collider2D>();
            Vector3 top = body != null && body.enabled
                ? new Vector3(body.bounds.center.x, body.bounds.max.y + LastEnemyMarkLift, 0f)
                : last.transform.position + Vector3.up * 1.5f;
            var vp = cam.WorldToViewportPoint(top);
            if (vp.z < 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return;

            if (lastEnemyMark == null)
                lastEnemyMark = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };

            var screen = cam.WorldToScreenPoint(top);
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * LastEnemyBobPixels;
            var rect = new Rect(screen.x - 14f, Screen.height - screen.y - 14f - bob, 28f, 28f);
            OutlinedLabel.Draw(rect, "▼", lastEnemyMark, ArenaCountColor, OutlinedLabel.Outline, 0f, 1.5f);
        }
    }
}
