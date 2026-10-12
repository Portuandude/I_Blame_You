using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 직선으로 날아가며 겹친 대상에게 피해를 주는 투사체 (B의 공격 스킬, 고블린 궁수의 화살).
    // Rigidbody 없이 매 물리 프레임 위치를 옮기고 겹침 조회로 판정한다. 벽/바닥에 닿으면 사라진다.
    public class Projectile : MonoBehaviour
    {
        private CombatStats owner;
        private bool hitsEnemies;
        private DamageKind kind;
        private Element element;
        private float multiplier = 1f;
        private Vector2 velocity;
        private float radius;
        private bool piercing;
        private float lifeRemaining;
        private LayerMask groundMask;
        private readonly HashSet<HealthSystem> alreadyHit = new HashSet<HealthSystem>();

        // owner: 피해 계산에 쓸 공격자 스탯. hitEnemies: true면 적만, false면 플레이어만 맞힌다.
        public void Launch(CombatStats attacker, bool hitEnemies, DamageKind damageKind, Element damageElement, Vector2 direction,
            float speed, float hitRadius, float range, bool pierce, float damageMultiplier = 1f)
        {
            owner = attacker;
            hitsEnemies = hitEnemies;
            kind = damageKind;
            element = damageElement;
            multiplier = damageMultiplier;
            velocity = direction.normalized * speed;
            radius = hitRadius;
            piercing = pierce;
            lifeRemaining = speed > 0f ? range / speed : 1f;

            int ground = LayerMask.NameToLayer("Ground");
            groundMask = ground >= 0 ? (LayerMask)(1 << ground) : (LayerMask)0;

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void FixedUpdate()
        {
            Vector2 position = transform.position;
            Vector2 step = velocity * Time.fixedDeltaTime;

            if (groundMask != 0 && Physics2D.Raycast(position, step.normalized, step.magnitude, groundMask))
            {
                Destroy(gameObject);
                return;
            }

            transform.position = position + step;

            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, radius))
            {
                var health = hit.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || !alreadyHit.Add(health)) continue;
                if (owner != null && health.gameObject == owner.gameObject) continue;

                bool isEnemy = health.GetComponent<IBlameYou.Enemies.EnemyController>() != null;
                if (isEnemy != hitsEnemies)
                {
                    alreadyHit.Remove(health);
                    continue;
                }

                DamageCalculator.Strike(owner, health, kind, element, multiplier);
                if (!piercing)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            lifeRemaining -= Time.fixedDeltaTime;
            if (lifeRemaining <= 0f) Destroy(gameObject);
        }
    }
}
