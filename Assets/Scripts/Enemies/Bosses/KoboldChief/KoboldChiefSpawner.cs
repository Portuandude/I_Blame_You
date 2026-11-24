using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 스테이지 2 보스 코볼트 족장 구성의 단일 출처 (와르그 둘은 StageRoster.AdaptChunk가 보스 방에 따로 둔다 → KoboldSpawner.SpawnWarg).
    // 돌격(KoboldController) + 그물 던지기(KoboldChiefNet) + 무리(KoboldPack). 위에서 떨어져 등장 (BossEntrance).
    // 그림은 임시: 고블린 대전사 그림을 크게, 짙은 붉은 갈색 (아트 규칙).
    public static class KoboldChiefSpawner
    {
        public static readonly Color ChiefTint = new Color(0.85f, 0.45f, 0.35f);
        private static readonly Vector2 Body = new Vector2(1f, 1.7f);

        public static KoboldController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            var chief = EnemySpawner.Spawn<KoboldController>(null, () => Build(artConfig), position, "KoboldChief");
            if (chief != null) BossEntrance.Play(chief.gameObject, 1.5f, "코볼트 족장");
            return chief;
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = KoboldSpawner.Build(artConfig, "KoboldChief", StatTable.KoboldChief, Body, ChiefTint, 1.3f,
                artConfig != null ? artConfig.goblinWarriorDefaultSprite : null, artConfig != null ? artConfig.goblinWarriorAnimatorController : null);
            var chief = go.GetComponent<KoboldController>();
            chief.Configure(18f, 1.5f, 8f, 0.7f, 10f, 0.6f, 0.8f, 1.3f);
            chief.ConfigureHitbox(new Vector2(1.2f, 1.4f));
            chief.ShowsOwnHealthBar = false;
            go.AddComponent<KoboldPack>().IsChief = true;
            go.AddComponent<KoboldChiefNet>();
            return go;
        }

        // 와르그는 보스 방에서만 무리(KoboldPack)에 든다 (기능 테스트에서 따로 부르면 일반 적)
        public static void JoinPack(KoboldController warg)
        {
            if (warg == null) return;
            warg.ShowsOwnHealthBar = false;
            warg.gameObject.AddComponent<KoboldPack>();
        }
    }
}
