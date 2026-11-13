using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 깜빡 발판 연출 (그림·소리만, 박자·보임/경고/사라짐 시간·솜구름 유예는 BlinkPlatform 그대로, 2026-11-13).
    // - Vanish: 발판이 꺼지는 순간 칸마다 하늘빛 조각이 흩어진다 (화면 안만)
    // - Appear: 다시 생기는 순간 칸마다 작은 반짝 + 카메라 가까이면 작은 "띵" (BlinkAppear, 0.25초 간격 제한)
    // - Underfoot: 플레이어가 서 있는데 꺼지면 "퐁" (BlinkVanish) + 발밑 작은 먼지
    // - CloudRise / CloudFade: 솜구름이 받쳐 주기 시작할 때 하얀 솜 조각이 몽글 피어오르고, 걷힐 때 옅게 퍼진다
    // 발판이 살짝 커졌다 제자리로 돌아오는 것은 BlinkPlatform이 직접 그린다 (판자 그림을 가진 곳).
    public class BlinkPlatformFx : MonoBehaviour
    {
        private const float AppearSfxGap = 0.25f; // 다리 위 발판들이 차례로 나타나도 "띵"이 겹쳐 시끄럽지 않게
        private const float VanishSfxGap = 0.1f;
        private static readonly Color Shard = new Color(0.62f, 0.9f, 1f, 0.9f);
        private static readonly Color Cotton = new Color(1f, 0.97f, 1f, 0.85f);

        private static float lastAppearSfx = -10f;
        private static float lastVanishSfx = -10f;

        // 솜 조각 하나 (중력 없이 천천히 떠오르거나 퍼지며 커지고 옅어진다)
        private SpriteRenderer sprite;
        private Vector3 velocity;
        private Color color;
        private float startSize;
        private float endSize;
        private float lifetime;
        private float age;

        // center: 발판 가운데, width: 칸 수.
        public static void Vanish(Vector3 center, int width)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            for (int i = 0; i < width; i++)
            {
                var cell = center + new Vector3(i - (width - 1) / 2f, 0f, 0f);
                for (int k = 0; k < 3; k++)
                {
                    var v = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.5f, 2.4f), 0f);
                    SlimeSplitFx.Drop(cell + new Vector3(Random.Range(-0.35f, 0.35f), 0f, 0f), v,
                        k == 2 ? new Color(1f, 1f, 1f, 0.85f) : Shard, Random.Range(0.06f, 0.11f));
                }
            }
        }

        // center: 발판 가운데, width: 칸 수.
        public static void Appear(Vector3 center, int width)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            if (Time.time - lastAppearSfx >= AppearSfxGap || Time.time < lastAppearSfx)
            {
                lastAppearSfx = Time.time;
                GameAudio.PlaySfx("BlinkAppear", 0.4f, 0.02f);
            }
            for (int i = 0; i < width; i++)
                VfxPlayer.AimGlint(center + new Vector3(i - (width - 1) / 2f + Random.Range(-0.2f, 0.2f), 0.2f, 0f));
        }

        // feet: 꺼진 발판 위에 서 있던 플레이어의 발 위치.
        public static void Underfoot(Vector3 feet)
        {
            if (Time.time - lastVanishSfx >= VanishSfxGap || Time.time < lastVanishSfx)
            {
                lastVanishSfx = Time.time;
                GameAudio.PlaySfx("BlinkVanish", 0.55f);
            }
            VfxPlayer.Dust(feet, 0.2f);
        }

        // 솜구름이 받쳐 주기 시작할 때: 칸마다 하얀 솜 조각이 몽글 피어오른다.
        public static void CloudRise(Vector3 center, int width)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            for (int i = 0; i < width; i++)
            {
                var cell = center + new Vector3(i - (width - 1) / 2f, 0f, 0f);
                for (int k = 0; k < 2; k++)
                {
                    var pos = cell + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.1f, 0.1f), 0f);
                    var v = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.4f, 0.8f), 0f);
                    Puff(pos, v, Cotton, 0.18f, 0.38f, Random.Range(0.6f, 0.8f));
                }
            }
        }

        // 솜구름이 걷힐 때: 옅은 솜 조각이 양옆으로 퍼지며 사라진다.
        public static void CloudFade(Vector3 center, int width)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            var faint = new Color(Cotton.r, Cotton.g, Cotton.b, 0.45f);
            for (int i = 0; i < width; i++)
            {
                var cell = center + new Vector3(i - (width - 1) / 2f, 0f, 0f);
                float side = i - (width - 1) / 2f >= 0f ? 1f : -1f;
                var v = new Vector3(side * Random.Range(0.4f, 0.9f), Random.Range(-0.1f, 0.2f), 0f);
                Puff(cell, v, faint, 0.3f, 0.6f, 0.5f);
            }
        }

        private static void Puff(Vector3 position, Vector3 velocity, Color color, float startSize, float endSize, float lifetime)
        {
            var go = new GameObject("BlinkCottonPuff");
            go.transform.position = position;
            var fx = go.AddComponent<BlinkPlatformFx>();
            fx.sprite = go.AddComponent<SpriteRenderer>();
            fx.sprite.sprite = SolidSpriteFactory.CreateCircle();
            fx.sprite.sortingOrder = 6;
            fx.sprite.color = color;
            fx.velocity = velocity;
            fx.color = color;
            fx.startSize = startSize;
            fx.endSize = endSize;
            fx.lifetime = lifetime;
            go.transform.localScale = Vector3.one * startSize;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position += velocity * Time.deltaTime;
            velocity *= 1f - Mathf.Min(1f, 2f * Time.deltaTime); // 점점 느려진다
            float grow = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, grow);
            var c = color;
            c.a *= 1f - t * t;
            sprite.color = c;
        }
    }
}
