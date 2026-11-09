using UnityEngine;

namespace IBlameYou.Systems
{
    // 잔상 (Docs/ArtStyle.md "잔상"): 지금 그림을 그 자리에 반투명 복사본으로 남기고 짧게 사라지게 한다.
    // 대쉬처럼 짧은 간격으로 여러 번 부르면 앞선 복사본일수록 더 옅어져 60% → 30% → 10% 꼬리가 된다.
    public class Afterimage : MonoBehaviour
    {
        public const float DefaultAlpha = 0.6f;
        public const float DefaultLife = 0.16f;
        public static readonly Color Aqua = new Color(0.5f, 0.97f, 1f); // 팔레트 aqua (마법 테두리)

        private SpriteRenderer ghost;
        private Color baseColor;
        private float startAlpha;
        private float life;
        private float age;

        // source의 지금 그림·방향·크기 그대로 복사본을 만든다. 그림이 없으면 아무것도 하지 않는다.
        public static void Spawn(SpriteRenderer source, Color tint, float alpha = DefaultAlpha, float lifeTime = DefaultLife)
        {
            if (source == null || source.sprite == null || !source.enabled) return;

            var go = new GameObject("Afterimage");
            var t = source.transform;
            go.transform.SetPositionAndRotation(t.position, t.rotation);
            go.transform.localScale = t.lossyScale; // 부모(플레이어)의 좌우 뒤집기까지 포함

            var image = go.AddComponent<Afterimage>();
            image.ghost = go.AddComponent<SpriteRenderer>();
            image.ghost.sprite = source.sprite;
            image.ghost.flipX = source.flipX;
            image.ghost.flipY = source.flipY;
            image.ghost.sortingLayerID = source.sortingLayerID;
            image.ghost.sortingOrder = source.sortingOrder - 1; // 본체 바로 뒤
            image.baseColor = tint;
            image.startAlpha = alpha;
            image.life = Mathf.Max(0.01f, lifeTime);
            image.Apply(alpha);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = 1f - age / life;
            if (k <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Apply(startAlpha * k);
        }

        private void Apply(float alpha)
        {
            ghost.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }
}
