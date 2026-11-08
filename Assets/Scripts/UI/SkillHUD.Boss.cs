using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.UI
{
    // 화면 위쪽 가운데 보스 체력바. 보스가 분열하면 몸 수와 합친 체력을 보여주고,
    // 소환한 무리가 있으면 합체까지 남은 시간을 아래에 적는다 (BossEvents).
    public partial class SkillHUD
    {
        private void DrawBossBar()
        {
            if (!BossEvents.Active) return;

            float width = Mathf.Min(560f, Screen.width - 2f * 400f);
            if (width < 280f) width = 280f;
            var rect = new Rect((Screen.width - width) / 2f, 12f, width, 52f);
            GUI.Box(rect, GUIContent.none, panel);

            int alive = BossEvents.AliveCount;
            string title = alive > 1 ? $"{BossEvents.Name} ×{alive}" : BossEvents.Name;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 2f, rect.width - 20f, 24f),
                $"<b>{title}</b>  {BossEvents.Current:0} / {BossEvents.Max:0}", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });

            var back = new Rect(rect.x + 12f, rect.y + 30f, rect.width - 24f, 12f);
            GUI.DrawTexture(back, Texture2D.grayTexture);
            float ratio = BossEvents.Max > 0f ? Mathf.Clamp01(BossEvents.Current / BossEvents.Max) : 0f;
            var previous = GUI.color;
            GUI.color = new Color(0.75f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * ratio, back.height), Texture2D.whiteTexture);
            GUI.color = previous;

            if (!string.IsNullOrEmpty(BossEvents.Notice))
                GUI.Label(new Rect(rect.x, rect.yMax + 2f, rect.width, 22f), $"<color=#ffd84d>{BossEvents.Notice}</color>",
                    new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
