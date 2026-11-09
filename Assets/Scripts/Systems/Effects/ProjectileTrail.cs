using UnityEngine;

namespace IBlameYou.Systems
{
    // 마법 투사체 뒤로 속성별 작은 입자를 흘려 비행감을 준다.
    // 불: 위로 날리는 불씨 / 물: 아래로 떨어지는 물방울 / 바람: 짧게 흩어지는 바람 조각 / 무: 반짝이는 마력 가루.
    // 비행 그림("Visual" 자식)이 있으면 GhostInterval마다 속성색 잔상도 남긴다 (Docs/ArtStyle.md "잔상": 꼬리 2~3개).
    public class ProjectileTrail : MonoBehaviour
    {
        private struct Settings
        {
            public float Interval;
            public float Size;
            public float Life;
            public Vector2 Drift;   // 생성 시 속도 (월드 기준)
            public float Spread;    // 속도 무작위 폭
            public float Gravity;   // 아래로 끌어당기는 가속도 (음수면 위로)
            public Color Start;
            public Color End;
        }

        private const float GhostInterval = 0.04f;
        private const float GhostAlpha = 0.5f;
        private const float GhostLife = 0.12f;

        private Settings settings;
        private float timer;
        private Vector3 lastPosition;
        private SpriteRenderer visual;
        private Color ghostColor;
        private float ghostTimer;

        public void Begin(Element element)
        {
            settings = For(element);
            lastPosition = transform.position;
            var child = transform.Find("Visual");
            visual = child != null ? child.GetComponent<SpriteRenderer>() : null;
            ghostColor = Color.Lerp(ElementInfo.Color(element), Color.white, 0.6f); // 비행 그림이 이미 속성색이라 밝게 해서 곱한다
            ghostTimer = GhostInterval;
        }

        private static Settings For(Element element)
        {
            switch (element)
            {
                case Element.Fire:
                    return new Settings { Interval = 0.02f, Size = 0.2f, Life = 0.35f, Drift = new Vector2(0f, 0.8f), Spread = 0.6f,
                        Gravity = -1.5f, Start = new Color(1f, 0.75f, 0.25f, 0.95f), End = new Color(0.6f, 0.15f, 0.05f, 0f) };
                case Element.Water:
                    return new Settings { Interval = 0.035f, Size = 0.13f, Life = 0.45f, Drift = new Vector2(0f, 0.3f), Spread = 0.5f,
                        Gravity = 7f, Start = new Color(0.6f, 0.85f, 1f, 0.9f), End = new Color(0.2f, 0.5f, 1f, 0f) };
                case Element.Wind:
                    return new Settings { Interval = 0.02f, Size = 0.1f, Life = 0.22f, Drift = Vector2.zero, Spread = 1.2f,
                        Gravity = 0f, Start = new Color(0.9f, 1f, 0.92f, 0.8f), End = new Color(0.6f, 1f, 0.75f, 0f) };
                default:
                    return new Settings { Interval = 0.03f, Size = 0.12f, Life = 0.4f, Drift = Vector2.zero, Spread = 0.5f,
                        Gravity = 0f, Start = new Color(0.95f, 0.85f, 1f, 0.95f), End = new Color(0.55f, 0.3f, 0.95f, 0f) };
            }
        }

        private void Update()
        {
            if (settings.Interval <= 0f) return;

            timer -= Time.deltaTime;
            while (timer <= 0f)
            {
                timer += settings.Interval;
                // 프레임 사이에 이동한 구간에 고르게 뿌려 빠른 투사체도 끊기지 않게 한다.
                var position = Vector3.Lerp(lastPosition, transform.position, Random.value);
                Spawn(position);
            }

            lastPosition = transform.position;

            if (visual != null)
            {
                ghostTimer -= Time.deltaTime;
                if (ghostTimer <= 0f)
                {
                    ghostTimer += GhostInterval;
                    Afterimage.Spawn(visual, ghostColor, GhostAlpha, GhostLife);
                }
            }
        }

        private void Spawn(Vector3 position)
        {
            var go = new GameObject("TrailParticle");
            go.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.08f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateCircle();
            renderer.sortingOrder = 4;
            var velocity = settings.Drift + Random.insideUnitCircle * settings.Spread;
            go.AddComponent<TrailParticle>().Begin(renderer, velocity, settings.Gravity, settings.Size, settings.Life,
                settings.Start, settings.End);
        }
    }

    // 짧게 움직이며 작아지고 투명해지다 사라지는 입자.
    public class TrailParticle : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Vector2 velocity;
        private float gravity;
        private float size;
        private float life;
        private float age;
        private Color start;
        private Color end;

        public void Begin(SpriteRenderer renderer, Vector2 initialVelocity, float gravityAcceleration, float startSize, float lifetime,
            Color startColor, Color endColor)
        {
            spriteRenderer = renderer;
            velocity = initialVelocity;
            gravity = gravityAcceleration;
            size = startSize;
            life = lifetime;
            start = startColor;
            end = endColor;
            Apply(0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            velocity.y -= gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            Apply(t);
        }

        private void Apply(float t)
        {
            float scale = size * (1f - t * 0.7f);
            transform.localScale = new Vector3(scale, scale, 1f);
            spriteRenderer.color = Color.Lerp(start, end, t);
        }
    }
}
