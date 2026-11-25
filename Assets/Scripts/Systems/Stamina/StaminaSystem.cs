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

            if (current < maxStamina && timeSinceLastConsume >= regenDelay && Time.deltaTime > 0f) // 멈춘 창(timeScale 0) 동안 같은 값으로 매 프레임 알리지 않게
            {
                SetCurrent(current + regenPerSecond * Time.deltaTime);
            }
        }

        // 최대 기력을 늘리거나 줄인다 (장비). 늘어난 만큼 현재 기력도 채우고, 줄면 새 최대치로 자른다.
        public void AddMax(float amount, bool fillCurrent = true)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount)) return; // NaN이면 최대·현재 기력이 NaN이 되어 무한 기력·재생 멈춤 (HealthSystem.AddMax와 같은 가드)
            maxStamina = Mathf.Max(1f, maxStamina + amount);
            SetCurrent(fillCurrent ? current + Mathf.Max(amount, 0f) : current); // fillCurrent false: 장비처럼 끼고 빼기를 반복해도 회복되지 않게
        }

        // 기력을 가득 채운다 (스테이지 시작 복원)
        public void Fill() => SetCurrent(maxStamina);

        public bool TryConsume(float amount)
        {
            if (float.IsNaN(amount)) return false;
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
