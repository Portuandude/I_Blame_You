using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    public static class EnemySpawner
    {
        private const float VisualScale = 0.6f;

        // 슬라임 프레임 원본 크기(258x256, PPU 100), 피벗이 좌하단(0,0)이라 시각 오브젝트를 절반만큼 옮겨서 가운데를 맞춘다.
        private const float SpriteWidth = 2.58f;
        private const float SpriteHeight = 1.53f;
        private const float SlimeMaxHealth = 30f;

        public static EnemyController SpawnSlime(Vector3 position, LevelArtConfig artConfig = null)
        {
            var go = new GameObject("Slime");
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;

            float colliderWidth = SpriteWidth * VisualScale * 0.6f;
            float colliderHeight = SpriteHeight * VisualScale * 0.8f;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(colliderWidth, colliderHeight);
            collider.offset = new Vector2(0f, colliderHeight / 2f);
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(VisualScale, VisualScale, 1f);
            visual.transform.localPosition = new Vector3(-SpriteWidth / 2f, 0f, 0f);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            if (artConfig != null && artConfig.slimeDefaultSprite != null)
            {
                renderer.sprite = artConfig.slimeDefaultSprite;
            }
            else
            {
                renderer.sprite = SolidSpriteFactory.CreateSquare();
                renderer.color = new Color(0.6f, 0.2f, 0.7f);
            }

            if (artConfig != null && artConfig.slimeAnimatorController != null)
            {
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = artConfig.slimeAnimatorController;
            }

            var health = go.AddComponent<HealthSystem>();
            health.ConfigureMaxHealth(SlimeMaxHealth);

            var controller = go.AddComponent<EnemyController>();
            int groundLayerIndex = LayerMask.NameToLayer("Ground");
            LayerMask groundLayer = groundLayerIndex >= 0 ? (LayerMask)(1 << groundLayerIndex) : (LayerMask)1;
            controller.ConfigureObstacleLayer(groundLayer);

            return controller;
        }
    }
}
