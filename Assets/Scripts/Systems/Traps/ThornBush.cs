using UnityEngine;

namespace IBlameYou.Systems
{
    // 숲 가시 덤불 (스테이지 2, 2026-11-24 사람 결정 C1. F3 미정 → 지금은 피해만): 닿으면 물리 피해, 튕기지 않는다.
    // 몸이 없어 길을 막지 않는다 (지나가며 맞거나 뛰어넘는다). 그림은 임시 (짙은 초록 덤불 사각형 + 가시 점, 아트 규칙).
    public class ThornBush : MonoBehaviour
    {
        private const float DamageMultiplier = 0.6f;
        private const float Cooldown = 1f;
        private static readonly Color BushColor = new Color(0.22f, 0.42f, 0.2f);
        private static readonly Color ThornColor = new Color(0.85f, 0.75f, 0.55f);

        private float nextHitTime;

        // bottomLeft: 덤불 왼쪽 아래 (서는 칸 바닥, 월드). width: 칸 수
        public static ThornBush Create(Transform parent, Vector2 bottomLeft, int width)
        {
            var go = new GameObject("ThornBush");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(bottomLeft.x + width / 2f, bottomLeft.y, 0f);

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(width - 0.3f, 0.7f);
            trigger.offset = new Vector2(0f, 0.35f);

            TrapCommon.AddSprite(go.transform, "Bush", null, new Vector2(0f, 0.4f), new Vector2(width - 0.1f, 0.8f), BushColor, 0);
            for (int i = 0; i < width * 2; i++)
            {
                float x = -width / 2f + 0.25f + i * 0.5f;
                TrapCommon.AddSprite(go.transform, "Thorn", null, new Vector2(x, 0.82f), new Vector2(0.12f, 0.12f), ThornColor, 1);
            }
            return go.AddComponent<ThornBush>();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time < nextHitTime) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;
            var playerHealth = player.GetComponent<HealthSystem>();
            if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvulnerable) return; // 무적 중엔 쿨다운·소리 없이 (가시 함정과 같게, 버그 점검 2026-11-24)
            if (TrapCommon.Strike(player, DamageKind.Physical, Element.Neutral, DamageMultiplier) <= 0f) return;
            nextHitTime = Time.time + Cooldown;
            GameAudio.PlaySfx("Hit_Neutral", 0.5f);
        }
    }
}
