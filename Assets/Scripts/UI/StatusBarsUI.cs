using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 캐릭터 머리 위에 HP/기력/마나 바를 띄운다. 부모-자식 관계 대신 매 프레임 위치만
    // 따라가게 해서, 캐릭터가 좌우로 뒤집혀도(스케일 반전) 바는 항상 정자세를 유지한다.
    public class StatusBarsUI : MonoBehaviour
    {
        private const float BarWidth = 1.4f;
        private const float HealthBarHeight = 0.2f;
        private const float SubBarHeightRatio = 1f / 5f;
        private const float BarGap = 0.03f;

        [SerializeField] private Vector3 offset = new Vector3(0f, 1.4f, 0f);

        private Transform target;
        private HealthSystem health;
        private StaminaSystem stamina;
        private ManaSystem mana;

        private CapsuleBar healthBar;
        private CapsuleBar manaBar;
        private CapsuleBar staminaBar;

        public void Initialize(Transform followTarget, HealthSystem healthSystem, StaminaSystem staminaSystem, ManaSystem manaSystem)
        {
            target = followTarget;
            health = healthSystem;
            stamina = staminaSystem;
            mana = manaSystem;

            BuildBars();

            health.HealthChanged += OnHealthChanged;
            stamina.StaminaChanged += OnStaminaChanged;
            mana.ManaChanged += OnManaChanged;

            OnHealthChanged(health.Current, health.Max);
            OnStaminaChanged(stamina.Current, stamina.Max);
            OnManaChanged(mana.Current, mana.Max);

            transform.position = target.position + offset;
        }

        private void BuildBars()
        {
            float subBarHeight = HealthBarHeight * SubBarHeightRatio;

            float healthY = 0f;
            float manaY = healthY - (HealthBarHeight / 2f + BarGap + subBarHeight / 2f);
            float staminaY = manaY - (subBarHeight + BarGap);

            // 둥근 캡슐 + 흰 잔상 (CapsuleBar, HUD와 같은 그림). 색은 HUD 바와 맞춘다: 체력 분홍, 마나 하늘, 기력 풀색
            healthBar = new CapsuleBar(transform, "HealthBar", new Vector3(0f, healthY, 0f), BarWidth, HealthBarHeight,
                new Color(0.15f, 0.02f, 0.02f), new Color(1f, 0.37f, 0.59f), 10);
            manaBar = new CapsuleBar(transform, "ManaBar", new Vector3(0f, manaY, 0f), BarWidth, subBarHeight,
                new Color(0.02f, 0.03f, 0.15f), new Color(0.35f, 0.7f, 1f), 10);
            staminaBar = new CapsuleBar(transform, "StaminaBar", new Vector3(0f, staminaY, 0f), BarWidth, subBarHeight,
                new Color(0.03f, 0.1f, 0.03f), new Color(0.31f, 0.76f, 0.29f), 10);
        }

        private void OnHealthChanged(float current, float max) => healthBar.SetRatio(max > 0f ? current / max : 0f);
        private void OnStaminaChanged(float current, float max) => staminaBar.SetRatio(max > 0f ? current / max : 0f);
        private void OnManaChanged(float current, float max) => manaBar.SetRatio(max > 0f ? current / max : 0f);

        private void LateUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = target.position + offset;
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            healthBar.Tick(now, dt);
            manaBar.Tick(now, dt);
            staminaBar.Tick(now, dt);
        }

        private void OnDestroy()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
            if (stamina != null) stamina.StaminaChanged -= OnStaminaChanged;
            if (mana != null) mana.ManaChanged -= OnManaChanged;
        }
    }
}
