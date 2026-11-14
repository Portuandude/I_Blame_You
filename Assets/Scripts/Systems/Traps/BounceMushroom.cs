using IBlameYou.Enemies;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 통통 버섯 (FeatureKind.BounceMushroom, 2026-11-14 레벨 재설계: 평지 위 높은 선반으로 가는 덤 길).
    // 플레이어가 위에서 내려앉으면 높이 튕겨 올린다 (보통 점프 약 2.4칸으로는 못 닿는 선반 위 동전 무더기까지).
    // 걸어서 지나가면 그냥 지나간다 (몸이 없는 트리거). 대시 중이거나 올라가는 중이면 튕기지 않는다.
    public class BounceMushroom : MonoBehaviour
    {
        public const float LaunchHeight = 7.5f;   // 버섯 윗면 근처에서 이만큼 더 오른다 (선반 서는 칸 = 바닥 + ChunkGenerator.BounceLedgeRise)
        private const float CapTop = 0.95f;       // 바닥에서 갓 윗면까지
        private const float LandTolerance = 0.4f; // 발이 갓 윗면보다 이만큼 아래여도 내려앉은 것으로 본다
        private const float Cooldown = 0.25f;
        private const float SquashTime = 0.35f;
        private static readonly Color CapColor = new Color(1f, 0.42f, 0.64f);
        private static readonly Color CapRim = new Color(0.86f, 0.28f, 0.5f);
        private static readonly Color StemColor = new Color(1f, 0.94f, 0.84f);
        private static readonly Color Spore = new Color(1f, 0.75f, 0.88f, 0.9f);

        private Transform visual;
        private float lastBounce = -10f;
        private float squashAge = 10f;

        // feet: 버섯이 선 바닥 윗면의 가운데 (월드).
        public static BounceMushroom Create(Transform parent, Vector2 feet)
        {
            var go = new GameObject("BounceMushroom");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(feet.x, feet.y, 0f);
            var mushroom = go.AddComponent<BounceMushroom>();

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.2f, 0.6f);
            trigger.offset = new Vector2(0f, CapTop - 0.2f);

            // 그림: 발 위치(바닥)를 피벗으로 눌렸다 펴지도록 Visual 아래에 둔다
            mushroom.visual = new GameObject("Visual").transform;
            mushroom.visual.SetParent(go.transform, false);
            var square = SolidSpriteFactory.CreateSquare();
            var circle = SolidSpriteFactory.CreateCircle();
            Part(mushroom.visual, "Stem", square, new Vector2(0f, 0.28f), new Vector2(0.34f, 0.56f), StemColor, 1);
            Part(mushroom.visual, "CapRim", circle, new Vector2(0f, 0.62f), new Vector2(1.24f, 0.62f), CapRim, 2);
            Part(mushroom.visual, "Cap", circle, new Vector2(0f, 0.68f), new Vector2(1.14f, 0.56f), CapColor, 3);
            Part(mushroom.visual, "SpotL", circle, new Vector2(-0.28f, 0.72f), new Vector2(0.18f, 0.14f), Color.white, 4);
            Part(mushroom.visual, "SpotR", circle, new Vector2(0.24f, 0.78f), new Vector2(0.22f, 0.17f), Color.white, 4);
            Part(mushroom.visual, "SpotT", circle, new Vector2(-0.02f, 0.88f), new Vector2(0.13f, 0.1f), Color.white, 4);
            return mushroom;
        }

        private static void Part(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time - lastBounce < Cooldown && Time.time >= lastBounce) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null || player.IsDashing) return;
            var body = player.GetComponent<Rigidbody2D>();
            if (body == null || body.linearVelocity.y > 0.5f) return; // 올라가는 중(아래에서 뛰어 지나감)이면 튕기지 않는다
            var health = player.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;
            float capTop = transform.position.y + CapTop;
            if (other.bounds.min.y < capTop - LandTolerance) return; // 옆에서 걸어 들어오면 그냥 지나간다

            lastBounce = Time.time;
            float gravity = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.01f, body.gravityScale);
            body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Sqrt(2f * gravity * LaunchHeight));
            player.ResetAirborne(); // 낮은 점프 끊기·코요테 점프·가짜 착지 먼지가 끼어들지 않게
            Bounced(new Vector3(transform.position.x, capTop, 0f));
        }

        private void Bounced(Vector3 top)
        {
            squashAge = 0f;
            GameAudio.PlaySfx("MushroomBoing", 0.7f, 0.05f);
            if (!SlimeSplitFx.OnScreen(top)) return;
            VfxPlayer.Dust(new Vector3(top.x, transform.position.y, 0f), 0.35f);
            for (int i = 0; i < 6; i++)
            {
                var v = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(1.2f, 3.2f), 0f);
                SlimeSplitFx.Drop(top + new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f), v, i % 3 == 0 ? Color.white : Spore, Random.Range(0.06f, 0.1f));
            }
        }

        private void Update()
        {
            if (visual == null) return;
            if (squashAge < SquashTime)
            {
                // 튕길 때: 납작하게 눌렸다가 길쭉하게 펴지며 흔들리다 제자리
                squashAge += Time.deltaTime;
                float t = Mathf.Clamp01(squashAge / SquashTime);
                float wave = Mathf.Cos(t * Mathf.PI * 2.5f) * (1f - t);
                float y = 1f - 0.35f * wave;
                visual.localScale = new Vector3(1f + 0.25f * wave, y, 1f);
                return;
            }
            // 가만히 있을 때: 숨 쉬듯 살짝 부풀었다 줄어든다 (버섯마다 박자가 다르게 위치로 어긋냄)
            float breathe = Mathf.Sin(Time.time * 2.2f + transform.position.x * 0.7f) * 0.03f;
            visual.localScale = new Vector3(1f - breathe * 0.5f, 1f + breathe, 1f);
        }
    }
}
