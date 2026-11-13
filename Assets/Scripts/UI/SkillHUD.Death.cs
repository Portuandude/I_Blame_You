using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 플레이어 사망 연출 (사망 애니메이션이 생기기 전까지의 대체 연출).
    // 1) 쓰러진 순간 슬로모션 (시간 0.25배, 실제 시간 1.2초 뒤 정상 속도로 복귀)
    // 2) 캐릭터가 회색으로 바래며 반투명해짐
    // 3) 화면이 서서히 어두워진 뒤 게임오버 창이 떠오름. 창이 다 뜨기 전에는 R/M이 먹지 않는다 (연타로 바로 넘어가지 않게).
    // 0) 쓰러진 순간 화면이 하얗게 번쩍(0.15초)하고 카메라가 짧게 흔들린다
    // 4) 쓰러짐 소리 PlayerDown (Tools/AudioGen), 음악은 테이프처럼 느려지며(빠르기 0.6) 먹먹해진다. 게임오버 징글 때 꺼진다 (SkillHUD.EndScreen.cs)
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

        private const float DeathFlashTime = 0.15f;     // 실제 시간
        private const float DeathFlashAlpha = 0.55f;
        private const float DeathShakeTime = 0.08f;     // 카메라는 게임 시간으로 흔들므로 슬로모션(0.25배) 동안 실제 약 0.3초
        private const float DeathShakeMagnitude = 0.14f;
        private const float DeathMusicPitch = 0.6f;     // GameAudio가 초당 0.5씩 옮기므로 약 0.8초에 걸쳐 느려진다

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
            CameraFollow.ShakeMain(DeathShakeTime, DeathShakeMagnitude);
            GameAudio.PlaySfx("PlayerDown", 0.85f, 0f);
            GameAudio.SetMusicPitch(DeathMusicPitch); // 다음 곡을 틀면 1로 돌아간다
            GameAudio.SetMusicMuffled(true);          // 씬이 정리될 때 OnDestroy가 되돌린다 (SkillHUD.cs)
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

            var previous = GUI.color;
            float alpha = DeathFadeAlpha * Mathf.Clamp01((DeathElapsed - DeathFadeDelay) / DeathFadeDuration);
            if (alpha > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }

            // 쓰러진 순간 하얀 번쩍임 (어둠보다 먼저 끝난다)
            float flash = DeathFlashAlpha * (1f - Mathf.Clamp01(DeathElapsed / DeathFlashTime));
            if (flash > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, flash);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        // 게임오버 창의 불투명도 (0이면 아직 그리지 않는다).
        private float GameOverAlpha => Mathf.Clamp01((DeathElapsed - GameOverDelay) / GameOverFadeIn);
    }
}
