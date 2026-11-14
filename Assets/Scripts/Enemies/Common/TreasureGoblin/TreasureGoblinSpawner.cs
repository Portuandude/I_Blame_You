using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 보물 고블린 구성의 단일 출처: 고블린 그림을 금빛으로 물들이고 등에 금화 자루(노란 원 + 끈 점)를 단다.
    // 체력은 낮고 방어는 없으며 고블린보다 조금 빠르다 (달리는 플레이어가 따라잡을 수 있게).
    public static class TreasureGoblinSpawner
    {
        private const float StunDuration = 0.35f;
        private static readonly Vector2 BodySize = new Vector2(0.5f, 0.85f);
        private static readonly Color Tint = new Color(1f, 0.92f, 0.62f);
        private static readonly Color SackColor = new Color(0.95f, 0.75f, 0.25f);

        public static TreasureGoblinController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            return EnemySpawner.Spawn<TreasureGoblinController>(null, () => Build(artConfig), position, "TreasureGoblin");
        }

        private static StatBlock Stats
        {
            get
            {
                var stats = StatTable.Goblin;
                stats.maxHealth = 60;
                stats.defense = 0;
                stats.moveSpeed = 2.4f;
                stats.physicalDamage = 0;
                stats.experience = 20;
                return stats;
            }
        }

        private static GameObject Build(LevelArtConfig artConfig)
        {
            var go = EnemySpawner.CreateBody("TreasureGoblin");
            EnemySpawner.AddBoxBody(go, BodySize);

            var sprite = artConfig != null ? artConfig.goblinDefaultSprite : null;
            var visual = EnemySpawner.AddVisual(go, sprite, artConfig != null ? artConfig.goblinAnimatorController : null, 1f, Vector3.zero, Tint);
            if (sprite == null)
            {
                visual.transform.localScale = new Vector3(BodySize.x, BodySize.y, 1f);
                visual.transform.localPosition = new Vector3(0f, BodySize.y / 2f, 0f);
            }
            else
            {
                visual.GetComponent<SpriteRenderer>().color = Tint;
            }

            // 금화 자루: 몸 뒤쪽(왼쪽, 몸이 돌면 같이 뒤집힌다) 어깨 높이
            var sack = new GameObject("Sack");
            sack.transform.SetParent(go.transform, false);
            sack.transform.localPosition = new Vector3(-0.28f, 0.6f, 0f);
            sack.transform.localScale = new Vector3(0.42f, 0.46f, 1f);
            var sackRenderer = sack.AddComponent<SpriteRenderer>();
            sackRenderer.sprite = SolidSpriteFactory.CreateCircle();
            sackRenderer.color = SackColor;
            sackRenderer.sortingOrder = 0; // 몸(1) 뒤
            var knot = new GameObject("Knot");
            knot.transform.SetParent(sack.transform, false);
            knot.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            knot.transform.localScale = new Vector3(0.35f, 0.3f, 1f);
            var knotRenderer = knot.AddComponent<SpriteRenderer>();
            knotRenderer.sprite = SolidSpriteFactory.CreateCircle();
            knotRenderer.color = new Color(0.6f, 0.4f, 0.15f);
            knotRenderer.sortingOrder = 0;

            EnemySpawner.AddStats(go, Stats, StunDuration);
            var goblin = go.AddComponent<TreasureGoblinController>();
            goblin.Configure(EnemySpawner.GroundMask());
            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }
    }
}
