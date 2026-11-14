using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 보물 고블린 동전 자국 (2026-11-14 ① 컨텐츠): 도망치는 고블린 발밑에 남는 작은 금빛 자국. 반짝이다가 Life초 동안 옅어져 사라진다.
    // 줍는 골드가 아니라 그림만 (어디로 달아났는지 보이게). 땅 그림 위, 캐릭터 뒤에 그린다.
    public class TreasureGoblinTrail : MonoBehaviour
    {
        private const float Life = 2.5f;
        private const float Width = 0.3f;
        private const float Height = 0.13f;
        private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);

        private SpriteRenderer sprite;
        private float bornAt;
        private float phase;

        public static void Drop(Vector3 feet)
        {
            var go = new GameObject("TreasureCoinMark");
            go.transform.position = new Vector3(feet.x + Random.Range(-0.12f, 0.12f), feet.y + 0.06f, 0f);
            go.transform.localScale = new Vector3(Width, Height, 1f);
            var trail = go.AddComponent<TreasureGoblinTrail>();
            trail.sprite = go.AddComponent<SpriteRenderer>();
            trail.sprite.sprite = SolidSpriteFactory.CreateCircle();
            trail.sprite.sortingOrder = -1; // 착지 먼지·소환 타원과 같은 층
            trail.sprite.color = Gold;
            trail.bornAt = Time.time;
            trail.phase = Random.Range(0f, 6.28f);
        }

        private void Update()
        {
            float t = (Time.time - bornAt) / Life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            float twinkle = 0.75f + 0.25f * Mathf.Sin(Time.time * 12f + phase);
            sprite.color = new Color(Gold.r, Gold.g, Gold.b, (1f - t * t) * twinkle);
        }
    }
}
