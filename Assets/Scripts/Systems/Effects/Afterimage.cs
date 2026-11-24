using System.Collections.Generic;
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

        // 다 사라진 복사본을 버리지 않고 꺼 두었다가 다시 쓴다 (대쉬·튕김마다 GameObject를 새로 만들지 않게).
        // 판을 다시 시작해 지워진 것은 꺼낼 때 건너뛴다.
        private const int PoolLimit = 32;
        private static readonly Stack<Afterimage> Pool = new Stack<Afterimage>();

        // source의 지금 그림·방향·크기 그대로 복사본을 만든다. 그림이 없으면 아무것도 하지 않는다.
        public static void Spawn(SpriteRenderer source, Color tint, float alpha = DefaultAlpha, float lifeTime = DefaultLife)
        {
            if (source == null || source.sprite == null || !source.enabled) return;

            var image = Take();
            var go = image.gameObject;
            var t = source.transform;
            go.transform.SetPositionAndRotation(t.position, t.rotation);
            go.transform.localScale = t.lossyScale; // 부모(플레이어)의 좌우 뒤집기까지 포함

            image.ghost.sprite = source.sprite;
            image.ghost.flipX = source.flipX;
            image.ghost.flipY = source.flipY;
            image.ghost.sortingLayerID = source.sortingLayerID;
            image.ghost.sortingOrder = source.sortingOrder - 1; // 본체 바로 뒤
            image.baseColor = tint;
            image.startAlpha = alpha;
            image.life = Mathf.Max(0.01f, lifeTime);
            image.age = 0f;
            image.Apply(alpha);
            go.SetActive(true);
        }

        private static Afterimage Take()
        {
            while (Pool.Count > 0)
            {
                var pooled = Pool.Pop();
                if (pooled != null && pooled.ghost != null) return pooled;
            }
            var go = new GameObject("Afterimage");
            var image = go.AddComponent<Afterimage>();
            image.ghost = go.AddComponent<SpriteRenderer>();
            return image;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = 1f - age / life;
            if (k <= 0f)
            {
                // 판 정리로 파괴된 항목이 풀 자리를 차지하고 있으면 걷어 낸다 (새 판 초반 잔상이 풀로 못 돌아가던 것, 버그 점검 2026-11-24)
                if (Pool.Count >= PoolLimit && Pool.Peek() == null) PurgeDestroyed();
                if (Pool.Count < PoolLimit)
                {
                    gameObject.SetActive(false);
                    Pool.Push(this);
                }
                else Destroy(gameObject);
                return;
            }

            Apply(startAlpha * k);
        }

        private static void PurgeDestroyed()
        {
            var alive = new System.Collections.Generic.List<Afterimage>(Pool.Count);
            while (Pool.Count > 0)
            {
                var item = Pool.Pop();
                if (item != null) alive.Add(item);
            }
            for (int i = alive.Count - 1; i >= 0; i--) Pool.Push(alive[i]);
        }

        private void Apply(float alpha)
        {
            ghost.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }
}
