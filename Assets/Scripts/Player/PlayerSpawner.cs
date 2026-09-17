using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 아직 손으로 만든 플레이어 프리팹이 없는 상태에서, 빠르게 이동/점프/달리기를 테스트해보기 위해
    // 필요한 컴포넌트를 전부 코드로 붙여 플레이어를 즉석에서 생성한다.
    public static class PlayerSpawner
    {
        // 캐릭터 프레임(300x256, PPU 100)에는 여백이 많이 포함돼 있어, 시각적으로 적당한 크기가
        // 되도록 균일하게 축소한다. 콜라이더 크기와는 별개.
        private const float VisualScale = 0.7f;

        public static PlayerMovement Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            var go = new GameObject("Player");
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 1.6f);
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

            var movement = go.AddComponent<PlayerMovement>();
            int groundLayerIndex = LayerMask.NameToLayer("Ground");
            LayerMask groundLayer = groundLayerIndex >= 0 ? (LayerMask)(1 << groundLayerIndex) : (LayerMask)1;
            movement.ConfigureGroundCheck(groundCheck.transform, groundLayer);

            return movement;
        }
    }
}
