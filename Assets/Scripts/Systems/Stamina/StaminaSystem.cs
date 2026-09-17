using System;
using UnityEngine;

namespace IBlameYou.Systems
{
    public class StaminaSystem : MonoBehaviour
    {
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float regenPerSecond = 15f;
        [SerializeField] private float regenDelay = 0.5f;

        private float current;
        private float timeSinceLastConsume;

        public float Max => maxStamina;
        public float Current => current;
        public event Action<float, float> StaminaChanged;

        private void Awake()
        {
            current = maxStamina;
        }

        private void Update()
        {
            timeSinceLastConsume += Time.deltaTime;

            if (current < maxStamina && timeSinceLastConsume >= regenDelay)
            {
                SetCurrent(current + regenPerSecond * Time.deltaTime);
            }
        }

        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (current < amount) return false;

            SetCurrent(current - amount);
            timeSinceLastConsume = 0f;
            return true;
        }

        private void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 0f, maxStamina);
            StaminaChanged?.Invoke(current, maxStamina);
        }
    }
}
