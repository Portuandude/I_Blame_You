using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 킹 착지 연출 (그림·소리만, 내려찍기·박치기 피해 폭·시간·수치는 SlimeKingController 그대로, 2026-11-13).
    // - Fall: 내려찍기가 떨어지기 시작할 때 아래로 휘는 "휘이익" (KingFall)
    // - Slam: 내려찍기 착지 때 충격파 그림(피해 폭만큼) + 양옆으로 튀는 보라 젤리 방울 + 무거운 "철퍼덕" (KingSlam)
    //   발광 연속 내려찍기는 index(0~)가 오를수록 소리가 높아지고 착지 자리에 금빛 고리가 하나 더 생긴다
    // - Splat: 착지 자리에 납작한 보라 젤리 얼룩이 남았다가 2초 동안 옅어진다 (땅 위, 캐릭터 뒤)
    // - Land: 박치기 착지 때 먼지 + 작은 방울 + 둔한 "쿵" (KingLand). 예전엔 화면 흔들림만 있었다
    public class SlimeKingLandFx : MonoBehaviour
    {
        public static readonly Color GlowRing = new Color(1f, 0.85f, 0.35f, 0.95f);
        private const float SplatLife = 2f;

        private SpriteRenderer sprite;
        private Color baseColor;
        private float age;

        public static void Fall(Vector3 position)
        {
            if (SlimeSplitFx.OnScreen(position)) GameAudio.PlaySfx("KingFall", 0.7f);
        }

        // feet: 착지한 바닥 위 발밑. width: 실제 피해 폭(칸). glowIndex: 발광 연속 내려찍기 회차, 아니면 -1.
        public static void Slam(Vector3 feet, float width, float size, int glowIndex)
        {
            bool onScreen = SlimeSplitFx.OnScreen(feet);
            if (onScreen)
            {
                float pitch = glowIndex >= 0 ? 1f + glowIndex * 0.06f : 1f;
                GameAudio.PlaySfxPitched("KingSlam", pitch, 0.85f);
            }

            VfxPlayer.Shockwave(feet, Mathf.Max(1f, width / 3.5f));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = Mathf.Lerp(15f, 60f, i / 2f) * Mathf.Deg2Rad;
                    var v = new Vector3(side * Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(5f, 8f) * Mathf.Sqrt(size);
                    SlimeSplitFx.Drop(feet + new Vector3(side * width * 0.25f, 0.2f, 0f), v, SlimeSplitFx.GooColor, Random.Range(0.16f, 0.28f) * size);
                }
            }

            Splat(feet, width * 0.6f);

            if (glowIndex >= 0)
            {
                var anchor = new GameObject("KingGlowSlamFx");
                anchor.transform.position = feet + Vector3.up * 0.5f;
                VfxPlayer.ManaRing(anchor.transform, Vector3.zero, GlowRing, width * 0.6f);
                Destroy(anchor, 0.6f);
            }
        }

        public static void Land(Vector3 feet, float size)
        {
            if (SlimeSplitFx.OnScreen(feet)) GameAudio.PlaySfx("KingLand", 0.75f);
            VfxPlayer.Dust(feet, 1f);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var v = new Vector3(side * Random.Range(2f, 4f), Random.Range(2.5f, 4f), 0f);
                SlimeSplitFx.Drop(feet + Vector3.up * 0.2f, v, SlimeSplitFx.GooColor, Random.Range(0.12f, 0.2f) * size);
            }
        }

        private static void Splat(Vector3 feet, float width)
        {
            var go = new GameObject("KingSlamSplat");
            go.transform.position = feet + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(Mathf.Max(1f, width), 0.3f, 1f);
            var fx = go.AddComponent<SlimeKingLandFx>();
            fx.sprite = go.AddComponent<SpriteRenderer>();
            fx.sprite.sprite = SolidSpriteFactory.CreateCircle();
            fx.baseColor = new Color(0.62f, 0.38f, 0.92f, 0.6f);
            fx.sprite.color = fx.baseColor;
            fx.sprite.sortingOrder = -1; // 땅 그림 위, 캐릭터·발판 뒤 (VfxPlayer.Dust와 같은 층)
        }

        private void Update()
        {
            age += Time.deltaTime;
            float k = age / SplatLife;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // 처음 0.5초는 그대로, 나머지 동안 옅어진다
            var c = baseColor;
            c.a *= 1f - Mathf.Clamp01((k - 0.25f) / 0.75f);
            sprite.color = c;
        }
    }
}
