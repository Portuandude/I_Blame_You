using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.UI
{
    // 화면 위쪽 가운데 보스 체력바 (왕관 + 캡슐 바 + 흰 잔상, 5% 넘게 깎이면 흔들림). 보스가 분열하면 몸 수와 합친 체력을 보여주고,
    // 소환한 무리가 있으면 합체까지 남은 시간을 아래에 적는다 (BossEvents).
    // 보스(중간보스 포함)가 처음 나타나면 화면 가운데에 이름 띠를 잠깐 크게 띄운다 (귀엽지만 화려하게: gold 글자 + 반짝 테두리).
    public partial class SkillHUD
    {
        private const float IntroTime = 2f;
        private const float IntroFade = 0.35f;
        private bool bossWasActive;
        private float bossIntroStart = -10f;
        private string bossIntroName;
        private GUIStyle introTitle;
        private GUIStyle introSub;

        private const float BossShakeDrop = 0.05f;   // 한 프레임에 이만큼(5%) 이상 깎이면 흔들림
        private const float BossShakeTime = 0.25f;
        private static readonly Color BossFillColor = new Color(0.78f, 0.45f, 1f);   // arcane 보라
        private static readonly Color BossGlowColor = new Color(1f, 0.85f, 0.3f);    // gold
        private BarTrail bossTrail;
        private float bossShakeStart = -10f;

        private void DrawBossBar()
        {
            bool active = BossEvents.Active;
            if (active && !bossWasActive)
            {
                bossIntroStart = Time.unscaledTime;
                bossIntroName = BossEvents.Name;
            }

            bossWasActive = active;
            DrawBossIntro();
            if (!active) return;

            float width = Mathf.Min(560f, Screen.width - 2f * 400f);
            if (width < 280f) width = 280f;
            var rect = new Rect((Screen.width - width) / 2f, 12f, width, 58f);
            GUI.Box(rect, GUIContent.none, panel);

            int alive = BossEvents.AliveCount;
            string title = alive > 1 ? $"{BossEvents.Name} ×{alive}" : BossEvents.Name;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 2f, rect.width - 20f, 24f),
                $"<b>{title}</b>  {BossEvents.Current:0} / {BossEvents.Max:0}", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });

            // 왕관 아이콘 + 캡슐 바 (SkillHUD.Bars.cs). 한 번에 크게 깎이면 바가 잠깐 흔들린다.
            float ratio = BossEvents.Max > 0f ? Mathf.Clamp01(BossEvents.Current / BossEvents.Max) : 0f;
            if (Event.current.type == EventType.Repaint)
            {
                if (bossTrail.Started && bossTrail.Last - ratio >= BossShakeDrop) bossShakeStart = Time.unscaledTime;
                bossTrail.Step(ratio, Time.unscaledTime, Time.unscaledDeltaTime);
            }

            float shakeAge = Time.unscaledTime - bossShakeStart;
            float shake = shakeAge < BossShakeTime ? Mathf.Sin(shakeAge * 70f) * 3f * (1f - shakeAge / BossShakeTime) : 0f;
            var icon = new Rect(rect.x + 10f + shake, rect.y + 26f, BarIconSize, BarIconSize);
            var bar = new Rect(icon.xMax + 4f, rect.y + 29f, rect.xMax - icon.xMax - 4f - 10f - BarGlow, BarHeight);
            DrawCapsuleBar(bar, ratio, bossTrail.Shown, BossFillColor, BossGlowColor, null);
            DrawIcon(crownIcon, icon, 1f);

            if (!string.IsNullOrEmpty(BossEvents.Notice))
                GUI.Label(new Rect(rect.x, rect.yMax + 2f, rect.width, 22f), $"<color=#ffd84d>{BossEvents.Notice}</color>",
                    new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }
    
        private void DrawBossIntro()
        {
            float since = Time.unscaledTime - bossIntroStart;
            if (since < 0f || since > IntroTime || string.IsNullOrEmpty(bossIntroName)) return;

            if (introTitle == null)
            {
                introTitle = new GUIStyle(big) { fontSize = 40, richText = true };
                introSub = new GUIStyle(small) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }

            // 들어올 때 옆에서 미끄러져 들어오고, 나갈 때 흐려진다.
            float alpha = Mathf.Min(1f, since / IntroFade, (IntroTime - since) / IntroFade);
            float slide = (1f - Mathf.Clamp01(since / IntroFade)) * 120f;
            float height = 96f;
            var band = new Rect(0f, Screen.height * 0.32f - height / 2f, Screen.width, height);

            var previous = GUI.color;
            GUI.color = new Color(0.08f, 0.06f, 0.16f, 0.72f * alpha); // 팔레트 night 띠
            GUI.DrawTexture(band, Texture2D.whiteTexture);
            float shimmer = 0.6f + 0.4f * Mathf.Sin(since * 10f);
            GUI.color = new Color(1f, 0.85f, 0.3f, alpha * shimmer); // gold 위아래 테두리
            GUI.DrawTexture(new Rect(band.x, band.y, band.width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(band.x, band.yMax - 3f, band.width, 3f), Texture2D.whiteTexture);

            var titleRect = new Rect(band.x + slide, band.y + 10f, band.width, 52f);
            GUI.color = new Color(0.17f, 0.1f, 0.24f, alpha); // 외곽선 대신 진한 그림자
            GUI.Label(new Rect(titleRect.x + 3f, titleRect.y + 3f, titleRect.width, titleRect.height), bossIntroName, introTitle);
            GUI.color = new Color(1f, 0.85f, 0.3f, alpha);
            GUI.Label(titleRect, bossIntroName, introTitle);
            GUI.color = new Color(1f, 1f, 1f, 0.85f * alpha);
            GUI.Label(new Rect(band.x - slide, band.y + 60f, band.width, 26f), "강적 등장!", introSub);
            GUI.color = previous;
        }
    }
}
