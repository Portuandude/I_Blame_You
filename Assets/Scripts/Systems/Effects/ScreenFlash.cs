using UnityEngine;

namespace IBlameYou.Systems
{
    // 마법이 적에게 맞는 순간 화면을 아주 살짝 하얗게 번쩍인다 (Docs/ArtStyle.md: 알파 0.08, 0.05초).
    // 여러 발이 한꺼번에 맞아도 눈이 아프지 않게 MinInterval 안의 번쩍임은 무시한다.
    // 방을 옮길 때는 RoomFade로 화면을 잠깐 어둡게 덮었다가 밝힌다 (Docs/LevelDesign.md 방 전환 연출).
    // 시작 화면에서 출발할 때는 StartFade로 어둡게 덮고 0.5초 동안 밝힌다 (월드를 만드는 긴 프레임이 지난 다음 프레임부터 잰다).
    // EdgeWarn: 화면 가장자리만 색으로 두 번 깜빡인다 (정예 등장 경고 — 가운데 싸움은 가리지 않게).
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
        private const float StartFadeHold = 0.12f; // 출발: 첫 화면이 잠깐 어둡게 머문 뒤
        private const float StartFadeTime = 0.5f;  // 천천히 밝아진다
        private const int SortingOrder = 900; // 월드 그림 전부보다 앞 (IMGUI HUD는 그 위에 따로 그려진다)

        private static ScreenFlash instance;
        private static float lastPulse = -10f;

        private SpriteRenderer overlay;
        private SpriteRenderer edge;
        private float edgeStartedAt = -10f;
        private float edgeDuration;
        private Color edgeColor;
        private const float EdgeAlpha = 0.7f;
        private const int EdgeTextureSize = 64;
        private static Sprite edgeSprite;
        private float startedAt = -10f;
        private float peak;
        private Color color = Color.white;
        private float hold = Duration;
        private float fade = FadeTime;
        private bool restartClock; // StartFade: 월드를 만드느라 길어진 프레임들을 빼고 시간을 잰다
        private int restartUntilFrame; // 이 프레임까지는 시간을 계속 처음으로 되돌린다 (만든 프레임 + 새 오브젝트 Start가 도는 다음 프레임)

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

        // 시작 화면에서 런을 시작하는 순간 부른다: 완전히 어둡게 덮고, 다음 프레임부터 0.12초 머문 뒤 0.5초 동안 밝아진다.
        public static void StartFade()
        {
            var flash = Find();
            if (flash == null) return;
            flash.Begin(RoomFadeColor, 1f, StartFadeHold, StartFadeTime);
            flash.restartClock = true;
            flash.restartUntilFrame = Time.frameCount + 2;
        }

        // 화면 가장자리를 color로 duration초 동안 두 번 깜빡인다 (가운데는 투명한 테두리 그림).
        // onlyIfIdle: 다른 가장자리 깜빡임(정예·보스 경고)이 아직 보이는 중이면 덮어쓰지 않는다 (레벨업 금빛 같은 축하용).
        public static void EdgeWarn(Color color, float duration, bool onlyIfIdle = false)
        {
            var flash = Find();
            if (flash == null) return;
            if (onlyIfIdle && flash.edge != null && flash.edge.enabled
                && Time.unscaledTime - flash.edgeStartedAt < flash.edgeDuration) return;
            flash.edgeColor = color;
            flash.edgeDuration = Mathf.Max(0.05f, duration);
            flash.edgeStartedAt = Time.unscaledTime;
            if (flash.edge == null)
            {
                var go = new GameObject("ScreenEdgeWarn");
                go.transform.SetParent(flash.transform, false);
                flash.edge = go.AddComponent<SpriteRenderer>();
                flash.edge.sprite = EdgeSprite();
                flash.edge.sortingOrder = SortingOrder - 1; // 방 전환 덮개 아래
            }
            flash.edge.enabled = true;
            flash.UpdateEdge();
        }

        // 가장자리에서 진하고 안쪽 30% 너머는 투명한 테두리 (한 번 만들어 둔다). 1유닛 크기.
        private static Sprite EdgeSprite()
        {
            if (edgeSprite != null) return edgeSprite;
            int n = EdgeTextureSize;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Min(x, n - 1 - x) / (float)(n - 1);
                    float dy = Mathf.Min(y, n - 1 - y) / (float)(n - 1);
                    float d = Mathf.Min(dx, dy); // 가장자리 0 ~ 가운데 0.5
                    float a = Mathf.Clamp01(1f - d / 0.15f);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            edgeSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return edgeSprite;
        }

        private void UpdateEdge()
        {
            var cam = GetComponent<Camera>();
            float t = (Time.unscaledTime - edgeStartedAt) / edgeDuration;
            if (t >= 1f || cam == null)
            {
                edge.enabled = false;
                return;
            }

            float blink = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)); // 두 번 깜빡임
            float height = cam.orthographicSize * 2f;
            edge.transform.localPosition = new Vector3(0f, 0f, 1f);
            edge.transform.localScale = new Vector3(height * cam.aspect, height, 1f);
            edge.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, EdgeAlpha * blink);
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

            restartClock = false; // 다른 덮개가 덮어쓰면 그 덮개의 시간대로
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
            if (edge != null && edge.enabled) UpdateEdge();
            if (overlay == null || !overlay.enabled) return;
            if (restartClock)
            {
                startedAt = Time.unscaledTime; // 긴 프레임 동안은 완전히 어두운 채로
                if (Time.frameCount >= restartUntilFrame) restartClock = false;
            }
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
