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
        public event Action<float, float> HealthChanged;

        private void Awake()
        {
            current = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;
            SetCurrent(current - amount);
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
