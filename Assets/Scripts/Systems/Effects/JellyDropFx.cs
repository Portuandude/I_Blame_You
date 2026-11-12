using UnityEngine;

namespace IBlameYou.Systems
{
    // 젤리 방울 (VfxPlayer.JellyDrops): 젤리 방석으로 버틴 발판이 무너질 때 분홍 젤리 방울이 위로 톡 튀었다가
    // 떨어지며 옅어진다. 그림 파일 없이 작은 동그라미(가운데 하이라이트)를 한 번 만들어 같이 쓴다.
    public class JellyDropFx : MonoBehaviour
    {
        private const float Lifetime = 0.7f;
        private const float Gravity = 14f;        // 유닛/초²
        private const float FadeStart = 0.5f;     // 이 비율부터 옅어진다
        private const int TexSize = 12;           // 그림 크기 (px), ppu 100 → 0.12유닛 (아래 Scale로 키움)

        private static readonly Color Pink = new Color(1f, 0.55f, 0.8f);      // 그림에 직접 칠한다 (색을 곱하면 하이라이트가 흐려져서)
        private static readonly Color PinkRim = new Color(0.85f, 0.3f, 0.6f);
        private static readonly Color Highlight = new Color(1f, 0.92f, 0.97f);
        private static Sprite dropSprite;

        private Vector3 velocity;
        private float age;
        private float baseScale;
        private SpriteRenderer sr;

        // position에서 방울 count개가 위쪽 부채꼴로 튄다.
        public static void Burst(Vector3 position, int count)
        {
            var sprite = DropSprite();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("JellyDrop");
                go.transform.position = position;
                var fx = go.AddComponent<JellyDropFx>();
                float angle = Random.Range(55f, 125f) * Mathf.Deg2Rad; // 위로, 좌우로 조금씩
                float speed = Random.Range(3f, 5f);
                fx.velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
                fx.baseScale = Random.Range(1.3f, 2f);
                go.transform.localScale = new Vector3(fx.baseScale, fx.baseScale, 1f);
                fx.sr = go.AddComponent<SpriteRenderer>();
                fx.sr.sprite = sprite;
                fx.sr.sortingOrder = 1; // 발판(0) 위, 캐릭터와 비슷한 높이
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }
            velocity.y -= Gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;

            // 떨어지는 속도만큼 세로로 늘어나 말랑해 보이게 (가로는 반대로)
            float stretch = 1f + Mathf.Clamp(Mathf.Abs(velocity.y) * 0.04f, 0f, 0.3f);
            transform.localScale = new Vector3(baseScale / stretch, baseScale * stretch, 1f);

            float t = age / Lifetime;
            var c = sr.color;
            c.a = t < FadeStart ? 1f : 1f - (t - FadeStart) / (1f - FadeStart);
            sr.color = c;
        }

        private static Sprite DropSprite()
        {
            if (dropSprite != null) return dropSprite;
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = TexSize / 2f;
            for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color col = Color.clear;
                if (d <= r - 0.5f)
                {
                    col = d > r - 1.8f ? PinkRim : Pink; // 테두리는 진분홍
                    // 왼쪽 위 하이라이트
                    float hx = x + 0.5f - r * 0.65f, hy = y + 0.5f - r * 1.35f;
                    if (hx * hx + hy * hy < 2.5f) col = Highlight;
                }
                tex.SetPixel(x, y, col);
            }
            tex.Apply();
            dropSprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f);
            return dropSprite;
        }
    }
}
