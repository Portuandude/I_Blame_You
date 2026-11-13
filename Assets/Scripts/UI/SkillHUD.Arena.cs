using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 전투 구역(ArenaZone)에서 문이 닫혀 싸우는 동안: 화면 위쪽 가운데에 "남은 적 N" 알약 (줄어들 때마다 1.3배로 톡 튐).
    // 남은 수 = 살아 있는 적 + 아직 안 나온 적. 하나 남으면 금색 "마지막 하나!". 보스 구역은 그 자리에 보스 체력바가 있어 그리지 않는다.
    public partial class SkillHUD
    {
        private const float ArenaPopTime = 0.25f;
        private const float ArenaPillWidth = 150f;
        private const float ArenaPillHeight = 30f;
        private static readonly Color ArenaCountColor = new Color(1f, 0.55f, 0.5f); // 문(마력 장벽)과 같은 붉은 계열

        private ArenaZone arenaShown;   // 지금 세는 구역 (구역이 바뀌면 처음부터)
        private int arenaLastCount = -1;
        private float arenaPopStart = -10f;
        private GUIStyle arenaText;

        private void DrawArenaCount()
        {
            var zone = ArenaZone.Current;
            if (zone == null || deathHandled || stageCleared)
            {
                arenaShown = null;
                arenaLastCount = -1;
                return;
            }

            int count = zone.Remaining;
            if (zone != arenaShown)
            {
                arenaShown = zone;
                arenaLastCount = count;
            }
            else if (count < arenaLastCount)
            {
                arenaPopStart = Time.unscaledTime;
                arenaLastCount = count;
            }
            else arenaLastCount = count;

            if (zone.HasBoss || count <= 0) return;

            if (arenaText == null)
                arenaText = new GUIStyle(label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };

            var rect = new Rect((Screen.width - ArenaPillWidth) / 2f, 12f, ArenaPillWidth, ArenaPillHeight);
            float t = Mathf.Clamp01((Time.unscaledTime - arenaPopStart) / ArenaPopTime);
            float pop = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
            GUI.Box(rect, GUIContent.none, panel);
            var previous = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), rect.center);
            // 하나 남으면 금색 "마지막 하나!" (머리 위 ▼와 함께, SkillHUD.LastEnemy.cs)
            if (count == 1) OutlinedLabel.Draw(rect, "마지막 하나!", arenaText, OutlinedLabel.Gold, OutlinedLabel.Outline, 0f, 1.5f);
            else OutlinedLabel.Draw(rect, $"남은 적 {count}", arenaText, ArenaCountColor, OutlinedLabel.Outline, 0f, 1.5f);
            GUI.matrix = previous;
        }
    }
}
