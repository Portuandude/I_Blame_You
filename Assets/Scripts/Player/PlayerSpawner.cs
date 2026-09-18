using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Player
{
    // Spawn: LevelArtConfig에 플레이어 프리팹이 있으면 그걸 인스턴스화하고, 없으면 Build로 즉석 조립한다.
    // Build: 플레이어의 컴포넌트 구성의 단일 출처. 에디터 툴(PrefabSetup)이 이걸로 Player.prefab을 굽는다.
    public static class PlayerSpawner
    {
        // 캐릭터 프레임(300x256, PPU 100)에는 여백이 많이 포함돼 있어, 시각적으로 적당한 크기가
        // 되도록 균일하게 축소한다. 콜라이더 크기와는 별개.
        private const float VisualScale = 0.7f;

        public static PlayerMovement Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            GameObject go;
            if (artConfig != null && artConfig.playerPrefab != null)
            {
                go = Object.Instantiate(artConfig.playerPrefab, position, Quaternion.identity);
            }
            else
            {
                go = Build(artConfig);
                go.transform.position = position;
            }

            go.name = "Player";

            // 상태 바는 플레이어 자식이 아니라 위치만 따라가는 별도 오브젝트라 프리팹 밖에서 붙인다.
            var statusBars = new GameObject("StatusBars_Player").AddComponent<StatusBarsUI>();
            statusBars.Initialize(go.transform, go.GetComponent<HealthSystem>(), go.GetComponent<StaminaSystem>(), go.GetComponent<ManaSystem>());

            return go.GetComponent<PlayerMovement>();
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = new GameObject("Player");

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.offset = new Vector2(-0.03f, 0.06f);
            collider.size = new Vector2(0.68f, 1.08f);
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1; // 바닥/플랫폼(기본 0)보다 항상 앞에 그려지도록.
            if (artConfig != null && artConfig.playerDefaultSprite != null)
            {
                visual.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
                renderer.sprite = artConfig.playerDefaultSprite;
            }
            else
            {
                visual.transform.localScale = new Vector3(0.8f, 1.6f, 1f);
                renderer.sprite = SolidSpriteFactory.CreateSquare();
                renderer.color = new Color(0.2f, 0.8f, 0.4f);
            }

            if (artConfig != null && artConfig.playerAnimatorController != null)
            {
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = artConfig.playerAnimatorController;
            }

            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(go.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, -0.85f, 0f);

            var movement = go.AddComponent<PlayerMovement>(); // RequireComponent로 Stamina/Health도 함께 붙는다.
            int groundLayerIndex = LayerMask.NameToLayer("Ground");
            LayerMask groundLayer = groundLayerIndex >= 0 ? (LayerMask)(1 << groundLayerIndex) : (LayerMask)1;
            movement.ConfigureGroundCheck(groundCheck.transform, groundLayer);

            go.AddComponent<ManaSystem>();
            go.AddComponent<PlayerCombat>();

            return go;
        }
    }
}
