using UnityEngine;

namespace IBlameYou.Systems
{
    // 바닥 가시. 밟으면 물리 피해를 받고 위로 튕겨 오른다 (대쉬 무적 중에는 그냥 지나간다).
    [RequireComponent(typeof(BoxCollider2D))]
    public class SpikeTrap : MonoBehaviour
    {
        private const float DamageMultiplier = 1f;
        private const float Cooldown = 0.8f;
        private const float BounceSpeed = 11f;

        private float nextHitTime;

        // (x, y)는 가시 줄 왼쪽 아래 모서리(월드), width는 칸 수.
        public static SpikeTrap Create(Transform parent, Vector2 bottomLeft, int width)
        {
            var go = new GameObject("Spikes");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(bottomLeft.x + width / 2f, bottomLeft.y, 0f);

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(width - 0.2f, 0.45f);
            trigger.offset = new Vector2(0f, 0.25f);

            var sprite = TrapCommon.Frame("Spikes");
            for (int i = 0; i < width; i++)
            {
                TrapCommon.AddSprite(go.transform, $"Spike_{i}", sprite, new Vector2(i - (width - 1) / 2f, 0.3f), new Vector2(0.9f, 0.6f),
                    new Color(0.85f, 0.85f, 0.9f), 0);
            }

            return go.AddComponent<SpikeTrap>();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time < nextHitTime) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;

            var health = player.GetComponent<HealthSystem>();
            if (health == null || health.IsDead || health.Invulnerable) return;

            TrapCommon.Strike(player, DamageKind.Physical, Element.Neutral, DamageMultiplier);
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, BounceSpeed);
            nextHitTime = Time.time + Cooldown;
        }
    }
}
