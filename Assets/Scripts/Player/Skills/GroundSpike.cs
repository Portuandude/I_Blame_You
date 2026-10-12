using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 바위 가시: 짧은 예고 뒤 땅에서 솟아올라 범위 안의 적에게 한 번 피해를 준다.
    public class GroundSpike : MonoBehaviour
    {
        private const float Delay = 0.25f;
        private const float Lifetime = 0.7f;

        private CombatStats owner;
        private float halfWidth;
        private float multiplier;
        private float age;
        private bool struck;

        public void Begin(CombatStats attacker, float width, float damageMultiplier)
        {
            owner = attacker;
            halfWidth = width;
            multiplier = damageMultiplier;
        }

        private void Update()
        {
            age += Time.deltaTime;

            if (!struck && age >= Delay)
            {
                struck = true;
                var center = (Vector2)transform.position + new Vector2(0f, 0.8f);
                var hit = new System.Collections.Generic.HashSet<HealthSystem>();
                foreach (var col in Physics2D.OverlapBoxAll(center, new Vector2(halfWidth * 2f, 1.6f), 0f))
                {
                    var health = col.GetComponentInParent<HealthSystem>();
                    if (health == null || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;
                    if (!hit.Add(health)) continue;
                    DamageCalculator.Strike(owner, health, DamageKind.Magic, Element.Earth, multiplier);
                }
            }

            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
