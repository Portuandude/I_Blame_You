using UnityEngine;

namespace IBlameYou.Systems
{
    // 피격 이펙트: 실제로 피해를 입으면 몸 스프라이트가 0.08초 동안 하얗게 번쩍이고, 작은 파편이 튄다.
    // HitStun이 Awake에서 자동으로 붙인다 (플레이어/적/보스 공통, 프리팹 재생성 불필요).
    // 번쩍임은 색(tint)을 건드리지 않고 머티리얼만 잠깐 바꿔 끼우므로, 속성 재색칠·사망 회색 연출과 겹치지 않는다.
    [RequireComponent(typeof(HealthSystem))]
    public class HitFlash : MonoBehaviour
    {
        public const float FlashDuration = 0.08f;
        private const int DebrisCount = 5;

        private static Material flashMaterial;
        private static bool flashMaterialLoaded;

        private HealthSystem health;
        private SpriteRenderer body;
        private Material originalMaterial;
        private float flashRemaining;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        // Resources/Shaders/SpriteFlash.shader. 셰이더를 못 찾거나 지원하지 않으면 null (번쩍임 없이 파편만).
        private static Material FlashMaterial
        {
            get
            {
                if (flashMaterialLoaded) return flashMaterial;
                flashMaterialLoaded = true;
                var shader = Shader.Find("IBlameYou/SpriteFlash");
                if (shader != null && shader.isSupported) flashMaterial = new Material(shader) { name = "SpriteFlash" };
                return flashMaterial;
            }
        }

        private SpriteRenderer Body
        {
            get
            {
                if (body != null) return body;
                var visual = transform.Find("Visual");
                // 몸 그림은 스포너가 만드는 "Visual" 자식이다. 없으면 머리 위 아이콘 등을 잘못 번쩍이지 않게 번쩍임을 건너뛴다.
                body = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                return body;
            }
        }

        private void OnDamaged(float amount)
        {
            var renderer = Body;
            if (renderer != null && FlashMaterial != null)
            {
                if (flashRemaining <= 0f) originalMaterial = renderer.sharedMaterial;
                renderer.sharedMaterial = FlashMaterial;
                flashRemaining = FlashDuration;
            }

            HitDebris.Burst(HitPoint(), DebrisCount);
        }

        private Vector3 HitPoint()
        {
            var collider = GetComponent<Collider2D>();
            return collider != null ? collider.bounds.center : transform.position + Vector3.up * 0.5f;
        }

        private void Update()
        {
            if (flashRemaining <= 0f) return;

            flashRemaining -= Time.unscaledDeltaTime; // 슬로모션(사망 연출) 중에도 0.08초 실제 시간
            if (flashRemaining <= 0f) Restore();
        }

        private void Restore()
        {
            flashRemaining = 0f;
            if (body != null && body.sharedMaterial == flashMaterial) body.sharedMaterial = originalMaterial;
        }

        private void OnDisable()
        {
            if (flashRemaining > 0f) Restore();
        }
    }
}
