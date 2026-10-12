using System;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마나를 쓰는 주체. 플레이어 A와 B는 한 캐릭터의 마나를 공유한다 (기획서 0.1의 12번).
    public enum ManaUser
    {
        PlayerA,
        PlayerB,
        Other
    }

    public class ManaSystem : MonoBehaviour
    {
        [SerializeField] private float maxMana = 100f;
        [SerializeField] private float regenPerSecond = 5f;

        private float current;
        private readonly float[] spentByUser = new float[3];
        private readonly float[] wastedByUser = new float[3];

        public float Max => maxMana;
        public float Current => current;
        public bool Infinite { get; set; } // 개발자 모드 치트
        public event Action<float, float> ManaChanged;

        // (누가, 얼마나, 헛되이 날렸는지) — 서로 탓하기 로그/HUD용.
        public event Action<ManaUser, float, bool> ManaSpent;

        public float SpentBy(ManaUser user) => spentByUser[(int)user];
        public float WastedBy(ManaUser user) => wastedByUser[(int)user];

        private void Awake()
        {
            current = maxMana;
        }

        public void ConfigureMax(float value)
        {
            maxMana = value;
            current = maxMana;
        }

        private void Update()
        {
            if (current < maxMana)
            {
                SetCurrent(current + regenPerSecond * Time.deltaTime);
            }
        }

        public bool HasEnough(float amount) => Infinite || current >= amount;

        public bool TryConsume(float amount) => TryConsume(amount, ManaUser.Other);

        public bool TryConsume(float amount, ManaUser user)
        {
            if (amount <= 0f) return true;
            if (!HasEnough(amount)) return false;

            if (!Infinite) SetCurrent(current - amount);
            spentByUser[(int)user] += amount;
            ManaSpent?.Invoke(user, amount, false);
            return true;
        }

        // 이미 쓴 마나가 미니게임 실패로 헛되이 날아갔음을 기록한다 (마나는 TryConsume에서 이미 빠졌다).
        public void MarkWasted(ManaUser user, float amount)
        {
            wastedByUser[(int)user] += amount;
            ManaSpent?.Invoke(user, amount, true);
        }

        private void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 0f, maxMana);
            ManaChanged?.Invoke(current, maxMana);
        }
    }
}
