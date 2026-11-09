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
        private const float Height = 0.14f;
        private const float MinWidth = 0.8f;
        private const float MaxWidth = 1.8f;
        private const float Gap = 0.25f; // 몸 윗면에서 이만큼 위

        private HealthSystem health;
        private Collider2D body;
        private float width;
        private float lastHitTime;
        private CapsuleBar bar;

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

            // 둥근 캡슐 + 흰 잔상 (CapsuleBar). 플레이어 상태 바(정렬 10~12)보다 아래(7~9)
            bar = new CapsuleBar(transform, "Bar", Vector3.zero, width, Height,
                new Color(0.1f, 0.02f, 0.02f, 0.85f), new Color(0.95f, 0.25f, 0.3f), 7);
            bar.Tick(Time.time, 0f); // 잔상을 가득 찬 상태로 시작해, 바를 만든 첫 피해도 흰 잔상으로 보이게

            health.HealthChanged += Refresh;
            Follow();
        }

        private void Refresh(float current, float max)
        {
            if (bar == null) return;
            bar.SetRatio(max > 0f ? current / max : 0f);
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
            bar.SetAlpha(alpha);
            bar.Tick(Time.time, Time.deltaTime);
        }

        private float topOffset = 1f; // 마지막으로 유효했던 bounds 기준 높이 (물리가 멈춘 동안 바가 튀지 않게)
        private float xOffset;

        private void Follow()
        {
            // 콜라이더가 꺼졌거나 물리가 멈춘 동안(슬라임 합체 등)은 bounds가 갱신되지 않으므로 몸 위치를 쓴다.
            bool useBounds = body != null && body.enabled && (body.attachedRigidbody == null || body.attachedRigidbody.simulated);
            if (useBounds)
            {
                topOffset = body.bounds.max.y - health.transform.position.y;
                xOffset = body.bounds.center.x - health.transform.position.x;
            }
            float top = health.transform.position.y + topOffset;
            float x = health.transform.position.x + xOffset;
            transform.position = new Vector3(x, top + Gap, 0f);
        }

        private void OnDestroy()
        {
            if (health != null) health.HealthChanged -= Refresh;
        }
    }
}
