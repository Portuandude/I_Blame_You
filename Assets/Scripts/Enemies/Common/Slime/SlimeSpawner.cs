using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 구성의 단일 출처. Spawn은 프리팹이 있으면 인스턴스화하고 없으면 Build로 즉석 조립하며,
    // 에디터 툴(PrefabSetup)은 Build로 Slime.prefab을 굽는다. 어미 슬라임(정예)도 같은 아트로 BuildVariant를 쓴다.
    public static class SlimeSpawner
    {
        private const float VisualScale = 0.6f;

        private const float StunDuration = 0.3f;
        private const float ContactDamageMultiplier = 0.3f;
        private const float ContactDamageCooldown = 1f;

        // 행동: 플레이어가 DetectionRange 안이면 추적, 아니면 IdleBehaviour대로(짧게 왕복하거나 가만히 서 있기).
        // 이동속도는 스탯 문서(슬라임 0.3 = 0.6유닛/초)로 정하고, 순찰은 그보다 느리게.
        private const SlimeController.IdleBehaviour Idle = SlimeController.IdleBehaviour.Patrol;
        private const float PatrolSpeedRatio = 0.6f;
        private const float PatrolRadius = 3f;
        private const float DetectionRange = 8f;

        // 공격(도약 덮치기): 사거리 안이면 웅크렸다가 플레이어 쪽으로 뛰어 덮친다. 재사용 대기는 공격 속도로 정한다.
        private const float AttackRange = 4f;
        private const float MaxLeapSpeed = 8f;

        public static SlimeController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            // 재생성 전의 구버전 프리팹(스프라이트 콜라이더/스탯이 없거나, 피벗 변경 전의 비주얼 오프셋)은
            // 쓰지 않고 코드로 조립한다.
            var prefab = artConfig != null ? artConfig.slimePrefab : null;
            if (prefab != null && !IsCurrentPrefab(prefab)) prefab = null;
            return EnemySpawner.Spawn<SlimeController>(prefab, () => Build(artConfig), position, "Slime");
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            return BuildVariant(artConfig, "Slime", StatTable.Slime, VisualScale, Color.white, AttackRange, MaxLeapSpeed);
        }

        // 슬라임 아트를 쓰는 변종(어미 슬라임 등)을 조립한다. tint는 스프라이트에 곱할 색.
        public static GameObject BuildVariant(LevelArtConfig artConfig, string name, StatBlock stats, float visualScale, Color tint,
            float attackRange, float maxLeapSpeed)
        {
            var go = EnemySpawner.CreateBody(name);

            // 콜라이더는 현재 스프라이트 프레임의 윤곽을 따라간다 (SpriteColliderFitter).
            var collider = go.AddComponent<PolygonCollider2D>();
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            // 모든 슬라임 스프라이트의 피벗은 하단 중앙(GameAssetSetup이 임포트 설정으로 맞춤)이라 오프셋이 필요 없다.
            var visual = EnemySpawner.AddVisual(
                go,
                artConfig != null ? artConfig.slimeDefaultSprite : null,
                artConfig != null ? artConfig.slimeAnimatorController : null,
                visualScale,
                Vector3.zero,
                new Color(0.6f, 0.2f, 0.7f));
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (artConfig != null && artConfig.slimeDefaultSprite != null) renderer.color = tint;

            go.AddComponent<SpriteColliderFitter>().Configure(renderer, collider);

            EnemySpawner.AddStats(go, stats, StunDuration);

            var controller = go.AddComponent<SlimeController>();
            float chaseSpeed = StatUnits.MoveSpeedToUnits(stats.moveSpeed);
            controller.ConfigureContactDamage(ContactDamageMultiplier, ContactDamageCooldown);
            controller.Configure(Idle, chaseSpeed * PatrolSpeedRatio, PatrolRadius, chaseSpeed, DetectionRange);
            controller.ConfigureAttack(attackRange, StatUnits.AttackInterval(stats.attackSpeed), maxLeapSpeed);
            controller.ConfigureObstacleLayer(EnemySpawner.GroundMask());

            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }

        private static bool IsCurrentPrefab(GameObject prefab)
        {
            if (prefab.GetComponent<SpriteColliderFitter>() == null || prefab.GetComponent<CombatStats>() == null) return false;

            var visual = prefab.transform.Find("Visual");
            return visual != null && Mathf.Abs(visual.localPosition.x) < 0.001f;
        }
    }
}
