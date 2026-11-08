using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 근접 고블린류(고블린, 고블린 병사) 구성의 단일 출처. 정예 고블린 대전사도 BuildMelee를 쓴다.
    // 스프라이트는 하단 중앙 피벗이라 비주얼 오프셋 없이 몸체 원점이 발밑이다.
    public static class GoblinSpawner
    {
        private const float StunDuration = 0.3f;
        private const float ContactDamageMultiplier = 0.3f;
        private const float ContactDamageCooldown = 1f;

        public static GoblinController SpawnGoblin(Vector3 position, LevelArtConfig artConfig = null)
        {
            var prefab = artConfig != null ? artConfig.goblinPrefab : null;
            return EnemySpawner.Spawn<GoblinController>(prefab, () => BuildGoblin(artConfig), position, "Goblin");
        }

        public static GoblinController SpawnSoldier(Vector3 position, LevelArtConfig artConfig = null)
        {
            var prefab = artConfig != null ? artConfig.goblinSoldierPrefab : null;
            return EnemySpawner.Spawn<GoblinController>(prefab, () => BuildSoldier(artConfig), position, "GoblinSoldier");
        }

        // 고블린: 단검으로 짧게 찌른다.
        public static GameObject BuildGoblin(LevelArtConfig artConfig)
        {
            return BuildMelee(artConfig, "Goblin", StatTable.Goblin,
                artConfig != null ? artConfig.goblinDefaultSprite : null,
                artConfig != null ? artConfig.goblinAnimatorController : null,
                new Color(0.4f, 0.7f, 0.3f), new Vector2(0.5f, 0.85f),
                range: 1.1f, hitbox: new Vector2(1.0f, 0.8f), forward: 0.6f, height: 0.45f);
        }

        // 고블린 병사: 창으로 길게 찌른다.
        public static GameObject BuildSoldier(LevelArtConfig artConfig)
        {
            return BuildMelee(artConfig, "GoblinSoldier", StatTable.GoblinSoldier,
                artConfig != null ? artConfig.goblinSoldierDefaultSprite : null,
                artConfig != null ? artConfig.goblinSoldierAnimatorController : null,
                new Color(0.4f, 0.5f, 0.6f), new Vector2(0.55f, 0.95f),
                range: 1.7f, hitbox: new Vector2(1.6f, 0.6f), forward: 0.9f, height: 0.5f);
        }

        public static GameObject BuildMelee(LevelArtConfig artConfig, string name, StatBlock stats, Sprite sprite,
            RuntimeAnimatorController controller, Color fallbackColor, Vector2 bodySize,
            float range, Vector2 hitbox, float forward, float height)
        {
            var go = EnemySpawner.CreateBody(name);
            EnemySpawner.AddBoxBody(go, bodySize);

            var visual = EnemySpawner.AddVisual(go, sprite, controller, 1f, Vector3.zero, fallbackColor);
            if (sprite == null)
            {
                // 아트가 없을 때의 단색 사각형도 발밑 기준으로 몸체 크기에 맞춘다.
                visual.transform.localScale = new Vector3(bodySize.x, bodySize.y, 1f);
                visual.transform.localPosition = new Vector3(0f, bodySize.y / 2f, 0f);
            }

            EnemySpawner.AddStats(go, stats, StunDuration);

            var goblin = go.AddComponent<GoblinController>();
            goblin.ConfigureContactDamage(ContactDamageMultiplier, ContactDamageCooldown);
            goblin.Configure(10f, range, hitbox, forward, height);
            goblin.ConfigureObstacleLayer(EnemySpawner.GroundMask());

            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }
    }
}
