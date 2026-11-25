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
        public bool GraceActive => Time.time < graceUntil; // 시간 무적만 (깜빡임 연출용)
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
            // 쓰러진 뒤 같은 프레임에 또 맞으면 HealthChanged(0)·Damaged가 다시 나가 쓰러짐 처리가 두 번 돌던 것, NaN 피해가 체력을 NaN으로 만들던 것 (버그 점검 2026-11-24)
            if (!(amount > 0f) || IsDead || IsInvulnerable) return;
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

        private const float DotDeathSaveGrace = 1f; // 지속 피해로 버티기를 쓴 뒤 무적 (화상 틱 간격 1초)

        // 지금부터 seconds초 동안 피해를 받지 않는다 (이미 더 긴 무적이 남아 있으면 그대로).
        public void GrantGrace(float seconds)
        {
            if (seconds > 0f) graceUntil = Mathf.Max(graceUntil, Time.time + seconds);
        }

        // 최대 체력을 늘린다 (늘어난 만큼 현재 체력도 채운다).
        public void AddMax(float amount, bool fillCurrent = true)
        {
            if (float.IsNaN(amount)) return;
            maxHealth = Mathf.Max(1f, maxHealth + amount); // 최대치가 0 이하가 되면 체력이 음수로 잘려 바로 쓰러지던 것 (버그 점검 2026-11-25, 기력과 같게)
            SetCurrent(fillCurrent && !IsDead ? current + Mathf.Max(amount, 0f) : current); // 쓰러진 뒤에는 채우지 않는다 (Heal과 같게). fillCurrent false: 장비처럼 끼고 빼기를 반복해도 회복되지 않게
        }

        // 최대/현재 체력을 한꺼번에 정한다 (보스 분열처럼 체력을 나눠 가질 때).
        public void SetValues(float currentValue, float maxValue)
        {
            if (!(maxValue > 0f) || float.IsNaN(currentValue)) return; // NaN·0 이하 값이면 죽지 않는 몸이 될 수 있어 받지 않는다
            maxHealth = maxValue;
            SetCurrent(currentValue);
        }

        // 경직/화면 흔들림 없이 체력만 깎는다 (광폭화의 지속 체력 감소 같은 자해 효과용).
        public void Drain(float amount)
        {
            if (!(amount > 0f) || IsDead) return;
            SetCurrent(current - amount);
        }

        // 지속 피해 (화상): Drain처럼 경직·Damaged 없이 깎지만, 무적 중이면 무시하고 쓰러질 피해면 버티기(DeathSaves)를 쓴다.
        // Drain을 쓰던 때는 화상이 대쉬·순간이동 무적, 무적 치트, 불사조 깃털·생명 20을 뚫고 쓰러뜨렸다 (버그 점검 2026-11-22). 깎았으면 true.
        public bool TakeDotDamage(float amount)
        {
            if (!(amount > 0f) || IsDead || IsInvulnerable) return false;
            if (current - amount <= 0f && DeathSaves > 0)
            {
                DeathSaves--;
                SetCurrent(maxHealth * DeathSaveRatio);
                GrantGrace(DotDeathSaveGrace); // 화상 다음 틱이 바로 버티기를 또 쓰거나 쓰러뜨리지 않게 (TakeDamage 쪽은 경직 무적이 있다)
                DeathPrevented?.Invoke();
                return true;
            }
            SetCurrent(current - amount);
            return true;
        }

        // 쓰러진 뒤에는 회복하지 않는다 (체력이 0에서 올라가면 IsDead가 풀려 다시 사는 것처럼 보인다, 버그 점검 2026-11-22).
        public void Heal(float amount)
        {
            if (!(amount > 0f) || IsDead) return;
            SetCurrent(current + amount);
        }

        private void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 0f, maxHealth);
            HealthChanged?.Invoke(current, maxHealth);
        }
    }
}
