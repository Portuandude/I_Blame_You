using UnityEngine;

namespace IBlameYou.UI
{
    // HP 화면 경고등 (기획서 UI/UX "HP (화면 경고등)").
    // 체력이 30% 이하이면 화면 가장자리가 붉게 맥동한다. 체력이 낮을수록 진하고 빠르게 뛴다.
    // 쓰러진 뒤와 스테이지 클리어 화면에서는 끈다.
    public partial class SkillHUD
    {
        public const float LowHealthRatio = 0.3f;

        private const int VignetteSize = 128;
        private const float VignetteInner = 0.55f;     // 중심에서 이 거리(0~1)까지는 투명
        private const float PulseSlowHz = 1.1f;        // 30%일 때 맥동 속도
        private const float PulseFastHz = 2.4f;        // 0%에 가까울 때 맥동 속도
        private const float MinAlpha = 0.25f;
        private const float MaxAlpha = 0.75f;

        private static Texture2D vignette;
        private float pulsePhase;

        private static Texture2D Vignette
        {
            get
            {
                if (vignette != null) return vignette;

                vignette = new Texture2D(VignetteSize, VignetteSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var pixels = new Color[VignetteSize * VignetteSize];
                float half = (VignetteSize - 1) / 2f;
                for (int y = 0; y < VignetteSize; y++)
                {
                    for (int x = 0; x < VignetteSize; x++)
                    {
                        // 화면 비율과 상관없이 네 변 모두 붉어지도록 가로/세로 중 가장자리에 가까운 쪽 기준으로 계산한다.
                        float dx = Mathf.Abs(x - half) / half;
                        float dy = Mathf.Abs(y - half) / half;
                        float edge = Mathf.Max(dx, dy);
                        float t = Mathf.Clamp01((edge - VignetteInner) / (1f - VignetteInner));
                        pixels[y * VignetteSize + x] = new Color(0.85f, 0.05f, 0.05f, t * t);
                    }
                }

                vignette.SetPixels(pixels);
                vignette.Apply();
                return vignette;
            }
        }

        // 0 = 경고 없음, 1 = 체력 0에 가까움.
        private float LowHealthSeverity()
        {
            if (health == null || health.IsDead || deathHandled || stageCleared || health.Max <= 0f) return 0f;
            float ratio = health.Current / health.Max;
            if (ratio > LowHealthRatio) return 0f;
            return 1f - ratio / LowHealthRatio;
        }

        private void DrawLowHealthWarning()
        {
            float severity = LowHealthSeverity();
            if (severity <= 0f)
            {
                pulsePhase = 0f;
                return;
            }

            // 일시정지나 특성 창(timeScale 0) 중에도 깜빡임이 멈춰 보이지 않게 실제 시간으로 센다.
            if (Event.current.type == EventType.Repaint)
                pulsePhase = (pulsePhase + Time.unscaledDeltaTime * Mathf.Lerp(PulseSlowHz, PulseFastHz, severity)) % 1f;

            float pulse = 0.5f + 0.5f * Mathf.Sin(pulsePhase * 2f * Mathf.PI);
            float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, severity) * Mathf.Lerp(0.45f, 1f, pulse);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Vignette, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }
    }
}
