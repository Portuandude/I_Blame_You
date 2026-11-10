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
        // Invulnerable(대쉬·순간이동·경직이 켜고 끄는 공용 스위치)과 별개로, 시간이 지나면 저절로 끝나는 짧은 무적 (유물 그림자 베일).
        // 공용 스위치를 직접 끄지 않으므로 다른 무적을 일찍 끝내 버리지 않는다.
        public bool IsInvulnerable => Invulnerable || Time.time < graceUntil;
        private float graceUntil = -1f;
        public event Action<float, float> HealthChanged;
        public event Action<float> Damaged; // 무적 등으로 무시되지 않고 실제로 데미지가 들어갔을 때만 발생
        public event Action DeathPrevented;

        // 쓰러질 피해를 받으면 대신 최대 체력의 DeathSaveRatio로 버티는 횟수 (레벨업 특성).
        public int DeathSaves { get; set; }
        public float DeathSaveRatio { get; set; } = 0.3f;

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
            if (amount <= 0f || IsInvulnerable) return;
            if (current - amount <= 0f && DeathSaves > 0 && current > 0f)
            {
                DeathSaves--;
                SetCurrent(maxHealth * DeathSaveRatio);
                Damaged?.Invoke(amount);
                DeathPrevented?.Invoke();
                return;
            }

            SetCurrent(current - amount);
            Damaged?.Invoke(amount);
        }

        // 지금부터 seconds초 동안 피해를 받지 않는다 (이미 더 긴 무적이 남아 있으면 그대로).
        public void GrantGrace(float seconds)
        {
            if (seconds > 0f) graceUntil = Mathf.Max(graceUntil, Time.time + seconds);
        }

        // 최대 체력을 늘린다 (늘어난 만큼 현재 체력도 채운다).
        public void AddMax(float amount)
        {
            maxHealth += amount;
            SetCurrent(current + Mathf.Max(amount, 0f));
        }

        // 최대/현재 체력을 한꺼번에 정한다 (보스 분열처럼 체력을 나눠 가질 때).
        public void SetValues(float currentValue, float maxValue)
        {
            maxHealth = maxValue;
            SetCurrent(currentValue);
        }

        // 경직/화면 흔들림 없이 체력만 깎는다 (광폭화의 지속 체력 감소 같은 자해 효과용).
        public void Drain(float amount)
        {
            if (amount <= 0f || IsDead) return;
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
