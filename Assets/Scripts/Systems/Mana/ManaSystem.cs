using System;
using UnityEngine;

namespace IBlameYou.Systems
{
    public class ManaSystem : MonoBehaviour
    {
        [SerializeField] private float maxMana = 100f;
        [SerializeField] private float regenPerSecond = 5f;

        private float current;

        public float Max => maxMana;
        public float Current => current;
        public event Action<float, float> ManaChanged;

        private void Awake()
        {
            current = maxMana;
        }

        private void Update()
        {
            if (current < maxMana)
            {
                SetCurrent(current + regenPerSecond * Time.deltaTime);
            }
        }

        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (current < amount) return false;

            SetCurrent(current - amount);
            return true;
        }

        private void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 0f, maxMana);
            ManaChanged?.Invoke(current, maxMana);
        }
    }
}
