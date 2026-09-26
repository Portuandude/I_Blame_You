using System;
using UnityEngine;

namespace IBlameYou.Systems
{
    public class HealthSystem : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        private float current;

        public float Max => maxHealth;
        public float Current => current;
        public bool IsDead => current <= 0f;
        public bool Invulnerable { get; set; }
        public event Action<float, float> HealthChanged;
        public event Action<float> Damaged; // 무적 등으로 무시되지 않고 실제로 데미지가 들어갔을 때만 발생

        private void Awake()
        {
            current = maxHealth;
        }

        // 스폰 시 몹마다 다른 최대 체력을 코드로 지정할 때 사용 (예: 잡몹은 플레이어보다 적게).
        public void ConfigureMaxHealth(float value)
        {
            maxHealth = value;
            current = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || Invulnerable) return;
            SetCurrent(current - amount);
            Damaged?.Invoke(amount);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            SetCurrent(current + amount);
        }

        private void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 0f, maxHealth);
            HealthChanged?.Invoke(current, maxHealth);
        }
    }
}
