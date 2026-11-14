using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 통통 버섯 튕김 꼬리 (2026-11-14): 튕겨 오르는 동안 분홍 잔상을 남기고,
    // 플레이어는 가장 높은 곳(올라가는 속도가 거의 0)에서 포자가 반짝 터진다.
    // 튕긴 몸에 한 번 붙여 두고 튕길 때마다 Play를 부른다. 대시(aqua)·순간이동(보라) 잔상과 색으로 구분된다.
    public class BounceTrailFx : MonoBehaviour
    {
        private const float Interval = 0.06f;
        private const float MaxTime = 1.2f;       // 천장·순간이동으로 최고점 판정이 안 나도 이만큼 뒤엔 멈춘다
        private const float ApexSpeed = 0.3f;
        private const float GhostAlpha = 0.5f;
        private const float GhostLife = 0.2f;
        private static readonly Color Pink = new Color(1f, 0.6f, 0.82f);
        private static readonly Color Spore = new Color(1f, 0.75f, 0.88f, 0.9f);

        private Rigidbody2D body;
        private SpriteRenderer visual;
        private float age = MaxTime;
        private float nextSpawn;
        private bool rising;
        private bool apexBurst;

        // target: 튕긴 몸 (Rigidbody2D가 있는 오브젝트). apex: 최고점 포자 반짝 (플레이어만).
        public static void Play(GameObject target, bool apex)
        {
            if (target == null) return;
            var fx = target.GetComponent<BounceTrailFx>();
            if (fx == null) fx = target.AddComponent<BounceTrailFx>();
            fx.Begin(apex);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            var child = transform.Find("Visual");
            visual = child != null ? child.GetComponent<SpriteRenderer>() : null;
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        }

        private void Begin(bool apex)
        {
            age = 0f;
            nextSpawn = 0f;
            rising = true;
            apexBurst = apex;
        }

        private void LateUpdate()
        {
            if (!rising) return;
            if (body == null)
            {
                rising = false;
                return;
            }
            age += Time.deltaTime;
            if (body.linearVelocity.y <= ApexSpeed)
            {
                rising = false;
                var health = GetComponent<HealthSystem>();
                if (apexBurst && (health == null || !health.IsDead)) Apex(); // 오르다 쓰러지면 반짝이지 않는다
                return;
            }
            if (age >= MaxTime)
            {
                rising = false;
                return;
            }
            if (age >= nextSpawn)
            {
                Afterimage.Spawn(visual, Pink, GhostAlpha, GhostLife);
                nextSpawn = age + Interval;
            }
        }

        // 최고점: 머리 위에서 반짝 + 포자 다섯 개가 둥글게 흩어진다 (화면 밖이면 생략)
        private void Apex()
        {
            Vector3 top = visual != null && visual.sprite != null
                ? new Vector3(visual.bounds.center.x, visual.bounds.max.y, 0f)
                : transform.position;
            if (!SlimeSplitFx.OnScreen(top)) return;
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Sparkle");
            if (frames != null) EffectLibrary.PlayOneShot(top, frames, 16f, Color.clear, 0f, false, 0.7f, sortingOrder: 7);
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                var v = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) + 0.6f, 0f) * Random.Range(1.4f, 2.2f);
                SlimeSplitFx.Drop(top, v, i % 2 == 0 ? Color.white : Spore, Random.Range(0.05f, 0.08f));
            }
        }
    }

    // 버섯 갓에서 가끔 피어오르는 포자 한 알: 좌우로 살랑이며 천천히 떠올라 사라진다 (중력 없음).
    public class MushroomSpore : MonoBehaviour
    {
        private const float Life = 1.8f;
        private const float RiseSpeed = 0.55f;

        private SpriteRenderer sprite;
        private Color baseColor;
        private Vector3 origin;
        private float phase;
        private float age;

        public static void Float(Vector3 position, Color color, float size)
        {
            var go = new GameObject("MushroomSpore");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var spore = go.AddComponent<MushroomSpore>();
            spore.sprite = go.AddComponent<SpriteRenderer>();
            spore.sprite.sprite = SolidSpriteFactory.CreateCircle();
            spore.sprite.sortingOrder = 5;
            spore.baseColor = color;
            spore.origin = position;
            spore.phase = Random.Range(0f, Mathf.PI * 2f);
            spore.Apply(0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            float sway = Mathf.Sin(age * 3f + phase) * 0.18f;
            transform.position = origin + new Vector3(sway, age * RiseSpeed, 0f);
            Apply(age / Life);
        }

        // 처음 0.25초에 나타나고 끝 40%에서 옅어진다
        private void Apply(float t)
        {
            float alpha = t < 0.14f ? t / 0.14f : (t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f);
            sprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(alpha));
        }
    }
}
