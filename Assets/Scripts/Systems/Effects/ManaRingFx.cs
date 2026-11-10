using UnityEngine;

namespace IBlameYou.Systems
{
    // 마나 고리 (VfxPlayer.ManaRing): 플레이어 둘레에 색 빛 고리가 작게 나타나 퍼지며 옅어진다 (0.4초).
    // 쌍둥이 반지(A 방어막 → A색)·응원 리본(B 마법 → B색) 마나 회복 표시. 그림 파일 없이 런타임에 고리 텍스처를 한 번 만든다.
    public class ManaRingFx : MonoBehaviour
    {
        private const float Lifetime = 0.4f;
        private const float StartScale = 0.35f;
        private const float EndScale = 1.5f;   // 스프라이트 크기 (텍스처 64px, ppu 64). 보이는 고리 지름은 0.78배 ≈ 1.2유닛 (리뷰 LOW)
        private const int TextureSize = 64;

        private static Sprite ringSprite;

        private SpriteRenderer ring;
        private Color baseColor;
        private float age;

        public static void Create(Transform follow, Vector3 localOffset, Color color)
        {
            if (follow == null) return;
            var go = new GameObject("ManaRing");
            go.transform.SetParent(follow, false);
            go.transform.localPosition = localOffset;
            go.transform.localScale = Vector3.one * StartScale;
            var fx = go.AddComponent<ManaRingFx>();
            fx.ring = go.AddComponent<SpriteRenderer>();
            fx.ring.sprite = RingSprite();
            fx.ring.sortingOrder = 7;
            fx.baseColor = color;
            fx.ring.color = color;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            float eased = 1f - (1f - t) * (1f - t); // 처음엔 빠르게 퍼지고 끝에서 느려진다
            float scale = Mathf.Lerp(StartScale, EndScale, eased);
            // 부모(플레이어)가 좌우로 뒤집혀도 고리는 대칭이라 상관없다. 부모 크기만 상쇄.
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)), scale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1f);
            var c = baseColor;
            c.a = baseColor.a * (1f - t);
            ring.color = c;
            if (t >= 1f) Destroy(gameObject);
        }

        // 부드러운 가장자리를 가진 흰 고리 (색은 SpriteRenderer.color로 입힌다).
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float half = TextureSize / 2f;
            const float radius = 0.78f, thickness = 0.16f; // 반지름 비율, 두께 비율
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius) / thickness;
                float a = Mathf.Clamp01(1f - d);
                a *= a; // 가운데 진하고 가장자리 부드럽게
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
            return ringSprite;
        }
    }
}
