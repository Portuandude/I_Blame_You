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
        private const float Height = 2.2f;

        private CombatStats owner;
        private float halfWidth;
        private float multiplier;
        private float duration;
        private float age;
        private float nextTick;
        private readonly HashSet<HealthSystem> burned = new HashSet<HealthSystem>();
        private System.Action<HealthSystem, bool> onHit; // (맞은 적, 처음 닿았는지)

        public void Begin(CombatStats attacker, float width, float damagePerTick, float lifetime, System.Action<HealthSystem, bool> hit)
        {
            owner = attacker;
            halfWidth = width;
            multiplier = damagePerTick;
            duration = lifetime;
            onHit = hit;

            // 그림: 불 비행 프레임을 가로로 늘어세운 불꽃 기둥들 (기존 속성 이펙트 다시 쓰기)
            var frames = EffectLibrary.FlightFrames(Element.Fire);
            int count = Mathf.Max(3, Mathf.RoundToInt(halfWidth * 2f));
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-halfWidth + 0.5f, halfWidth - 0.5f, count == 1 ? 0.5f : i / (float)(count - 1));
                var flame = EffectLibrary.CreateVisual(transform, "Flame", frames, 12f + i % 3, true, ElementInfo.Color(Element.Fire), 1.1f, 6);
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
                var center = (Vector2)transform.position + new Vector2(0f, Height / 2f);
                var hitThisTick = new HashSet<HealthSystem>();
                foreach (var col in Physics2D.OverlapBoxAll(center, new Vector2(halfWidth * 2f, Height), 0f))
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
