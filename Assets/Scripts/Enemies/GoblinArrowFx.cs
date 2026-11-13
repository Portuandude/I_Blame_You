using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 고블린 궁수 화살 연출 (그림·소리만, 화살 속도·사거리·피해·간격은 GoblinArcherController 그대로, 2026-11-13).
    // - Released: 시위를 놓을 때 "팅" (BowTwang)
    // - 날아가는 동안 옅은 흰 꼬리 (TrailRenderer)
    // - 스침: 플레이어 곁(1.3칸 안)을 맞히지 않고 지나가면 "슈웅" (ArrowWhiz) 한 번
    // - Stick: 벽·바닥에 닿으면 그 자리에 박혀 부르르 떨다가 1.5초 뒤 옅어진다 + "턱" (ArrowThunk)
    // - Splinter: 플레이어에게 맞으면 나무 조각 4개가 튄다 (아픈 소리는 플레이어 쪽에서 이미 난다)
    public class GoblinArrowFx : MonoBehaviour
    {
        private static readonly Color TrailColor = new Color(1f, 0.97f, 0.85f, 0.45f);
        private static readonly Color SplinterColor = new Color(0.72f, 0.52f, 0.3f, 0.95f);
        private static Material trailMaterial;

        private const float WhizRange = 1.3f;
        private const float StuckLife = 1.5f;
        private const float StuckFade = 0.5f;
        private const float WobbleTime = 0.35f;

        private enum Kind { Flying, Stuck }

        private Kind kind;
        // Flying
        private Transform target;
        private float closest = float.MaxValue;
        private bool whizzed;
        // Stuck
        private SpriteRenderer sprite;
        private Color baseColor;
        private float baseAngle;
        private float age;

        public static void Released(Vector3 origin)
        {
            if (SlimeSplitFx.OnScreen(origin)) GameAudio.PlaySfx("BowTwang", 0.6f);
        }

        // 날아가는 화살에 꼬리와 스침 소리를 붙인다. target: 이 화살이 노린 플레이어 (없으면 스침 소리 없음).
        public static void Attach(GameObject arrow, Transform target)
        {
            var fx = arrow.AddComponent<GoblinArrowFx>();
            fx.kind = Kind.Flying;
            fx.target = target;

            if (trailMaterial == null) trailMaterial = new Material(Shader.Find("Sprites/Default"));
            var trail = arrow.AddComponent<TrailRenderer>();
            trail.material = trailMaterial;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.07f, 1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(TrailColor, 0f), new GradientColorKey(TrailColor, 1f) },
                new[] { new GradientAlphaKey(TrailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.sortingOrder = 4; // 화살 그림(5) 바로 뒤
        }

        // 벽·바닥에 박힌 화살. point: 부딪힌 자리, direction: 날아가던 방향, look: 날아가던 화살 그림 (모양·크기를 그대로 쓴다).
        public static void Stick(Vector2 point, Vector2 direction, SpriteRenderer look)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            if (SlimeSplitFx.OnScreen(point)) GameAudio.PlaySfx("ArrowThunk", 0.55f);

            var go = new GameObject("StuckArrow");
            go.transform.position = point - direction * 0.12f; // 끝이 살짝 박히게
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var fx = go.AddComponent<GoblinArrowFx>();
            fx.kind = Kind.Stuck;
            fx.baseAngle = angle;
            fx.sprite = go.AddComponent<SpriteRenderer>();
            if (look != null && look.sprite != null)
            {
                fx.sprite.sprite = look.sprite;
                fx.sprite.flipX = look.flipX;
                fx.sprite.flipY = look.flipY;
                fx.baseColor = look.color;
                go.transform.localScale = look.transform.localScale;
            }
            else
            {
                fx.sprite.sprite = SolidSpriteFactory.CreateCircle();
                fx.baseColor = new Color(0.9f, 0.8f, 0.5f);
                go.transform.localScale = new Vector3(0.5f, 0.12f, 1f);
            }

            fx.sprite.color = fx.baseColor;
            fx.sprite.sortingOrder = 5;

            // 부딪힌 자리 작은 먼지 조각
            for (int i = 0; i < 2; i++)
            {
                var v = new Vector3(-direction.x * Random.Range(1f, 2f) + Random.Range(-1f, 1f), Random.Range(1f, 2.5f), 0f);
                SlimeSplitFx.Drop(point, v, new Color(0.75f, 0.7f, 0.6f, 0.9f), Random.Range(0.07f, 0.11f));
            }
        }

        // 화살이 플레이어에게 맞았을 때 나무 조각.
        public static void Splinter(Vector3 position, Vector2 direction)
        {
            float side = direction.x >= 0f ? 1f : -1f;
            for (int i = 0; i < 4; i++)
            {
                var v = new Vector3(side * Random.Range(1.5f, 3.5f), Random.Range(1.5f, 4f), 0f);
                if (i % 2 == 1) v.x = -v.x * 0.5f; // 반은 뒤로 튄다
                SlimeSplitFx.Drop(position, v, SplinterColor, Random.Range(0.08f, 0.13f));
            }
        }

        private void Update()
        {
            if (kind == Kind.Flying) UpdateWhiz();
            else UpdateStuck();
        }

        private void UpdateWhiz()
        {
            if (whizzed || target == null) return;
            float distance = Vector2.Distance(transform.position, (Vector2)target.position + new Vector2(0f, 0.5f));
            if (distance < closest)
            {
                closest = distance;
                return;
            }

            // 가장 가까웠던 순간이 지나고 멀어지기 시작했다 = 맞히지 않고 스쳐 지나갔다 (맞으면 화살이 이미 없어졌다)
            if (closest <= WhizRange && distance > closest + 0.05f)
            {
                whizzed = true;
                GameAudio.PlaySfx("ArrowWhiz", 0.5f);
            }
        }

        private void UpdateStuck()
        {
            age += Time.deltaTime;
            if (age >= StuckLife)
            {
                Destroy(gameObject);
                return;
            }

            // 박힌 직후 부르르 (점점 잦아든다)
            float wobble = age < WobbleTime ? Mathf.Sin(age * 70f) * 7f * (1f - age / WobbleTime) : 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + wobble);

            var c = baseColor;
            c.a *= Mathf.Clamp01((StuckLife - age) / StuckFade);
            sprite.color = c;
        }
    }
}
