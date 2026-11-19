using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 프리즘 젤리 한 몸 (Docs/MidBossDraft.md 후보 D). 규칙 수치는 PrismJellyRules (EditMode 테스트).
    // - 몸 색 = 몸 속성 (불/물/바람/땅). PrismJellyRules.ShiftInterval초마다 지금과 다른 색으로 바뀌고, WarnSeconds초 전부터
    //   지금 색과 다음 색 사이로 빠르게 깜빡여 예고한다.
    // - 같은 색 마법 피해 50% (IDamageModifier). 이기는 상성 1.5배·지는 상성 0.5배는 몸 속성 상성(DamageCalculator)이 준다.
    // - 도약 덮치기 공격 속성도 지금 색. 보스 체력바 아래 줄에 지금 색과 약점 속성을 띄운다 (P2가 마법진 속성을 고르는 근거).
    [RequireComponent(typeof(SlimeController))]
    public class PrismJelly : MonoBehaviour, IDamageModifier
    {
        public const string DisplayName = "프리즘 젤리";
        private const int ShardCount = 6;

        private HealthSystem health;
        private CombatStats stats;
        private SlimeController slime;
        private ElementRecolor recolor;
        private SpriteRenderer body;
        private float elementalDamage;
        private Element color;
        private Element nextColor;
        private float nextShiftAt;
        private bool started;
        private bool fallen;

        private static PrismJelly noticeOwner;

        public Element CurrentColor => color;

        public void Configure(ElementRecolor bodyRecolor, SpriteRenderer bodyRenderer)
        {
            recolor = bodyRecolor;
            body = bodyRenderer;
            ApplyTint(ElementInfo.Color(color));
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            stats = GetComponent<CombatStats>();
            slime = GetComponent<SlimeController>();
            if (stats != null)
            {
                // 표의 무속성 데미지를 색 속성으로 옮겨 쓴다 (색이 바뀔 때마다 그 칸으로)
                var block = stats.Stats;
                for (int i = 0; i < block.elementDamage.Length; i++)
                {
                    elementalDamage = Mathf.Max(elementalDamage, block.elementDamage[i]);
                    block.elementDamage[i] = 0f;
                }
            }
            SetColor(PrismJellyRules.FirstColor(Random.value), false);
            if (health != null) health.HealthChanged += OnHealthChanged;
        }

        private void Start()
        {
            started = true;
            nextShiftAt = Time.time + PrismJellyRules.ShiftInterval;
            nextColor = PrismJellyRules.NextColor(color, Random.value);
            BossEvents.Register(DisplayName, health);
            StageEvents.PostNotice($"중간보스: {DisplayName}");
            UpdateNotice();
        }

        public float Modify(float damage, DamageKind kind, Element element)
        {
            if (kind != DamageKind.Magic) return damage;
            return damage * PrismJellyRules.ColorMultiplier(element, color);
        }

        private void Update()
        {
            if (fallen || !started) return;

            if (Time.time >= nextShiftAt)
            {
                SetColor(nextColor, true);
                nextShiftAt = Time.time + PrismJellyRules.ShiftInterval;
                nextColor = PrismJellyRules.NextColor(color, Random.value);
                UpdateNotice();
            }

            var tint = ElementInfo.Color(color);
            if (nextShiftAt - Time.time <= PrismJellyRules.WarnSeconds)
                tint = Color.Lerp(tint, ElementInfo.Color(nextColor), 0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
            ApplyTint(tint);
        }

        private void SetColor(Element element, bool announce)
        {
            color = element;
            if (stats != null)
            {
                var block = stats.Stats;
                for (int i = 0; i < block.elementDamage.Length; i++) block.elementDamage[i] = 0f;
                block.elementDamage[(int)element] = elementalDamage;
                stats.SetBodyElement(element);
            }
            if (slime != null) slime.SetAttackElement(element);
            ApplyTint(ElementInfo.Color(element));
            if (!announce) return;

            // 색이 바뀌는 순간: 반짝 파편 + 머리 위 글자 (그림 이펙트는 다음 단계에서 pixelfx로 바꾼다)
            var center = transform.position + Vector3.up * 0.8f;
            DamageNumbers.ShowText(transform.position + Vector3.up * 1.8f, $"{ElementInfo.KoreanName(element)}색!", ElementInfo.Color(element), 0.9f);
            for (int i = 0; i < ShardCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ShardCount;
                KnightEnrageFx.Puff(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.6f, ElementInfo.Color(element), 0.7f);
            }
            GameAudio.PlaySfx("KnightLink", 0.6f, 0.1f);
        }

        private void ApplyTint(Color tint)
        {
            if (recolor != null) recolor.Tint = tint;
            else if (body != null) body.color = tint; // 아트가 없을 때의 단색 사각형
        }

        // 보스 체력바 아래 줄: 지금 색과 약점 (그 색을 이기는 속성)
        private void UpdateNotice()
        {
            if (noticeOwner != null && noticeOwner != this) return;
            noticeOwner = this;
            BossEvents.Notice = $"{ElementInfo.KoreanName(color)}색 · 약점 {ElementInfo.KoreanName(WeaknessOf(color))} 마법 · 같은 색은 반감";
        }

        public static Element WeaknessOf(Element target)
        {
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
                if (ElementAffinity.Beats(e) == target) return e;
            return Element.Neutral;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current > 0f || fallen) return;
            fallen = true;
            ApplyTint(Color.white);
            ReleaseNotice();
            BossDefeatFx.Play(transform.position + Vector3.up * 0.6f, DisplayName);
        }

        private void ReleaseNotice()
        {
            if (noticeOwner != this) return;
            noticeOwner = null;
            BossEvents.Notice = null;
        }

        private void OnDestroy()
        {
            ReleaseNotice();
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                BossEvents.Unregister(health);
            }
        }
    }
}
