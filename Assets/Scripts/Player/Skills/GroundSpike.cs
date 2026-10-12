using System;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 바위 가시: 예고 시간 동안 대상의 발밑을 따라가다가 땅에서 솟아올라 범위 안의 적에게 한 번 피해를 준다.
    // 대상이 없으면 처음 자리에서 솟는다.
    public class GroundSpike : MonoBehaviour
    {
        private const float Lifetime = 0.45f; // 솟아오른 뒤 남아 있는 시간

        private CombatStats owner;
        private float halfWidth;
        private float multiplier;
        private float delay;
        private HealthSystem target;
        private LayerMask groundMask;
        private Action<HealthSystem> onHit;
        private float age;
        private bool struck;
        private GameObject telegraph;

        public void Begin(CombatStats attacker, float width, float damageMultiplier, float strikeDelay, HealthSystem followTarget,
            LayerMask ground, Action<HealthSystem> hit)
        {
            owner = attacker;
            halfWidth = width;
            multiplier = damageMultiplier;
            delay = strikeDelay;
            target = followTarget;
            groundMask = ground;
            onHit = hit;

            // 예고: 솟아오를 자리에 납작한 갈색 표시.
            var color = ElementInfo.Color(Element.Earth);
            color.a = 0.55f;
            var marker = EffectLibrary.CreateVisual(transform, "Telegraph", null, 0f, false, color, 1f, 4);
            marker.transform.localScale = new Vector3(halfWidth * 2f, 0.25f, 1f);
            telegraph = marker.gameObject;
            if (target != null && !target.IsDead) Follow();
        }

        private void Update()
        {
            age += Time.deltaTime;

            if (!struck && target != null && !target.IsDead) Follow();

            if (!struck && age >= delay)
            {
                struck = true;
                if (telegraph != null) Destroy(telegraph);
                var frames = EffectLibrary.SpellFrames(Element.Earth);
                var visual = EffectLibrary.CreateVisual(transform, "Visual", frames, 14f, false,
                    ElementInfo.Color(Element.Earth), halfWidth * 1.5f);
                if (frames == null) visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                var center = (Vector2)transform.position + new Vector2(0f, 0.8f);
                var hit = new System.Collections.Generic.HashSet<HealthSystem>();
                foreach (var col in Physics2D.OverlapBoxAll(center, new Vector2(halfWidth * 2f, 1.6f), 0f))
                {
                    var health = col.GetComponentInParent<HealthSystem>();
                    if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;
                    if (!hit.Add(health)) continue;
                    DamageCalculator.Strike(owner, health, DamageKind.Magic, Element.Earth, multiplier);
                    onHit?.Invoke(health);
                }
            }

            if (age >= delay + Lifetime) Destroy(gameObject);
        }

        private void Follow()
        {
            Vector2 feet = target.transform.position;
            var ground = Physics2D.Raycast(feet + Vector2.up * 0.5f, Vector2.down, 30f, groundMask);
            transform.position = ground.collider != null ? (Vector3)ground.point : (Vector3)feet;
        }
    }
}
