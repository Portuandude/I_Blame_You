using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 킹 구성의 단일 출처. 슬라임 그림을 크게 키우고 회색 프레임에 보라색을 곱한 뒤 머리에 왕관을 얹는다.
    // 분열할 때도 같은 Build로 두 번째 몸을 만든다. 프리팹은 아직 없어서 항상 코드로 조립한다.
    public static class SlimeKingSpawner
    {
        private const float VisualScale = 3.2f;
        private const float CrownSize = 1.4f;
        private const float KingEntranceWeight = 1.7f; // 등장 충격파·흔들림 배율 (기사 1)
        private const float StunDuration = 0f; // 보스는 경직되지 않는다 (SlimeKingController가 HitStun.Immune을 켠다)
        private const float ContactDamageMultiplier = 0.3f;
        private const float ContactDamageCooldown = 1f;
        private static readonly Color Tint = new Color(0.6f, 0.35f, 0.95f);

        // entrance: 위에서 떨어지는 등장 연출. 분열로 생기는 두 번째 몸은 false (BecomeTwin이 바로 코루틴·속도를 줘야 해서 꺼지면 안 된다).
        public static SlimeKingController Spawn(Vector3 position, LevelArtConfig artConfig = null, bool entrance = true)
        {
            var king = EnemySpawner.Spawn<SlimeKingController>(null, () => Build(artConfig), position, "SlimeKing");
            if (king != null && entrance)
            {
                var crown = king.GetComponentInChildren<SlimeKingCrown>();
                if (crown != null) crown.Place(); // 대역이 왕관을 제자리에서 복사하도록
                BossEntrance.Play(king.gameObject, KingEntranceWeight, SlimeKingController.DisplayName); // 위에서 떨어져 쿵 + 이름 띠
            }
            return king;
        }

        public static GameObject Build(LevelArtConfig artConfig)
        {
            var go = EnemySpawner.CreateBody("SlimeKing");

            var collider = go.AddComponent<PolygonCollider2D>();
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            bool hasArt = artConfig != null && artConfig.slimeDefaultSprite != null;
            var visual = EnemySpawner.AddVisual(
                go,
                hasArt ? artConfig.slimeDefaultSprite : null,
                artConfig != null ? artConfig.slimeAnimatorController : null,
                VisualScale,
                Vector3.zero,
                Tint);
            var renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            var recolor = hasArt ? ElementRecolor.Attach(renderer, "Enemies/SlimeGray", Tint) : null;

            go.AddComponent<SpriteColliderFitter>().Configure(renderer, collider);
            EnemySpawner.AddStats(go, StatTable.SlimeKing, StunDuration);

            var king = go.AddComponent<SlimeKingController>();
            king.ConfigureContactDamage(ContactDamageMultiplier, ContactDamageCooldown);
            king.Configure(EnemySpawner.GroundMask(), recolor);
            SlimeKingCrown.Attach(go, renderer, CrownSize);

            EnemySpawner.AssignEnemyLayer(go);
            return go;
        }
    }
}
