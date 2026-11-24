using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 그물 코볼트 구성의 단일 출처 (스테이지 2 숲). 고블린 궁수 몸·행동(거리 두고 당겨 쏘기)에 그물(NetShot)을 얹는다.
    // 그림은 임시: 궁수 그림에 코볼트 색 (KoboldSpawner.KoboldTint).
    public static class KoboldNetterSpawner
    {
        public const float ShootRange = 7f;       // 그물은 화살보다 짧다
        public const float PreferredMin = 3f;

        public static GoblinArcherController Spawn(Vector3 position, LevelArtConfig artConfig = null) =>
            EnemySpawner.Spawn<GoblinArcherController>(null, () => Build(artConfig), position, "KoboldNetter");

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = GoblinArcherSpawner.Build(artConfig);
            go.name = "KoboldNetter";
            go.GetComponent<CombatStats>().Configure(StatTable.KoboldNetter);
            var health = go.GetComponent<HealthSystem>();
            if (health != null) health.ConfigureMaxHealth(StatTable.KoboldNetter.maxHealth);
            var visual = go.transform.Find("Visual");
            var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (renderer != null) renderer.color = artConfig != null && artConfig.goblinArcherDefaultSprite != null
                ? KoboldSpawner.KoboldTint
                : new Color(0.7f, 0.45f, 0.3f);
            var archer = go.GetComponent<GoblinArcherController>();
            archer.ThrowsNets = true;
            archer.ConfigureRange(ShootRange, PreferredMin);
            return go;
        }
    }
}
