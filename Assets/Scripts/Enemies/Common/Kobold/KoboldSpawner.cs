using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 스테이지 2 숲 돌격형 구성의 단일 출처: 코볼트 근접 돌격형, 족장의 늑대 와르그 (KoboldController).
    // 그림은 임시 자리 표시 (2026-11-24 아트 규칙: 새 그림은 pixelfx 시안 → 사람 확인 뒤). 고블린 그림·애니메이션에 색만 입힌다.
    public static class KoboldSpawner
    {
        private const float StunDuration = 0.3f;
        public static readonly Color KoboldTint = new Color(0.95f, 0.6f, 0.45f);   // 붉은 갈색 (고블린 초록과 구별)
        public static readonly Color WargTint = new Color(0.55f, 0.55f, 0.65f);    // 회색 늑대
        private static readonly Vector2 KoboldBody = new Vector2(0.55f, 0.9f);
        private static readonly Vector2 WargBody = new Vector2(1.1f, 0.8f);

        public static KoboldController SpawnKobold(Vector3 position, LevelArtConfig artConfig = null) =>
            EnemySpawner.Spawn<KoboldController>(null, () => BuildKobold(artConfig), position, "Kobold");

        public static KoboldController SpawnWarg(Vector3 position, LevelArtConfig artConfig = null) =>
            EnemySpawner.Spawn<KoboldController>(null, () => BuildWarg(artConfig), position, "Warg");

        public static GameObject BuildKobold(LevelArtConfig artConfig)
        {
            var go = Build(artConfig, "Kobold", StatTable.Kobold, KoboldBody, KoboldTint, 1f,
                artConfig != null ? artConfig.goblinDefaultSprite : null, artConfig != null ? artConfig.goblinAnimatorController : null);
            var kobold = go.GetComponent<KoboldController>();
            kobold.Configure(10f, 1.2f, 5.5f, 0.5f, 8.5f, 0.45f, 0.6f, 1.2f);
            kobold.ConfigureHitbox(new Vector2(0.8f, 0.8f));
            return go;
        }

        // 와르그: 빠르고 낮게 덮친다 (돌격 첫 순간 살짝 뜀). 몸이 가로로 길다
        public static GameObject BuildWarg(LevelArtConfig artConfig)
        {
            var go = Build(artConfig, "Warg", StatTable.Warg, WargBody, WargTint, 1.25f,
                artConfig != null ? artConfig.goblinSoldierDefaultSprite : null, artConfig != null ? artConfig.goblinSoldierAnimatorController : null);
            var warg = go.GetComponent<KoboldController>();
            warg.Configure(16f, 1.5f, 7f, 0.4f, 11f, 0.5f, 0.7f, 1f, pounce: 5f);
            warg.ConfigureHitbox(new Vector2(1.1f, 0.8f));
            return go;
        }

        internal static GameObject Build(LevelArtConfig artConfig, string name, StatBlock stats, Vector2 bodySize, Color tint, float scale,
            Sprite sprite, RuntimeAnimatorController controller)
        {
            var go = EnemySpawner.CreateBody(name);
            EnemySpawner.AddBoxBody(go, bodySize);

            var visual = EnemySpawner.AddVisual(go, sprite, controller, scale, Vector3.zero, tint);
            if (sprite == null)
            {
                visual.transform.localScale = new Vector3(bodySize.x, bodySize.y, 1f);
                visual.transform.localPosition = new Vector3(0f, bodySize.y / 2f, 0f);
            }
            else
            {
                visual.GetComponent<SpriteRenderer>().color = tint;
            }

            EnemySpawner.AddStats(go, stats, StunDuration);

            var mover = go.AddComponent<KoboldController>();
            mover.ConfigureContactDamage(0.3f, 1f);
            mover.ConfigureObstacleLayer(EnemySpawner.GroundMask());

            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }
    }
}
