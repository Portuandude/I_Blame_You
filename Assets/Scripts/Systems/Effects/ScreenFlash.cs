using UnityEngine;

namespace IBlameYou.Systems
{
    // 마법이 적에게 맞는 순간 화면을 아주 살짝 하얗게 번쩍인다 (Docs/ArtStyle.md: 알파 0.08, 0.05초).
    // 여러 발이 한꺼번에 맞아도 눈이 아프지 않게 MinInterval 안의 번쩍임은 무시한다.
    public class ScreenFlash : MonoBehaviour
    {
        private const float DefaultAlpha = 0.08f;
        private const float Duration = 0.05f;
        private const float FadeTime = 0.08f;
        private const float MinInterval = 0.12f;
        private const int SortingOrder = 900; // 월드 그림 전부보다 앞 (IMGUI HUD는 그 위에 따로 그려진다)

        private static ScreenFlash instance;
        private static float lastPulse = -10f;

        private SpriteRenderer overlay;
        private float startedAt = -10f;
        private float peak;

        public static void Pulse(float alpha = DefaultAlpha)
        {
            if (Time.unscaledTime - lastPulse < MinInterval) return;
            var cam = Camera.main;
            if (cam == null) return;

            if (instance == null || instance.gameObject != cam.gameObject)
            {
                instance = cam.GetComponent<ScreenFlash>();
                if (instance == null) instance = cam.gameObject.AddComponent<ScreenFlash>();
            }

            lastPulse = Time.unscaledTime;
            instance.Begin(alpha);
        }

        private void Begin(float alpha)
        {
            if (overlay == null)
            {
                var go = new GameObject("ScreenFlash");
                go.transform.SetParent(transform, false);
                overlay = go.AddComponent<SpriteRenderer>();
                overlay.sprite = SolidSpriteFactory.CreateSquare();
                overlay.sortingOrder = SortingOrder;
            }

            peak = alpha;
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
            float since = Time.unscaledTime - startedAt;
            float a = since <= Duration ? peak : peak * (1f - (since - Duration) / FadeTime);
            if (a <= 0f || cam == null)
            {
                overlay.enabled = false;
                return;
            }

            float height = cam.orthographicSize * 2f + 1f;
            overlay.transform.localPosition = new Vector3(0f, 0f, 1f); // 카메라 앞 (z = -10 + 1)
            overlay.transform.localScale = new Vector3(height * cam.aspect + 1f, height, 1f);
            overlay.color = new Color(1f, 1f, 1f, a);
        }
    }
}
