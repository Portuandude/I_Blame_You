using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.UI
{
    // 골드 표시 (Core/RunGold.cs): 스테이지 막대 오른쪽에 금화 아이콘 + 숫자 알약. 숫자는 아직 날아오는 동전 몫을 빼고 보여 주고,
    // 동전이 닿을 때마다 1.25배로 톡 튀었다가 돌아온다 (unscaledTime). 결과 창 오른쪽 위에도 이번 판 골드를 적는다.
    public partial class SkillHUD
    {
        private const float GoldPopTime = 0.25f;
        private static readonly Rect GoldPillRect = new Rect(380f, 168f, 120f, 30f); // 스테이지 막대 줄 오른쪽 (위쪽 가운데는 보스 막대 자리)

        private Texture2D coinIcon;
        private bool coinIconLoaded;
        private float goldPopStart = -10f;
        private GUIStyle goldText;

        private void OnGoldArrived(int value) => goldPopStart = Time.unscaledTime;

        private void EnsureGoldArt()
        {
            if (!coinIconLoaded)
            {
                coinIconLoaded = true;
                coinIcon = Resources.Load<Texture2D>("Effects/Coin/00");
            }
            if (goldText == null)
                goldText = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        }

        private void DrawGoldPill()
        {
            EnsureGoldArt();
            var rect = GoldPillRect;
            GUI.Box(rect, GUIContent.none, panel);
            float t = Mathf.Clamp01((Time.unscaledTime - goldPopStart) / GoldPopTime);
            float pop = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
            var icon = new Rect(rect.x + 6f, rect.y + 3f, 24f, 24f);
            if (coinIcon != null) GUI.DrawTexture(ScaleAround(icon, pop), coinIcon, ScaleMode.ScaleToFit, true);
            int shown = Mathf.Max(0, RunStats.Gold - RunGold.InFlight);
            var previous = GUI.matrix;
            var textRect = new Rect(rect.x + 36f, rect.y, rect.width - 40f, rect.height);
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(textRect.x, textRect.center.y));
            OutlinedLabel.Draw(textRect, shown.ToString(), goldText, OutlinedLabel.Gold, OutlinedLabel.Outline, 0f, 1.5f);
            GUI.matrix = previous;
        }

        // 결과 창 오른쪽 위: 금화 + "골드 N"
        private void DrawEndGold(Rect panelRect)
        {
            EnsureGoldArt();
            var row = new Rect(panelRect.xMax - 150f, panelRect.y + 10f, 140f, 28f);
            if (coinIcon != null) GUI.DrawTexture(new Rect(row.x, row.y + 2f, 24f, 24f), coinIcon, ScaleMode.ScaleToFit, true);
            OutlinedLabel.Draw(new Rect(row.x + 28f, row.y, row.width - 28f, row.height), $"골드 {RunStats.Gold}", goldText,
                OutlinedLabel.Gold, OutlinedLabel.Outline, 0f, 1.5f);
        }

        private static Rect ScaleAround(Rect r, float s) =>
            new Rect(r.center.x - r.width * s / 2f, r.center.y - r.height * s / 2f, r.width * s, r.height * s);
    }
}
