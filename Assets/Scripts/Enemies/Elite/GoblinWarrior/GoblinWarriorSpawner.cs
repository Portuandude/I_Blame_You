using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 정예 고블린 대전사: 큰 도끼를 휘두르는 근접 고블린 + 광폭화(EnrageAbility).
    public static class GoblinWarriorSpawner
    {
        public static GoblinController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            var prefab = artConfig != null ? artConfig.goblinWarriorPrefab : null;
            return EnemySpawner.Spawn<GoblinController>(prefab, () => Build(artConfig), position, "GoblinWarrior");
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = GoblinSpawner.BuildMelee(artConfig, "GoblinWarrior", StatTable.GoblinWarrior,
                artConfig != null ? artConfig.goblinWarriorDefaultSprite : null,
                artConfig != null ? artConfig.goblinWarriorAnimatorController : null,
                new Color(0.6f, 0.25f, 0.2f), new Vector2(0.8f, 1.35f),
                range: 1.8f, hitbox: new Vector2(1.8f, 1.3f), forward: 1.0f, height: 0.65f);
            go.AddComponent<EnrageAbility>();
            return go;
        }
    }
}
