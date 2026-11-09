using UnityEngine;

namespace IBlameYou.Systems
{
    // 적 경직 연출 (GDD 로드맵 "경직 애니메이션"): 경직 동안 머리 위에서 별 3개가 빙글빙글 돈다 (어질어질).
    // 그림은 Resources/Effects/StunStars (Tools/ArtGen/generate_skill_fx.py, 64x32 반복 6프레임). 그림이 없으면 아무것도 하지 않는다.
    // HitStun의 뒤로 젖히기(Visual 회전)와 함께 보인다. 경직이 아주 짧아도 MinVisible 동안은 보여 주고 끝에 흐려진다.
    // 몸의 자식이 아니라 위치만 따라가서 좌우 반전·젖히기 회전을 타지 않는다. 체력 바(몸 위 0.25)보다 위에 띄운다.
    // 슈퍼아머(HitStun.Immune) 보스는 경직에 걸리지 않으므로 별도 처리 없이 안 나온다.
    [RequireComponent(typeof(HitStun))]
    public class StunStars : MonoBehaviour
    {
        private const float FramesPerSecond = 14f;
        private const float MinVisible = 0.45f;
        private const float FadeTime = 0.12f;
        private const float HeadGap = 0.55f;      // 몸 윗면에서 별 중심까지
        private const float ArtWidth = 0.64f;     // 64px / 100ppu
        private const int SortingOrder = 9;

        private static Sprite[] frames;
        private static bool framesLoaded;

        private HitStun hitStun;
        private HealthSystem health;
        private Collider2D body;
        private SpriteRenderer stars;
        private bool wasStunned;
        private float shownAt = -10f;
        private float hideAt = -10f;
        private float topOffset = 0.5f;

        private void Awake()
        {
            hitStun = GetComponent<HitStun>();
            health = GetComponent<HealthSystem>();
            body = GetComponent<Collider2D>();
            if (!framesLoaded)
            {
                framesLoaded = true;
                frames = EffectLibrary.LoadRuntimeFrames("Effects/StunStars");
            }
        }

        private void LateUpdate()
        {
            if (frames == null || frames.Length == 0) return;

            bool dead = health != null && health.IsDead;
            bool stunned = hitStun.IsStunned && !dead;
            if (stunned && !wasStunned) shownAt = Time.time;
            wasStunned = stunned;
            if (stunned) hideAt = Mathf.Max(Time.time, shownAt + MinVisible);

            float alpha = dead ? 0f : Mathf.Clamp01((hideAt + FadeTime - Time.time) / FadeTime);
            if (alpha <= 0f)
            {
                if (stars != null) stars.enabled = false;
                return;
            }

            if (stars == null) Build();
            stars.enabled = true;
            stars.color = new Color(1f, 1f, 1f, alpha);
            stars.sprite = frames[(int)((Time.time - shownAt) * FramesPerSecond) % frames.Length];
            Follow();
        }

        private void Build()
        {
            var go = new GameObject("StunStars");
            stars = go.AddComponent<SpriteRenderer>();
            stars.sortingOrder = SortingOrder;
            float width = body != null ? body.bounds.size.x : ArtWidth;
            float scale = Mathf.Clamp(width * 0.9f / ArtWidth, 0.8f, 1.6f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Follow()
        {
            // 콜라이더가 꺼졌거나 물리가 멈춘 동안은 bounds가 갱신되지 않으므로 마지막 높이를 쓴다 (EnemyHealthBar와 같은 방식).
            if (body != null && body.enabled && (body.attachedRigidbody == null || body.attachedRigidbody.simulated))
                topOffset = body.bounds.max.y - transform.position.y;
            float x = body != null ? body.bounds.center.x : transform.position.x;
            stars.transform.position = new Vector3(x, transform.position.y + topOffset + HeadGap, 0f);
        }

        private void OnDisable()
        {
            if (stars != null) stars.enabled = false;
        }

        private void OnDestroy()
        {
            if (stars != null) Destroy(stars.gameObject);
        }
    }
}
