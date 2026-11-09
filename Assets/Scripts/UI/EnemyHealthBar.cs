using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 적 머리 위 작은 체력 바. 처음 피해를 받을 때 생기고, 마지막 피해 뒤 VisibleTime 동안 보였다가 서서히 사라진다.
    // 다시 맞으면 다시 보인다. 적이 쓰러지거나 사라지면 함께 없어진다. 보스는 화면 위 보스 체력바를 쓰므로 붙이지 않는다.
    // StatusBarsUI처럼 자식이 아니라 위치만 따라가서, 적이 좌우로 뒤집혀도 바는 정자세다.
    public class EnemyHealthBar : MonoBehaviour
    {
        private const float VisibleTime = 3f;
        private const float FadeTime = 0.6f;
        private const float Height = 0.12f;
        private const float MinWidth = 0.8f;
        private const float MaxWidth = 1.8f;
        private const float Gap = 0.25f; // 몸 윗면에서 이만큼 위

        private HealthSystem health;
        private Collider2D body;
        private float width;
        private float lastHitTime;
        private Transform fill;
        private SpriteRenderer backRenderer;
        private SpriteRenderer fillRenderer;
        private Color backColor;
        private Color fillColor;

        // 적이 피해를 받을 때 부른다: 바가 없으면 만들고, 있으면 다시 보이게 한다.
        public static EnemyHealthBar Ping(EnemyHealthBar existing, HealthSystem health, Collider2D body)
        {
            if (health == null) return existing;
            if (existing == null)
            {
                existing = new GameObject("EnemyHealthBar").AddComponent<EnemyHealthBar>();
                existing.Build(health, body);
            }

            existing.lastHitTime = Time.time;
            existing.Refresh(health.Current, health.Max);
            return existing;
        }

        private void Build(HealthSystem target, Collider2D targetBody)
        {
            health = target;
            body = targetBody;
            width = body != null ? Mathf.Clamp(body.bounds.size.x * 1.1f, MinWidth, MaxWidth) : 1f;

            backColor = new Color(0.1f, 0.02f, 0.02f, 0.85f);
            fillColor = new Color(0.9f, 0.2f, 0.2f);

            var back = new GameObject("Background");
            back.transform.SetParent(transform, false);
            back.transform.localScale = new Vector3(width + 0.06f, Height + 0.06f, 1f);
            backRenderer = back.AddComponent<SpriteRenderer>();
            backRenderer.sprite = SolidSpriteFactory.CreateSquare();
            backRenderer.sortingOrder = 8; // 플레이어 상태 바(10/11)보다 아래

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.localPosition = new Vector3(-width / 2f, 0f, 0f);
            fillObject.transform.localScale = new Vector3(width, Height, 1f);
            fillRenderer = fillObject.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = SolidSpriteFactory.CreateLeftPivotSquare();
            fillRenderer.sortingOrder = 9;
            fill = fillObject.transform;

            health.HealthChanged += Refresh;
            Follow();
        }

        private void Refresh(float current, float max)
        {
            if (fill == null) return;
            var scale = fill.localScale;
            scale.x = width * Mathf.Clamp01(max > 0f ? current / max : 0f);
            fill.localScale = scale;
        }

        private void LateUpdate()
        {
            if (health == null || health.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            Follow();
            float since = Time.time - lastHitTime;
            float alpha = since <= VisibleTime ? 1f : Mathf.Clamp01(1f - (since - VisibleTime) / FadeTime);
            backRenderer.color = new Color(backColor.r, backColor.g, backColor.b, backColor.a * alpha);
            fillRenderer.color = new Color(fillColor.r, fillColor.g, fillColor.b, alpha);
        }

        private void Follow()
        {
            // 콜라이더가 꺼졌거나 물리가 멈춘 동안(슬라임 합체 등)은 bounds가 갱신되지 않으므로 몸 위치를 쓴다.
            bool useBounds = body != null && body.enabled && (body.attachedRigidbody == null || body.attachedRigidbody.simulated);
            float top = useBounds ? body.bounds.max.y : health.transform.position.y + 1f;
            float x = useBounds ? body.bounds.center.x : health.transform.position.x;
            transform.position = new Vector3(x, top + Gap, 0f);
        }

        private void OnDestroy()
        {
            if (health != null) health.HealthChanged -= Refresh;
        }
    }
}
