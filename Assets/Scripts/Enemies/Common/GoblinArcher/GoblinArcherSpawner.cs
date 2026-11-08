using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 고블린 궁수 구성의 단일 출처.
    public static class GoblinArcherSpawner
    {
        private const float StunDuration = 0.3f;
        private static readonly Vector2 BodySize = new Vector2(0.5f, 0.85f);

        public static GoblinArcherController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            var prefab = artConfig != null ? artConfig.goblinArcherPrefab : null;
            return EnemySpawner.Spawn<GoblinArcherController>(prefab, () => Build(artConfig), position, "GoblinArcher");
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = EnemySpawner.CreateBody("GoblinArcher");
            EnemySpawner.AddBoxBody(go, BodySize);

            var sprite = artConfig != null ? artConfig.goblinArcherDefaultSprite : null;
            var visual = EnemySpawner.AddVisual(go, sprite, artConfig != null ? artConfig.goblinArcherAnimatorController : null,
                1f, Vector3.zero, new Color(0.5f, 0.6f, 0.25f));
            if (sprite == null)
            {
                visual.transform.localScale = new Vector3(BodySize.x, BodySize.y, 1f);
                visual.transform.localPosition = new Vector3(0f, BodySize.y / 2f, 0f);
            }

            EnemySpawner.AddStats(go, StatTable.GoblinArcher, StunDuration);

            var archer = go.AddComponent<GoblinArcherController>();
            archer.ConfigureContactDamage(0.3f, 1f);
            archer.ConfigureObstacleLayer(EnemySpawner.GroundMask());

            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }
    }
}
