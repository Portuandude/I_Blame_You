using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 구성의 단일 출처. Spawn은 프리팹이 있으면 인스턴스화하고 없으면 Build로 즉석 조립하며,
    // 에디터 툴(PrefabSetup)은 Build로 Slime.prefab을 굽는다.
    public static class SlimeSpawner
    {
        private const float VisualScale = 0.6f;

        // 슬라임 프레임 원본 크기(258x153, PPU 100), 피벗이 좌하단(0,0)이라 시각 오브젝트를 절반만큼 옮겨서 가운데를 맞춘다.
        private const float SpriteWidth = 2.58f;

        private const float MaxHealth = 30f;
        private const float StunDuration = 0.3f;
        private const float ContactDamage = 8f;
        private const float ContactDamageCooldown = 1f;

        private static readonly Vector2 ColliderOffset = new Vector2(-1.27f, -0.09f);
        private const float ColliderRadius = 0.27f;

        public static SlimeController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            var prefab = artConfig != null ? artConfig.slimePrefab : null;
            return EnemySpawner.Spawn<SlimeController>(prefab, () => Build(artConfig), position, "Slime");
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = EnemySpawner.CreateBody("Slime");

            var collider = go.AddComponent<CircleCollider2D>();
            collider.offset = ColliderOffset;
            collider.radius = ColliderRadius;
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            EnemySpawner.AddVisual(
                go,
                artConfig != null ? artConfig.slimeDefaultSprite : null,
                artConfig != null ? artConfig.slimeAnimatorController : null,
                VisualScale,
                new Vector3(-SpriteWidth / 2f, 0f, 0f),
                new Color(0.6f, 0.2f, 0.7f));

            EnemySpawner.AddHealthAndStun(go, MaxHealth, StunDuration);

            var controller = go.AddComponent<SlimeController>();
            controller.ConfigureContactDamage(ContactDamage, ContactDamageCooldown);
            controller.ConfigureObstacleLayer(EnemySpawner.GroundMask());

            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }
    }
}
