using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 중간보스 프리즘 젤리 구성의 단일 출처 (Docs/MidBossDraft.md 후보 D, 2026-11-16 "종족은 슬라임으로 하나 더").
    // 쌍둥이 기사와 판마다 시드로 반반 (RoomLayout.MidBossPool). 슬라임 몸(도약 덮치기)에 수정 젤리 그림(Resources/Enemies/PrismJelly, 회색 슬라임판에서 생성)을
    // 지금 색으로 물들이고, PrismJelly가 8초마다 색(= 몸 속성)을 바꾼다. 패턴은 JellyPatterns.
    public static class PrismJellySpawner
    {
        public const string ObjectName = "PrismJelly";
        private const float VisualScale = 1.3f;   // 기사(0.95)보다 크고 슬라임 킹보다 작게
        private const float AttackRange = 5f;
        private const float MaxLeapSpeed = 9f;

        public static SlimeController Spawn(Vector3 position, LevelArtConfig artConfig = null)
        {
            var jelly = EnemySpawner.Spawn<SlimeController>(null, () => Build(artConfig), position, ObjectName);
            if (jelly != null) BossEntrance.Play(jelly.gameObject, 1.2f); // 위에서 떨어져 쿵 착지
            return jelly;
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = SlimeSpawner.BuildVariant(artConfig, ObjectName, StatTable.PrismJelly, VisualScale, Color.white, AttackRange, MaxLeapSpeed);

            var visual = go.transform.Find("Visual");
            var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            ElementRecolor recolor = null;
            if (renderer != null && artConfig != null && artConfig.slimeDefaultSprite != null)
                recolor = ElementRecolor.Attach(renderer, "Enemies/PrismJelly", Color.white); // 수정 젤리 그림 (Tools/ArtGen/generate_prism_jelly.py)

            go.GetComponent<SlimeController>().ShowsOwnHealthBar = false; // 화면 위 보스 체력바를 쓴다
            var jelly = go.AddComponent<PrismJelly>();
            jelly.Configure(recolor, renderer);
            go.AddComponent<JellyPatterns>(); // 출렁 착지·젤리 방울·프리즘 광선 (PrismJelly 뒤에 붙인다: Awake에서 찾는다)
            // 등장 낙하 대역(BossEntrance)은 몸이 꺼지기 전 renderer.color를 베낀다: ElementRecolor.Tint는 LateUpdate에서야 칠해지므로 첫 색을 미리 칠해 둔다
            if (renderer != null) renderer.color = ElementInfo.Color(jelly.CurrentColor);
            return go;
        }
    }
}
