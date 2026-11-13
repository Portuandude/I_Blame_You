using UnityEngine;

namespace IBlameYou.Systems
{
    // 적 착지 예고 (슬라임 도약): 떨어질 땅 위에 옅은 붉은 타원을 깐다. 주인이 Place로 자리·진하기를 매 프레임 알려 주고,
    // 착지하거나 공격이 끊기면 Remove. 주인이 사라지면 스스로 없어진다. 그림 파일 없이 런타임에 타원 텍스처를 한 번 만든다.
    public class LandingMarkFx : MonoBehaviour
    {
        private const int TextureWidth = 64;
        private const int TextureHeight = 16; // ppu 64 → 1 × 0.25 유닛. 크기는 localScale(가로 폭)로 맞춘다
        private const float MinAlpha = 0.15f;
        private const float MaxAlpha = 0.5f;
        private const float GroundLift = 0.03f;
        private static readonly Color MarkColor = new Color(1f, 0.35f, 0.3f);

        private static Sprite markSprite;

        private SpriteRenderer mark;
        private Color color = MarkColor;
        private GameObject owner;
        private float strength;
        private bool visible;

        // color: 없으면 붉은색 (슬라임 킹 소환 자리는 보라).
        public static LandingMarkFx Create(GameObject owner, float width, Color? color = null)
        {
            var go = new GameObject("LandingMark");
            go.transform.localScale = new Vector3(width, width, 1f);
            var fx = go.AddComponent<LandingMarkFx>();
            fx.owner = owner;
            if (color.HasValue) fx.color = color.Value;
            fx.mark = go.AddComponent<SpriteRenderer>();
            fx.mark.sprite = MarkSprite();
            fx.mark.sortingOrder = 2; // 땅 타일 위, 적·플레이어 아래 (화살 발사대 점선과 같은 층)
            fx.mark.color = Color.clear;
            return fx;
        }

        // ground: 땅 표면 점. strength 0~1: 예고가 진행될수록 진해진다. hasGround=false면 (아래가 낭떠러지) 숨긴다.
        public void Place(Vector2 ground, float strength01, bool hasGround)
        {
            visible = hasGround;
            strength = Mathf.Clamp01(strength01);
            transform.position = new Vector3(ground.x, ground.y + GroundLift, 0f);
        }

        public void Remove()
        {
            if (this != null) Destroy(gameObject);
        }

        private void Update()
        {
            if (owner == null || !owner.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 22f); // 살짝 두근거림
            float alpha = visible ? Mathf.Lerp(MinAlpha, MaxAlpha, strength) * pulse : 0f;
            mark.color = new Color(color.r, color.g, color.b, alpha);
        }

        // 가운데가 옅고 테두리가 진한 흰 타원 (색은 SpriteRenderer.color로 입힌다).
        private static Sprite MarkSprite()
        {
            if (markSprite != null) return markSprite;
            var tex = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float hw = TextureWidth / 2f, hh = TextureHeight / 2f;
            var pixels = new Color32[TextureWidth * TextureHeight];
            for (int y = 0; y < TextureHeight; y++)
            for (int x = 0; x < TextureWidth; x++)
            {
                float dx = (x + 0.5f - hw) / hw, dy = (y + 0.5f - hh) / hh;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = r >= 1f ? 0f : Mathf.Lerp(0.35f, 1f, r * r); // 안쪽 옅게, 테두리 쪽 진하게
                a *= Mathf.Clamp01((1f - r) * 6f); // 바깥 가장자리 부드럽게
                pixels[y * TextureWidth + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            markSprite = Sprite.Create(tex, new Rect(0, 0, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f), TextureWidth);
            return markSprite;
        }
    }
}
