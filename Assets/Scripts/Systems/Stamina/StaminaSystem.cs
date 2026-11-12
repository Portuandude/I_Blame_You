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

        // 최대 기력을 늘리거나 줄인다 (장비). 늘어난 만큼 현재 기력도 채우고, 줄면 새 최대치로 자른다.
        public void AddMax(float amount)
        {
            maxStamina = Mathf.Max(1f, maxStamina + amount);
            SetCurrent(current + Mathf.Max(amount, 0f));
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
