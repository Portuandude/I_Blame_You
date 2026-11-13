using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 분열 연출 (그림·소리만, 분열 수치·시간은 SlimeKingController.DoSplit·SplitOnDeath 그대로, 2026-11-13).
    // - Burst: 화면 안이면 SlimeSplit 소리("뿌직-쪼옥") + 갈라진 자리에서 보라 젤리 방울이 사방으로 튀어 떨어지며 옅어진다
    // - JellyString: 갈라진 두 몸 사이에 젤리 끈이 이어져 늘어나며 가늘어지다가 "톡" 끊긴다 (끊긴 자리에 작은 방울 둘)
    // 모두 런타임 원 스프라이트(SolidSpriteFactory.CreateCircle)와 선(LineRenderer)이라 그림 파일이 없다. 땅 판정은 없다(그림만).
    public class SlimeSplitFx : MonoBehaviour
    {
        public static readonly Color GooColor = new Color(0.72f, 0.45f, 1f, 0.95f);

        private const float Gravity = 20f;
        private const float GooLife = 0.7f;
        private const float SfxCooldown = 0.1f; // 어미 여럿이 한꺼번에 터져도 소리가 겹쳐 커지지 않게

        private static Material lineMaterial;
        private static float lastSfxTime = -10f;

        private enum Kind { Goo, String }

        private Kind kind;
        private float age;
        private float lifetime;

        // Goo
        private SpriteRenderer sprite;
        private Color baseColor;
        private Vector3 velocity;
        private float baseScale;

        // String
        private LineRenderer line;
        private Transform endA;
        private Transform endB;
        private Vector3 offsetA;
        private Vector3 offsetB;
        private Vector3 lastA;
        private Vector3 lastB;
        private float startWidth;

        public static void Burst(Vector3 center, Color color, int count, float scale, float sfxVolume)
        {
            if (OnScreen(center) && Time.unscaledTime - lastSfxTime >= SfxCooldown)
            {
                lastSfxTime = Time.unscaledTime;
                GameAudio.PlaySfx("SlimeSplit", sfxVolume);
            }

            for (int i = 0; i < count; i++)
            {
                // 위쪽 반원으로 고르게 퍼지되 조금씩 흔든다
                float angle = Mathf.Lerp(20f, 160f, count > 1 ? i / (float)(count - 1) : 0.5f) + Random.Range(-12f, 12f);
                float speed = Random.Range(4f, 7f) * Mathf.Sqrt(scale);
                var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
                Goo(center, dir * speed, color, Random.Range(0.18f, 0.32f) * scale);
            }
        }

        public static void JellyString(Transform a, Vector3 offsetA, Transform b, Vector3 offsetB, Color color, float width, float snapAfter)
        {
            if (a == null || b == null) return;
            var go = new GameObject("SlimeSplitString");
            var fx = go.AddComponent<SlimeSplitFx>();
            fx.kind = Kind.String;
            fx.lifetime = Mathf.Max(0.05f, snapAfter);
            fx.endA = a;
            fx.endB = b;
            fx.offsetA = offsetA;
            fx.offsetB = offsetB;
            fx.lastA = a.position + offsetA;
            fx.lastB = b.position + offsetB;
            fx.startWidth = width;

            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            fx.line = go.AddComponent<LineRenderer>();
            fx.line.positionCount = 2;
            fx.line.useWorldSpace = true;
            fx.line.material = lineMaterial;
            fx.line.startColor = color;
            fx.line.endColor = color;
            fx.line.numCapVertices = 4;
            fx.line.sortingOrder = 0; // 몸(1) 뒤
            fx.UpdateString(0f);
        }

        private static void Goo(Vector3 position, Vector3 velocity, Color color, float size)
        {
            var go = new GameObject("SlimeSplitGoo");
            go.transform.position = position;
            var fx = go.AddComponent<SlimeSplitFx>();
            fx.kind = Kind.Goo;
            fx.lifetime = GooLife * Random.Range(0.85f, 1.15f);
            fx.velocity = velocity;
            fx.baseColor = color;
            fx.baseScale = size;
            fx.sprite = go.AddComponent<SpriteRenderer>();
            fx.sprite.sprite = SolidSpriteFactory.CreateCircle();
            fx.sprite.color = color;
            fx.sprite.sortingOrder = 6;
            go.transform.localScale = Vector3.one * size;
        }

        // 그림만 있는 젤리 방울 하나 (SlimeBroodFx 합체 연출도 쓴다).
        public static void Drop(Vector3 position, Vector3 velocity, Color color, float size) => Goo(position, velocity, color, size);

        public static bool OnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var vp = cam.WorldToViewportPoint(world);
            return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);

            if (kind == Kind.Goo)
            {
                velocity.y -= Gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                // 날아가는 쪽으로 살짝 길쭉하게, 끝 40%에서 옅어지며 작아진다
                float stretch = Mathf.Clamp(velocity.magnitude / 12f, 0f, 0.35f);
                float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                transform.localScale = new Vector3(baseScale * (1f - stretch * 0.5f), baseScale * (1f + stretch), 1f) * Mathf.Lerp(1f, 0.6f, t);
                transform.rotation = Quaternion.FromToRotation(Vector3.up, velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.up);
                var c = baseColor;
                c.a *= fade;
                sprite.color = c;
                if (t >= 1f) Destroy(gameObject);
                return;
            }

            // String: 한쪽 몸이 사라지면(쓰러짐) 바로 끊긴다
            if (endA == null || endB == null || t >= 1f)
            {
                Snap();
                return;
            }
            UpdateString(t);
        }

        private void UpdateString(float t)
        {
            if (endA != null) lastA = endA.position + offsetA;
            if (endB != null) lastB = endB.position + offsetB;
            line.SetPosition(0, lastA);
            line.SetPosition(1, lastB);
            // 처음엔 굵다가 늘어날수록 가늘어지고, 끊기기 직전 가운데가 더 얇아지는 느낌으로 끝 쪽 폭을 더 줄인다
            float w = Mathf.Lerp(startWidth, startWidth * 0.15f, t * t);
            line.startWidth = w;
            line.endWidth = w;
        }

        private void Snap()
        {
            var mid = (lastA + lastB) * 0.5f;
            var c = line != null ? line.startColor : GooColor;
            c.a = GooColor.a;
            Goo(mid, new Vector3(-2.5f, 3f, 0f), c, 0.18f);
            Goo(mid, new Vector3(2.5f, 3f, 0f), c, 0.18f);
            if (OnScreen(mid)) GameAudio.PlaySfx("JellyDrip", 0.6f);
            Destroy(gameObject);
        }
    }
}
