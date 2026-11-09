using UnityEngine;

namespace IBlameYou.Systems
{
    // 피격 지점에서 사방으로 튀었다가 떨어지며 사라지는 작은 파편.
    public class HitDebris : MonoBehaviour
    {
        private const float Lifetime = 0.35f;
        private const float Gravity = 18f;
        private const float Size = 0.09f;

        private static readonly Color DebrisColor = new Color(1f, 0.95f, 0.85f);

        private Vector2 velocity;
        private float age;
        private SpriteRenderer spriteRenderer;

        public static void Burst(Vector3 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("HitDebris");
                go.transform.position = position;
                go.transform.localScale = new Vector3(Size, Size, 1f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = SolidSpriteFactory.CreateSquare();
                renderer.color = DebrisColor;
                renderer.sortingOrder = 6;

                var debris = go.AddComponent<HitDebris>();
                debris.spriteRenderer = renderer;
                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad; // 주로 위쪽으로
                float speed = Random.Range(3f, 6f);
                debris.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            velocity.y -= Gravity * dt;
            transform.position += (Vector3)(velocity * dt);
            var color = spriteRenderer.color;
            spriteRenderer.color = new Color(color.r, color.g, color.b, 1f - age / Lifetime);
        }
    }
}
