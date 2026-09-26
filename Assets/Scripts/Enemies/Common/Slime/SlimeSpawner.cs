using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 구성의 단일 출처. Spawn은 프리팹이 있으면 인스턴스화하고 없으면 Build로 즉석 조립하며,
    // 에디터 툴(PrefabSetup)은 Build로 Slime.prefab을 굽는다.
    public static class SlimeSpawner
    {
        private const float VisualScale = 0.6f;

        private const float MaxHealth = 30f;
        private const float StunDuration = 0.3f;
        private const float ContactDamage = 8f;
        private const float ContactDamageCooldown = 1f;

        // 행동: 플레이어가 DetectionRange 안이면 추적, 아니면 IdleBehaviour대로(짧게 왕복하거나 가만히 서 있기).
        private const SlimeController.IdleBehaviour Idle = SlimeController.IdleBehaviour.Patrol;
        private const float PatrolSpeed = 1.5f;
        private const float PatrolRadius = 3f;
        private const float ChaseSpeed = 2.5f;
        private const float DetectionRange = 8f;

        // 공격(도약 덮치기): 사거리 안이면 웅크렸다가 플레이어 쪽으로 뛰어 덮친다.
        private const float AttackRange = 4f;
        private const float AttackDamage = 12f;
        private const float AttackCooldown = 2f;
        private const float MaxLeapSpeed = 8f;

        public static SlimeController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            // 재생성 전의 구버전 프리팹(스프라이트 콜라이더가 없거나, 피벗 변경 전의 비주얼 오프셋)은
            // 쓰지 않고 코드로 조립한다.
            var prefab = artConfig != null ? artConfig.slimePrefab : null;
            if (prefab != null && !IsCurrentPrefab(prefab)) prefab = null;
            return EnemySpawner.Spawn<SlimeController>(prefab, () => Build(artConfig), position, "Slime");
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = EnemySpawner.CreateBody("Slime");

            // 콜라이더는 현재 스프라이트 프레임의 윤곽을 따라간다 (SpriteColliderFitter).
            var collider = go.AddComponent<PolygonCollider2D>();
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            // 모든 슬라임 스프라이트의 피벗은 하단 중앙(GameAssetSetup이 임포트 설정으로 맞춤)이라 오프셋이 필요 없다.
            var visual = EnemySpawner.AddVisual(
                go,
                artConfig != null ? artConfig.slimeDefaultSprite : null,
                artConfig != null ? artConfig.slimeAnimatorController : null,
                VisualScale,
                Vector3.zero,
                new Color(0.6f, 0.2f, 0.7f));

            go.AddComponent<SpriteColliderFitter>().Configure(visual.GetComponent<SpriteRenderer>(), collider);

            EnemySpawner.AddHealthAndStun(go, MaxHealth, StunDuration);

            var controller = go.AddComponent<SlimeController>();
            controller.ConfigureContactDamage(ContactDamage, ContactDamageCooldown);
            controller.Configure(Idle, PatrolSpeed, PatrolRadius, ChaseSpeed, DetectionRange);
            controller.ConfigureAttack(AttackRange, AttackDamage, AttackCooldown, MaxLeapSpeed);
            controller.ConfigureObstacleLayer(EnemySpawner.GroundMask());

            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }

        private static bool IsCurrentPrefab(GameObject prefab)
        {
            if (prefab.GetComponent<SpriteColliderFitter>() == null) return false;

            var visual = prefab.transform.Find("Visual");
            return visual != null && Mathf.Abs(visual.localPosition.x) < 0.001f;
        }
    }
}
