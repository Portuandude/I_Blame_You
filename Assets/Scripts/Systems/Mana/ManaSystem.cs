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

        // 레벨업 특성용 배율: 재생 속도, 소모량, 헛되이 날린 마나 중 돌려받는 비율.
        public float RegenMultiplier { get; set; } = 1f;
        public float CostMultiplier { get; set; } = 1f;
        public float WastedRefundRatio { get; set; }
        public float Current => current;
        public bool Infinite { get; set; } // 개발자 모드 치트
        // 마나가 모자랄 때 (쓰는 사람, 모자란 만큼)을 다른 것으로 대신 냈으면 true를 돌려준다 (유물 피의 계약). 없으면 그냥 실패.
        public Func<ManaUser, float, bool> ShortfallCover { get; set; }
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
                SetCurrent(current + regenPerSecond * RegenMultiplier * Time.deltaTime);
            }
        }

        // 최대 마나를 늘린다 (늘어난 만큼 현재 마나도 채운다).
        public void AddMax(float amount)
        {
            maxMana += amount;
            SetCurrent(current + Mathf.Max(amount, 0f));
        }

        // 체크포인트 등에서 마나를 채운다 (사용/낭비 기록에는 넣지 않는다).
        public void Restore(float amount)
        {
            if (amount > 0f) SetCurrent(current + amount);
        }

        public bool HasEnough(float amount) => Infinite || current >= amount;

        public bool TryConsume(float amount) => TryConsume(amount, ManaUser.Other);

        public bool TryConsume(float amount, ManaUser user)
        {
            if (amount <= 0f) return true;
            amount *= CostMultiplier;
            if (!HasEnough(amount))
            {
                if (ShortfallCover == null || !ShortfallCover(user, amount - current)) return false;
                SetCurrent(0f); // 있던 마나는 다 쓰고 모자란 만큼은 대신 냈다
            }
            else if (!Infinite)
            {
                SetCurrent(current - amount);
            }
            spentByUser[(int)user] += amount;
            ManaSpent?.Invoke(user, amount, false);
            return true;
        }

        // 이미 쓴 마나가 미니게임 실패로 헛되이 날아갔음을 기록한다 (마나는 TryConsume에서 이미 빠졌다).
        public void MarkWasted(ManaUser user, float amount)
        {
            wastedByUser[(int)user] += amount;
            ManaSpent?.Invoke(user, amount, true);
            if (WastedRefundRatio > 0f && !Infinite) SetCurrent(current + amount * CostMultiplier * WastedRefundRatio);
        }

        private void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 0f, maxMana);
            ManaChanged?.Invoke(current, maxMana);
        }
    }
}
