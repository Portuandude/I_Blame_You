using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 플레이어 사망 연출 (사망 애니메이션이 생기기 전까지의 대체 연출).
    // 1) 쓰러진 순간 슬로모션 (시간 0.25배, 실제 시간 1.2초 뒤 정상 속도로 복귀)
    // 2) 캐릭터가 회색으로 바래며 반투명해짐
    // 3) 화면이 서서히 어두워진 뒤 게임오버 창이 떠오름. 창이 다 뜨기 전에는 R/M이 먹지 않는다 (연타로 바로 넘어가지 않게).
    // 연출 시간은 모두 Time.unscaledTime 기준이라 슬로모션의 영향을 받지 않는다.
    public partial class SkillHUD
    {
        private const float DeathSlowScale = 0.25f;
        private const float DeathSlowDuration = 1.2f;   // 실제 시간(초)
        private const float DeathTintDuration = 1.0f;
        private const float DeathFadeDelay = 0.3f;
        private const float DeathFadeDuration = 1.2f;
        private const float DeathFadeAlpha = 0.6f;
        private const float GameOverDelay = 1.4f;       // 쓰러지고 게임오버 창이 뜨기 시작할 때까지
        private const float GameOverFadeIn = 0.4f;

        private static readonly Color DeadTint = new Color(0.5f, 0.5f, 0.5f, 0.7f);

        private float deathStartTime;
        private bool deathSlowMotion;
        private SpriteRenderer deathVisual;
        private Color deathVisualStart = Color.white;

        private float DeathElapsed => Time.unscaledTime - deathStartTime;

        // 게임오버 창이 다 떠서 R/M 입력을 받을 수 있는지.
        private bool GameOverReady => deathHandled && DeathElapsed >= GameOverDelay + GameOverFadeIn;

        private void BeginDeathSequence()
        {
            deathStartTime = Time.unscaledTime;
            deathSlowMotion = true;
            Time.timeScale = DeathSlowScale;

            deathVisual = health.GetComponentInChildren<SpriteRenderer>();
            if (deathVisual != null) deathVisualStart = deathVisual.color;
            GameAudio.PlaySfx("Hit_Earth", 0.8f);
        }

        private void UpdateDeathSequence()
        {
            if (!deathHandled) return;

            float elapsed = DeathElapsed;
            if (deathSlowMotion && elapsed >= DeathSlowDuration) EndDeathSlowMotion();

            if (deathVisual != null)
                deathVisual.color = Color.Lerp(deathVisualStart, DeadTint, Mathf.Clamp01(elapsed / DeathTintDuration));
        }

        private void EndDeathSlowMotion()
        {
            if (!deathSlowMotion) return;
            deathSlowMotion = false;
            Time.timeScale = 1f;
        }

        private void DrawDeathFade()
        {
            if (!deathHandled) return;

            float alpha = DeathFadeAlpha * Mathf.Clamp01((DeathElapsed - DeathFadeDelay) / DeathFadeDuration);
            if (alpha <= 0f) return;

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // 게임오버 창의 불투명도 (0이면 아직 그리지 않는다).
        private float GameOverAlpha => Mathf.Clamp01((DeathElapsed - GameOverDelay) / GameOverFadeIn);
    }
}
