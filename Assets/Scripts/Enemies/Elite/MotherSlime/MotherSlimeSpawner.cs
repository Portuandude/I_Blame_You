using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 정예 어미 슬라임: 슬라임 아트를 크게, 진하게 그린 변종. 스탯은 스탯 문서의 어미 슬라임 값.
    public static class MotherSlimeSpawner
    {
        private const float VisualScale = 1.3f;
        private static readonly Color Tint = new Color(0.75f, 0.55f, 1f);
        private const float AttackRange = 5f;
        private const float MaxLeapSpeed = 9f;

        public static SlimeController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            var prefab = artConfig != null ? artConfig.motherSlimePrefab : null;
            if (prefab != null && prefab.GetComponent<SplitOnDeath>() == null) prefab = null;
            var slime = EnemySpawner.Spawn<SlimeController>(prefab, () => Build(artConfig), position, "MotherSlime");
            // 회색 슬라임 그림에 보라를 곱한다 (2026-11-25 pixelfx 슬라임: 초록 원본에 곱하면 탁한 초록이 되던 것). 슬라임 킹과 같은 방식
            var visual = slime != null ? slime.transform.Find("Visual") : null;
            var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (renderer != null && artConfig != null && artConfig.slimeDefaultSprite != null)
                ElementRecolor.Attach(renderer, "Enemies/SlimeGray", Tint);
            return slime;
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = SlimeSpawner.BuildVariant(artConfig, "MotherSlime", StatTable.MotherSlime, VisualScale, Tint, AttackRange, MaxLeapSpeed);
            go.AddComponent<SplitOnDeath>();
            return go;
        }
    }
}
