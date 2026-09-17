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

        private Transform healthFill;
        private Transform manaFill;
        private Transform staminaFill;

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

            healthFill = CreateBar("HealthBar", healthY, HealthBarHeight, new Color(0.15f, 0.02f, 0.02f), new Color(0.85f, 0.15f, 0.15f));
            manaFill = CreateBar("ManaBar", manaY, subBarHeight, new Color(0.02f, 0.03f, 0.15f), new Color(0.2f, 0.4f, 0.95f));
            staminaFill = CreateBar("StaminaBar", staminaY, subBarHeight, new Color(0.03f, 0.1f, 0.03f), new Color(0.3f, 0.85f, 0.3f));
        }

        private Transform CreateBar(string name, float centerY, float height, Color backgroundColor, Color fillColor)
        {
            var barRoot = new GameObject(name);
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = new Vector3(0f, centerY, 0f);

            var background = new GameObject("Background");
            background.transform.SetParent(barRoot.transform, false);
            background.transform.localScale = new Vector3(BarWidth, height, 1f);
            var backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = SolidSpriteFactory.CreateSquare();
            backgroundRenderer.color = backgroundColor;
            backgroundRenderer.sortingOrder = 10;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(barRoot.transform, false);
            fill.transform.localPosition = new Vector3(-BarWidth / 2f, 0f, 0f);
            fill.transform.localScale = new Vector3(BarWidth, height, 1f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = SolidSpriteFactory.CreateLeftPivotSquare();
            fillRenderer.color = fillColor;
            fillRenderer.sortingOrder = 11;

            return fill.transform;
        }

        private void OnHealthChanged(float current, float max) => SetFill(healthFill, current / max);
        private void OnStaminaChanged(float current, float max) => SetFill(staminaFill, current / max);
        private void OnManaChanged(float current, float max) => SetFill(manaFill, current / max);

        private static void SetFill(Transform fill, float ratio)
        {
            var scale = fill.localScale;
            scale.x = BarWidth * Mathf.Clamp01(ratio);
            fill.localScale = scale;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = target.position + offset;
        }

        private void OnDestroy()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
            if (stamina != null) stamina.StaminaChanged -= OnStaminaChanged;
            if (mana != null) mana.ManaChanged -= OnManaChanged;
        }
    }
}
