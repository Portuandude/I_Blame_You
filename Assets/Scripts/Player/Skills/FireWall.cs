using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 불꽃 장막 (아카데미 마법, Docs/CityDesign.md 5절 — 불 = 범위): 캐릭터 앞 땅 위에 잠깐 서 있는 불벽.
    // TickInterval마다 벽 안의 적 모두에게 작은 불 피해, 처음 닿은 적은 반드시 화상. 적이 지나가며 여러 번 데인다.
    public class FireWall : MonoBehaviour
    {
        public const float TickInterval = 0.4f;
        public const float WallHeight = 2.2f;
        private float height = WallHeight;

        private CombatStats owner;
        private float halfWidth;
        private float multiplier;
        private float duration;
        private float age;
        private float nextTick;
        private readonly HashSet<HealthSystem> burned = new HashSet<HealthSystem>();
        private readonly HashSet<HealthSystem> hitThisTick = new HashSet<HealthSystem>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private System.Action<HealthSystem, bool> onHit; // (맞은 적, 처음 닿았는지)

        // rain: 불꽃 비 — 높이 zoneHeight의 넓은 구역에 위에서 불똥이 떨어지는 그림 (판정은 같은 상자).
        public void Begin(CombatStats attacker, float width, float damagePerTick, float lifetime, System.Action<HealthSystem, bool> hit,
            bool rain = false, float zoneHeight = WallHeight, Sprite[] art = null)
        {
            height = zoneHeight;
            owner = attacker;
            halfWidth = width;
            multiplier = damagePerTick;
            duration = lifetime;
            onHit = hit;

            // 그림: 전용 그림(pixelfx — 장막은 땅에 선 불꽃 기둥, 비는 아래로 떨어지는 불똥)이 있으면 그것,
            // 없으면 불 비행 프레임을 가로로 늘어세운다 (예전 그림)
            var frames = art ?? EffectLibrary.FlightFrames(Element.Fire);
            int count = Mathf.Max(3, Mathf.RoundToInt(halfWidth * 2f));
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-halfWidth + 0.5f, halfWidth - 0.5f, count == 1 ? 0.5f : i / (float)(count - 1));
                var flame = EffectLibrary.CreateVisual(transform, "Flame", frames, 12f + i % 3, true, ElementInfo.Color(Element.Fire), 1.1f, 6);
                if (rain)
                {
                    // 불똥: 구역 위쪽 여기저기에서 아래로 (FireRainDrop이 떨어뜨리고 다시 위로)
                    flame.transform.localPosition = new Vector3(x, Random.Range(0.5f, height), 0f);
                    if (art == null) flame.transform.localRotation = Quaternion.Euler(0f, 0f, -90f); // 전용 그림은 이미 아래를 향함
                    flame.gameObject.AddComponent<FireRainDrop>().Begin(height);
                    continue;
                }
                if (art != null)
                {
                    // 아래 가운데 피벗이라 땅(0)에 세운다. 높낮이·속도를 조금씩 달리해 기둥이 똑같아 보이지 않게
                    flame.transform.localPosition = new Vector3(x, 0f, 0f);
                    flame.transform.localScale = new Vector3(1f, 1f + (i % 3) * 0.08f, 1f);
                    flame.GetComponent<SpriteFrameAnimator>()?.SkipFrames(i * 5);
                    continue;
                }
                flame.transform.localPosition = new Vector3(x, 0.7f + (i % 2) * 0.25f, 0f);
                flame.transform.localScale *= 1.4f;
                flame.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 위로 타오르게
            }
            GameAudio.PlaySfx("Hit_Fire", 0.8f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= nextTick)
            {
                nextTick += TickInterval;
                var center = (Vector2)transform.position + new Vector2(0f, height / 2f);
                hitThisTick.Clear();
                foreach (var col in OverlapQuery.Box(center, new Vector2(halfWidth * 2f, height), overlaps))
                {
                    var health = col.GetComponentInParent<HealthSystem>();
                    if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;
                    if (!hitThisTick.Add(health)) continue;
                    DamageCalculator.Strike(owner, health, DamageKind.Magic, Element.Fire, multiplier);
                    onHit?.Invoke(health, burned.Add(health));
                }
            }

            // 끝나기 0.3초 전부터 줄어들며 꺼진다
            float left = duration - age;
            if (left < 0.3f) transform.localScale = new Vector3(1f, Mathf.Max(0.05f, left / 0.3f), 1f);
            if (age >= duration) Destroy(gameObject);
        }
    }
}
