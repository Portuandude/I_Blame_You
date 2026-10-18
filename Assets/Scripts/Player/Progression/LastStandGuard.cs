using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 인내 20: 체력이 30% 이하일 때 받는 피해를 25% 줄인다.
    public class LastStandGuard : MonoBehaviour, IDamageModifier
    {
        private const float Threshold = 0.3f;
        private const float Reduction = 0.25f;

        private HealthSystem health;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
        }

        public float Modify(float damage, DamageKind kind, Element element)
        {
            if (health == null || health.Current > health.Max * Threshold) return damage;
            return damage * (1f - Reduction);
        }
    }
}
