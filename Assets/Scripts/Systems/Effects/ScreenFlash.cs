using UnityEngine;

namespace IBlameYou.Systems
{
    // 마법이 적에게 맞는 순간 화면을 아주 살짝 하얗게 번쩍인다 (Docs/ArtStyle.md: 알파 0.08, 0.05초).
    // 여러 발이 한꺼번에 맞아도 눈이 아프지 않게 MinInterval 안의 번쩍임은 무시한다.
    // 방을 옮길 때는 RoomFade로 화면을 잠깐 어둡게 덮었다가 밝힌다 (Docs/LevelDesign.md 방 전환 연출).
    public class ScreenFlash : MonoBehaviour
    {
        private const float DefaultAlpha = 0.08f;
        private const float Duration = 0.05f;
        private const float FadeTime = 0.08f;
        private const float MinInterval = 0.12f;
        private const float RoomFadeAlpha = 0.85f;
        private const float RoomFadeHold = 0.04f;  // 옮긴 직전 프레임이 비치지 않게 잠깐 어둡게 유지
        private const float RoomFadeTime = 0.15f;  // 어둡게 → 밝게
        private static readonly Color RoomFadeColor = new Color(0.08f, 0.06f, 0.16f); // 팔레트 night보다 조금 더 어둡게
        private const int SortingOrder = 900; // 월드 그림 전부보다 앞 (IMGUI HUD는 그 위에 따로 그려진다)

        private static ScreenFlash instance;
        private static float lastPulse = -10f;

        private SpriteRenderer overlay;
        private float startedAt = -10f;
        private float peak;
        private Color color = Color.white;
        private float hold = Duration;
        private float fade = FadeTime;

        public static void Pulse(float alpha = DefaultAlpha)
        {
            if (Time.unscaledTime - lastPulse < MinInterval) return;
            var flash = Find();
            if (flash == null || flash.CurrentAlpha() > alpha) return; // 더 진한 덮개(방 전환)가 보이는 중이면 덮어쓰지 않는다

            lastPulse = Time.unscaledTime;
            flash.Begin(Color.white, alpha, Duration, FadeTime);
        }

        // 방 문을 지나 옆 방으로 옮긴 순간 부른다: 어둡게 덮은 채 시작해서 0.15초 동안 밝아진다.
        public static void RoomFade()
        {
            var flash = Find();
            if (flash != null) flash.Begin(RoomFadeColor, RoomFadeAlpha, RoomFadeHold, RoomFadeTime);
        }

        private static ScreenFlash Find()
        {
            var cam = Camera.main;
            if (cam == null) return null;

            if (instance == null || instance.gameObject != cam.gameObject)
            {
                instance = cam.GetComponent<ScreenFlash>();
                if (instance == null) instance = cam.gameObject.AddComponent<ScreenFlash>();
            }

            return instance;
        }

        private float CurrentAlpha()
        {
            if (overlay == null || !overlay.enabled) return 0f;
            float since = Time.unscaledTime - startedAt;
            return since <= hold ? peak : peak * (1f - (since - hold) / fade);
        }

        private void Begin(Color tint, float alpha, float holdTime, float fadeTime)
        {
            if (overlay == null)
            {
                var go = new GameObject("ScreenFlash");
                go.transform.SetParent(transform, false);
                overlay = go.AddComponent<SpriteRenderer>();
                overlay.sprite = SolidSpriteFactory.CreateSquare();
                overlay.sortingOrder = SortingOrder;
            }

            color = tint;
            peak = alpha;
            hold = holdTime;
            fade = fadeTime;
            startedAt = Time.unscaledTime;
            overlay.enabled = true;
            UpdateOverlay();
        }

        private void LateUpdate()
        {
            if (overlay == null || !overlay.enabled) return;
            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            var cam = GetComponent<Camera>();
            float a = CurrentAlpha();
            if (a <= 0f || cam == null)
            {
                overlay.enabled = false;
                return;
            }

            float height = cam.orthographicSize * 2f + 1f;
            overlay.transform.localPosition = new Vector3(0f, 0f, 1f); // 카메라 앞 (z = -10 + 1)
            overlay.transform.localScale = new Vector3(height * cam.aspect + 1f, height, 1f);
            overlay.color = new Color(color.r, color.g, color.b, a);
        }
    }
}
