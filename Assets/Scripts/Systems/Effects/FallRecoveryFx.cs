using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 낭떠러지 추락·복귀 연출 (그림·소리만, 잃는 체력·복귀 자리·무적 1초는 FallRecovery 그대로, 2026-11-13).
    // - Falling: 바닥 아래로 떨어지기 시작하면 "휘이이잉" (PitFall) 한 번
    // - Returned: 복귀 순간 화면이 잠깐 어두워졌다 밝아지고(ScreenFlash.RoomFade), 복귀 자리에 aqua 빛 고리 + 반짝 조각 +
    //   발밑 먼지 + "뿅" (PitReturn). 모래시계로 피해 없이 돌아오면 금빛 고리 + 위에서 흘러내리는 금빛 모래 + 조금 높은 음
    // - 복귀 뒤 무적 시간 동안 몸 그림이 깜빡인다 (forceRenderingOff만 켰다 끈다: 색·머티리얼을 쓰는 다른 연출과 겹치지 않게)
    public class FallRecoveryFx : MonoBehaviour
    {
        private static readonly Color ReturnColor = new Color(0.5f, 0.97f, 1f, 0.85f); // 방 입장 고리와 같은 aqua
        private static readonly Color SandColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        private const float BlinkRate = 12f; // 초당 깜빡임 수

        private SpriteRenderer visual;
        private float blinkUntil;

        public static void Falling()
        {
            GameAudio.PlaySfx("PitFall", 0.5f);
        }

        // player: 돌아온 플레이어, feet: 복귀 자리 (발밑), free: 모래시계로 피해 없이, invulnerable: 무적 시간 (깜빡임 길이).
        public static void Returned(Transform player, Vector3 feet, bool free, float invulnerable)
        {
            if (player == null) return;
            ScreenFlash.RoomFade();
            GameAudio.PlaySfxPitched("PitReturn", free ? 1.25f : 1f, 0.55f);

            Color ring = free ? SandColor : ReturnColor;
            ManaRingFx.Create(player, new Vector3(0f, 0.5f, 0f), ring, 1.2f);
            VfxPlayer.Dust(feet, 0.4f);

            Vector3 body = player.position + new Vector3(0f, 0.5f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var v = new Vector3(side * Random.Range(0.8f, 2f), Random.Range(2f, 3.5f), 0f);
                SlimeSplitFx.Drop(body, v, ring, Random.Range(0.05f, 0.08f));
            }

            if (free)
            {
                // 모래시계: 머리 위에서 금빛 모래가 흘러내린다
                for (int i = 0; i < 8; i++)
                {
                    var from = player.position + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(1.4f, 1.9f), 0f);
                    SlimeSplitFx.Drop(from, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.5f, 0.5f), 0f), SandColor,
                        Random.Range(0.04f, 0.07f));
                }
            }

            var fx = player.GetComponent<FallRecoveryFx>();
            if (fx == null) fx = player.gameObject.AddComponent<FallRecoveryFx>();
            fx.blinkUntil = Time.time + invulnerable;
        }

        private void Awake()
        {
            var child = transform.Find("Visual");
            visual = child != null ? child.GetComponent<SpriteRenderer>() : null;
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            bool blinking = Time.time < blinkUntil;
            visual.forceRenderingOff = blinking && Mathf.Repeat(Time.time * BlinkRate, 1f) < 0.35f;
        }

        private void OnDisable()
        {
            if (visual != null) visual.forceRenderingOff = false;
        }
    }
}
