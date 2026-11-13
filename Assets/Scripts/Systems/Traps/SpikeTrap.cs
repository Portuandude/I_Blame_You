using UnityEngine;

namespace IBlameYou.Systems
{
    // 바닥 가시. 밟으면 물리 피해를 받고 위로 튕겨 오른다 (대쉬 무적 중에는 그냥 지나간다).
    // 가끔 가시 끝 하나에 하얀 반짝(마름모)이 번쩍여 바닥과 구분되게 한다 (그림만, 판정 없음).
    [RequireComponent(typeof(BoxCollider2D))]
    public class SpikeTrap : MonoBehaviour
    {
        private const float DamageMultiplier = 1f;
        private const float Cooldown = 0.8f;
        private const float BounceSpeed = 11f;

        private const float GlintDuration = 0.35f;
        private const float GlintSize = 0.32f;

        private float nextHitTime;
        private int width;
        private System.Random glintRng; // 화면 연출 전용 난수 (UnityEngine.Random 상태를 건드리지 않게)
        private float nextGlint;
        private float glintTimer = -1f;
        private Transform glint;

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

            var trap = go.AddComponent<SpikeTrap>();
            trap.width = width;
            trap.glintRng = new System.Random(Mathf.RoundToInt(bottomLeft.x * 73f + bottomLeft.y * 191f));
            trap.nextGlint = 0.5f + (float)trap.glintRng.NextDouble() * 2f;

            var glintGo = new GameObject("Glint");
            glintGo.transform.SetParent(go.transform, false);
            glintGo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var glintRenderer = glintGo.AddComponent<SpriteRenderer>();
            glintRenderer.sprite = SolidSpriteFactory.CreateSquare();
            glintRenderer.color = new Color(1f, 1f, 1f, 0.9f);
            glintRenderer.sortingOrder = 1;
            glintGo.SetActive(false);
            trap.glint = glintGo.transform;
            return trap;
        }

        private void Update()
        {
            if (glint == null) return;
            if (glintTimer >= 0f)
            {
                glintTimer += Time.deltaTime;
                float t = glintTimer / GlintDuration;
                if (t >= 1f)
                {
                    glintTimer = -1f;
                    glint.gameObject.SetActive(false);
                    return;
                }
                float size = GlintSize * Mathf.Sin(t * Mathf.PI); // 커졌다 작아짐
                glint.localScale = new Vector3(size, size, 1f);
                return;
            }

            nextGlint -= Time.deltaTime;
            if (nextGlint > 0f) return;
            nextGlint = 1.8f + (float)glintRng.NextDouble() * 2.4f;
            int i = glintRng.Next(Mathf.Max(1, width));
            glint.localPosition = new Vector3(i - (width - 1) / 2f, 0.55f, 0f); // 가시 끝쯤
            glint.localScale = Vector3.zero;
            glint.gameObject.SetActive(true);
            glintTimer = 0f;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time < nextHitTime) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;

            var health = player.GetComponent<HealthSystem>();
            if (health == null || health.IsDead || health.IsInvulnerable) return;

            TrapCommon.Strike(player, DamageKind.Physical, Element.Neutral, DamageMultiplier);
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, BounceSpeed);
            nextHitTime = Time.time + Cooldown;
        }
    }
}
