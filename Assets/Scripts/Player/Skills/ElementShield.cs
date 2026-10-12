using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 A의 속성 방어막 효과 (기획서 2.5 가안):
    // 켜져 있는 동안 같은 속성의 마법 피해는 무효, 그 외 피해는 30% 감소.
    public class ElementShield : MonoBehaviour, IDamageModifier
    {
        private const float OtherDamageMultiplier = 0.7f;

        private SpriteRenderer bubble;
        private float remaining;

        public bool Active => remaining > 0f;
        public Element ActiveElement { get; private set; }
        public float Remaining => remaining;

        public void Activate(Element element, float duration)
        {
            ActiveElement = element;
            remaining = duration;

            if (bubble == null)
            {
                bubble = EffectLibrary.CreateVisual(transform, "ShieldBubble", EffectLibrary.ShieldFrames, 10f, true,
                    Color.white, 1.8f, sortingOrder: 3);
                bubble.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            }

            var color = ElementInfo.Color(element);
            color.a = 0.6f;
            bubble.color = color;
            bubble.gameObject.SetActive(true);
        }

        public float Modify(float damage, DamageKind kind, Element element)
        {
            if (!Active) return damage;
            if (kind == DamageKind.Magic && element == ActiveElement) return 0f;
            return damage * OtherDamageMultiplier;
        }

        private void Update()
        {
            if (remaining <= 0f) return;

            remaining -= Time.deltaTime;
            if (bubble != null)
            {
                // 끝나기 직전에는 깜빡여서 곧 꺼진다는 걸 알린다.
                bool visible = remaining > 0.6f || Mathf.Repeat(remaining, 0.2f) > 0.1f;
                bubble.enabled = visible;
                if (remaining <= 0f) bubble.gameObject.SetActive(false);
            }
        }
    }
}
