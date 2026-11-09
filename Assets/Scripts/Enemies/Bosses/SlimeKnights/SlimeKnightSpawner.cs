using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 중간보스 쌍둥이 슬라임 기사 구성의 단일 출처 (Docs/MidBossDraft.md 후보 C, 진행도 55% 중간보스 방).
    // 슬라임 몸(도약 덮치기)을 그대로 쓰고, 투구 쓴 그림(Resources/Enemies/SlimeKnight*)으로 바꿔 끼운 뒤
    // 불 기사는 돌진(KnightCharge), 물 기사는 방패(KnightShield)를 붙인다. 둘 다 짝이 쓰러지면 광폭화(SlimeKnight).
    // 상성: 불 기사 = 불 몸(물 마법에 약함), 물 기사 = 물 몸(상성표대로 땅에 약함) + 바람 방어 -30(초안의 "바람에 약함").
    public static class SlimeKnightSpawner
    {
        private const float VisualScale = 0.95f;
        private const float AttackRange = 4.5f;
        private const float MaxLeapSpeed = 9f;
        private const float OwnElementDefense = 40f;
        private const float WaterKnightWindDefense = -30f;

        public static SlimeController Spawn(Vector3 position, Element element, LevelArtConfig artConfig = null)
        {
            return EnemySpawner.Spawn<SlimeController>(null, () => Build(artConfig, element), position, NameOf(element));
        }

        private static string NameOf(Element element) => element == Element.Fire ? "SlimeKnightFire" : "SlimeKnightWater";

        public static GameObject Build(LevelArtConfig artConfig, Element element)
        {
            bool fire = element == Element.Fire;

            var stats = StatTable.SlimeKnight;
            float elemental = 0f;
            for (int i = 0; i < stats.elementDamage.Length; i++)
            {
                elemental = Mathf.Max(elemental, stats.elementDamage[i]);
                stats.elementDamage[i] = 0f;
            }
            stats.elementDamage[(int)element] = elemental;
            stats.elementDefense[(int)element] = OwnElementDefense;
            if (!fire) stats.elementDefense[(int)Element.Wind] = WaterKnightWindDefense;

            var go = SlimeSpawner.BuildVariant(artConfig, NameOf(element), stats, VisualScale, Color.white, AttackRange, MaxLeapSpeed);
            go.GetComponent<CombatStats>().SetBodyElement(element);
            go.GetComponent<SlimeController>().SetAttackElement(element);

            var visual = go.transform.Find("Visual");
            var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            ElementRecolor recolor = null;
            if (renderer != null)
            {
                if (artConfig != null && artConfig.slimeDefaultSprite != null)
                    recolor = ElementRecolor.Attach(renderer, fire ? "Enemies/SlimeKnightFire" : "Enemies/SlimeKnightWater", Color.white);
                else
                    renderer.color = ElementInfo.Color(element); // 아트가 없을 때의 단색 사각형
            }

            var enrage = go.AddComponent<EnrageAbility>();
            enrage.AutoTrigger = false;
            enrage.Configure(0.4f, 2f, 12f);

            if (fire) go.AddComponent<KnightCharge>();
            else go.AddComponent<KnightShield>();
            go.AddComponent<SlimeKnight>().Configure(recolor, enrage);
            return go;
        }
    }
}
