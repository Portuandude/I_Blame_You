using IBlameYou.Enemies;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Puzzles
{
    // 봉인문 연출 (그림·소리만, 푸는 순서·판정·기록은 ElementGate 그대로, 2026-11-13).
    // - SigilHit: 문양을 맞히면 그 자리에서 속성 색 고리가 퍼지고 문양이 한 번 크게 부풀었다 꺼진다
    // - Deflected: 틀린 속성·평타가 튕겨 나가면 유리 같은 "팅" (GateDeflect) + 흰 불꽃 조각 (기존 흰 번쩍임은 그대로)
    // - Shatter: 열리면 마력 벽이 속성 색 조각으로 깨져 위로 흩어지고 "차르르" (GateShatter, 기존 GateOpen과 겹쳐 남)
    //   + 문 자리에 잠깐 빛 기둥이 섰다 옅어지고 약한 화면 흔들림
    public class ElementGateFx : MonoBehaviour
    {
        private static readonly Color SparkColor = new Color(1f, 1f, 1f, 0.95f);

        private enum Kind { SigilPop, Column }

        private Kind kind;
        private SpriteRenderer sprite;
        private Color baseColor;
        private Vector3 baseScale;
        private float age;
        private float life;

        // 맞힌 문양: 고리 + 부풀었다 꺼지는 문양 복제 (원래 문양은 ElementGate가 바로 어둡게 바꾼다).
        public static void SigilHit(SpriteRenderer sigil, Color color)
        {
            if (sigil == null) return;
            // 고리는 문과 따로 둔다 (마지막 문양이면 문이 바로 사라지므로)
            var anchor = new GameObject("SigilRingAnchor");
            anchor.transform.position = sigil.transform.position;
            VfxPlayer.ManaRing(anchor.transform, Vector3.zero, new Color(color.r, color.g, color.b, 0.9f), 1.1f);
            Destroy(anchor, 1.5f);

            var go = new GameObject("SigilPop");
            go.transform.position = sigil.transform.position;
            go.transform.rotation = sigil.transform.rotation;
            var fx = go.AddComponent<ElementGateFx>();
            fx.kind = Kind.SigilPop;
            fx.life = 0.3f;
            fx.baseScale = sigil.transform.lossyScale;
            fx.sprite = go.AddComponent<SpriteRenderer>();
            fx.sprite.sprite = sigil.sprite;
            fx.baseColor = new Color(color.r, color.g, color.b, 0.9f);
            fx.sprite.color = fx.baseColor;
            fx.sprite.sortingOrder = sigil.sortingOrder + 1;
            go.transform.localScale = fx.baseScale;
        }

        // 튕겨냄. at: 문 가운데쯤 (문양 높이).
        public static void Deflected(Vector3 at)
        {
            for (int i = 0; i < 5; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var v = new Vector3(side * Random.Range(1.5f, 3f), Random.Range(0.5f, 3f), 0f);
                SlimeSplitFx.Drop(at, v, SparkColor, Random.Range(0.05f, 0.08f));
            }

            if (SlimeSplitFx.OnScreen(at)) GameAudio.PlaySfx("GateDeflect", 0.5f);
        }

        // 열림. bottom: 문 바닥 가운데, height: 벽 높이, color: 마지막 속성 색.
        public static void Shatter(Vector3 bottom, float height, Color color)
        {
            bool onScreen = SlimeSplitFx.OnScreen(bottom + Vector3.up * Mathf.Min(height, 3f) * 0.5f);
            if (onScreen)
            {
                GameAudio.PlaySfx("GateShatter", 0.5f);
                IBlameYou.Core.CameraFollow.ShakeMain(0.12f, 0.06f);
            }

            // 벽 조각: 높이를 따라 고르게, 위로 떠올랐다 떨어진다
            int count = Mathf.Clamp(Mathf.RoundToInt(height * 3f), 6, 18);
            var shard = new Color(color.r, color.g, color.b, 0.9f);
            for (int i = 0; i < count; i++)
            {
                var from = bottom + new Vector3(Random.Range(-0.4f, 0.4f), height * (i + 0.5f) / count, 0f);
                var v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 4.5f), 0f);
                SlimeSplitFx.Drop(from, v, i % 3 == 0 ? SparkColor : shard, Random.Range(0.07f, 0.13f));
            }

            // 빛 기둥: 벽 자리에 서서 넓어지며 옅어진다
            var go = new GameObject("GateLightColumn");
            go.transform.position = bottom + Vector3.up * (height / 2f);
            var fx = go.AddComponent<ElementGateFx>();
            fx.kind = Kind.Column;
            fx.life = 0.45f;
            fx.baseScale = new Vector3(0.9f, height, 1f);
            fx.sprite = go.AddComponent<SpriteRenderer>();
            fx.sprite.sprite = SolidSpriteFactory.CreateSquare();
            fx.baseColor = new Color(Mathf.Lerp(color.r, 1f, 0.6f), Mathf.Lerp(color.g, 1f, 0.6f), Mathf.Lerp(color.b, 1f, 0.6f), 0.7f);
            fx.sprite.color = fx.baseColor;
            fx.sprite.sortingOrder = 4;
            go.transform.localScale = fx.baseScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            var c = baseColor;
            if (kind == Kind.SigilPop)
            {
                // 빠르게 1.6배까지 부풀며 옅어진다
                transform.localScale = baseScale * (1f + 0.6f * Mathf.Sin(t * Mathf.PI * 0.5f));
                c.a *= 1f - t;
            }
            else
            {
                transform.localScale = new Vector3(baseScale.x * (1f + 1.5f * t), baseScale.y, 1f);
                c.a *= (1f - t) * (1f - t);
            }
            sprite.color = c;
        }
    }
}
