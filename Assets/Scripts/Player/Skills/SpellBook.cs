using IBlameYou.Systems;

namespace IBlameYou.Player
{
    public enum SpellShape
    {
        Projectile,  // 유도 투사체, 첫 대상에 맞으면 사라짐
        Piercing,    // 유도 관통 투사체
        GroundSpike, // 조준점에서 가장 가까운 적을 노려 발밑에서 솟아오름
        FireWall,    // 앞 땅 위에 잠깐 서는 불벽 (불꽃 장막, FireWall.cs)
        Mist,        // 커서 자리 둥근 안개 (물안개, MistZone)
        Tailwind     // B 자신에게 거는 버프: Duration초 동안 마법진이 한 단계 쉬워짐 (순풍, MagicCircleRules.TierFor)
    }

    public class SpellDefinition
    {
        public Element Element;
        public string Name;
        public float ManaCost;
        public string Chant;              // 주문 한 줄. 2026-11-16부터는 타이핑하지 않고 마법진 위에 읽을거리로만 띄운다
        public SpellShape Shape;
        public float Speed;
        public float Radius;
        public float Range;
        public float DamageMultiplier = 1f;
        public float HomingTurnRate;      // 초당 최대 회전 각도(도). 클수록 잘 따라간다.
        public float Windup;              // 바위 가시: 시전 후 예고가 생기기까지의 선딜 (그동안 다른 공격 스킬 불가)
        public float CastDelay;           // 바위 가시: 예고가 생긴 뒤 솟아오르기까지의 시간
        public StatusDebuff Debuff;       // 맞은 적에게 거는 디버프 (확률은 StatusEffects.Chance)
        public float Duration;
        public bool AtCursor;             // 바라보는 쪽이 아니라 발동 순간 마우스 커서 자리 (불꽃 비·물안개·운석)
        public int Volley = 1;            // 투사체를 몇 발 연달아 (칼바람 3)
        public float Height;              // 불꽃 비 구역 높이            // 불벽이 서 있는 시간 / 순풍이 이어지는 시간
        public bool DebuffAlways;         // 확률 판정 없이 반드시 건다 (얼음 족쇄)
        // 아카데미에서 배우는 마법만 (Docs/CityDesign.md 5절): 저장 키·가루·설명
        public string Id;
        public int DustCost;
        public string Description;
    }

    // 플레이어 B의 공격 스킬 목록 (기획서 2.5). 고르면 마법진을 따라 그려 발동한다 (MagicCircleRules, 2026-11-16 영창 타이핑 대체).
    // 피해는 스탯 문서의 속성 데미지(각 25)에 DamageMultiplier를 곱하고 마법 공식으로 계산한다.
    // 모든 투사체는 수평으로 출발하고 유도로 방향을 꺾는다.
    // 속성별 성격 (2026-10-12 사용자 지시):
    //   불: 강한 피해, 낮은 유도, 중첩 화상 / 물: 느린 탄, 낮은 피해, 젖음(둔화)
    //   바람: 빠른 탄, 높은 유도, 낮은 피해 / 대지: 선딜 + 긴 딜레이, 낮은 피해, 타겟팅, 경직
    //   무: 평타로 전환 (BasicBolt). 보통 유도·속도, 피해 절반, 마나 5
    public static class SpellBook
    {
        public const float HomingRange = 12f;

        private static readonly SpellDefinition[] Spells =
        {
            new SpellDefinition { Element = Element.Fire, Name = "화염구", ManaCost = 20f, Chant = "불꽃이여, 작렬하라!",
                Shape = SpellShape.Projectile, Speed = 12f, Radius = 0.4f, Range = 14f, DamageMultiplier = 1.5f,
                HomingTurnRate = 90f, Debuff = StatusDebuff.Burn },
            new SpellDefinition { Element = Element.Water, Name = "물의 창", ManaCost = 15f, Chant = "물이여, 꿰뚫어라!",
                Shape = SpellShape.Piercing, Speed = 7f, Radius = 0.35f, Range = 12f, DamageMultiplier = 0.7f,
                HomingTurnRate = 180f, Debuff = StatusDebuff.Wet },
            new SpellDefinition { Element = Element.Wind, Name = "돌풍", ManaCost = 20f, Chant = "바람이여, 휩쓸어라!",
                Shape = SpellShape.Projectile, Speed = 22f, Radius = 0.45f, Range = 18f, DamageMultiplier = 0.7f,
                HomingTurnRate = 600f },
            new SpellDefinition { Element = Element.Earth, Name = "바위 가시", ManaCost = 25f, Chant = "대지여, 솟아올라라!",
                Shape = SpellShape.GroundSpike, Radius = 1.2f, Range = 12f, DamageMultiplier = 0.7f,
                Windup = 0.5f, CastDelay = 0.8f, Debuff = StatusDebuff.Stun },
            null, // 무속성은 영창 스킬이 아니라 B의 평타(BasicBolt)
        };

        // B의 평타 (2026-10-16): 좌클릭으로 영창 없이 쏘는 무속성 마력탄. 피해는 예전 마력탄(×1.0)의 절반, 마나 5.
        // 연사 간격은 공격 속도(초당 1회)로 정한다 (PlayerCombat).
        public static readonly SpellDefinition BasicBolt = new SpellDefinition
        {
            Element = Element.Neutral, Name = "마력탄", ManaCost = 5f, Chant = "",
            Shape = SpellShape.Projectile, Speed = 14f, Radius = 0.3f, Range = 14f, DamageMultiplier = 0.5f,
            HomingTurnRate = 270f
        };

        // 영창으로 쓰는 공격 스킬. 무속성은 없다(null).
        public static SpellDefinition Get(Element element) => Spells[(int)element];

        // 속성별 배운 마법 (P2 마법 다이얼의 2단계, SpellDialRules). 칸 0은 처음부터 아는 기본 마법(위 표),
        // 그 뒤는 도시 아카데미에서 배우는 새 마법이 Learn으로 붙는다 (CityDesign.md 5절, 도시 4단계에서 연결).
        private static readonly System.Collections.Generic.List<SpellDefinition>[] Extra =
        {
            new System.Collections.Generic.List<SpellDefinition>(), new System.Collections.Generic.List<SpellDefinition>(),
            new System.Collections.Generic.List<SpellDefinition>(), new System.Collections.Generic.List<SpellDefinition>(),
        };

        public static int LearnedCount(Element element)
        {
            int e = (int)element;
            if (e < 0 || e >= Extra.Length || Spells[e] == null) return 0;
            return 1 + Extra[e].Count;
        }

        // 속성의 slot번째 배운 마법. 범위 밖이면 기본 마법.
        public static SpellDefinition Get(Element element, int slot)
        {
            int e = (int)element;
            if (e < 0 || e >= Extra.Length) return Get(element);
            if (slot <= 0 || slot > Extra[e].Count) return Spells[e];
            return Extra[e][slot - 1];
        }

        // 아카데미 마법 (도시 4단계, 2026-11-15 사람 결정 속성마다 2개 — 불 범위·물 디버프·바람 마법 속도·대지 데미지).
        // 속성마다 싼 것(60)·비싼 것(90). 마법진 단계는 같은 속성 안 코스트 순위 (CircleTier).
        public static readonly SpellDefinition[] Academy =
        {
            new SpellDefinition { Id = "IceShackle", Element = Element.Water, Name = "얼음 족쇄", ManaCost = 20f, Chant = "얼음이여, 묶어라!",
                Shape = SpellShape.Projectile, Speed = 9f, Radius = 0.35f, Range = 13f, DamageMultiplier = 0.4f,
                HomingTurnRate = 220f, Debuff = StatusDebuff.Wet, DebuffAlways = true, DustCost = 60,
                Description = "맞은 적을 반드시 젖게 해 3초 동안 40% 느리게 (정예·보스는 20%, 피해는 약함)" },
            new SpellDefinition { Id = "StoneLance", Element = Element.Earth, Name = "바위 창", ManaCost = 30f, Chant = "대지여, 창이 되어 꿰뚫어라!",
                Shape = SpellShape.GroundSpike, Radius = 0.8f, Range = 12f, DamageMultiplier = 1.75f,
                Windup = 0.6f, CastDelay = 0.6f, Debuff = StatusDebuff.Stun, DustCost = 60,
                Description = "좁은 자리에 큰 창 하나: 바위 가시의 2.5배 피해 (선딜이 조금 더 김)" },
            new SpellDefinition { Id = "FlameCurtain", Element = Element.Fire, Name = "불꽃 장막", ManaCost = 25f, Chant = "불꽃이여, 벽이 되어라!",
                Shape = SpellShape.FireWall, Radius = 2.5f, Range = 6f, DamageMultiplier = 0.3f, Duration = 2f,
                Debuff = StatusDebuff.Burn, DustCost = 60,
                Description = "앞 5칸에 2초 동안 불벽: 안의 적 모두 0.4초마다 데고, 처음 닿으면 반드시 화상" },
            new SpellDefinition { Id = "Tailwind", Element = Element.Wind, Name = "순풍", ManaCost = 15f, Chant = "바람이여, 등을 밀어라!",
                Shape = SpellShape.Tailwind, Duration = 6f, DustCost = 60,
                Description = "6초 동안 모든 마법진이 한 단계 쉬워진다 (대마법 → 숙련, 숙련 → 견습)" },
            new SpellDefinition { Id = "FlameRain", Element = Element.Fire, Name = "불꽃 비", ManaCost = 35f, Chant = "불꽃이여, 비가 되어 쏟아져라!",
                Shape = SpellShape.FireWall, AtCursor = true, Radius = 4f, Height = 4f, Range = 14f, DamageMultiplier = 0.3f, Duration = 1.5f,
                Debuff = StatusDebuff.Burn, DustCost = 90,
                Description = "커서 둘레 넓은 자리(8칸)에 1.5초 불똥: 안의 적 모두 0.4초마다 데고 처음 맞으면 화상" },
            new SpellDefinition { Id = "WaterMist", Element = Element.Water, Name = "물안개", ManaCost = 30f, Chant = "물이여, 안개로 감싸라!",
                Shape = SpellShape.Mist, AtCursor = true, Radius = 3f, Range = 14f, Duration = 5f, DustCost = 90,
                Description = "커서 자리에 5초 안개: 안의 적은 주는 피해 -30%, 받는 피해 +15%" },
            new SpellDefinition { Id = "GaleBlades", Element = Element.Wind, Name = "칼바람", ManaCost = 25f, Chant = "베어라!",
                Shape = SpellShape.Projectile, Volley = 3, Speed = 24f, Radius = 0.35f, Range = 16f, DamageMultiplier = 0.45f,
                HomingTurnRate = 600f, DustCost = 90,
                Description = "바람 칼날 3발을 빠르게 연달아" },
            new SpellDefinition { Id = "Meteor", Element = Element.Earth, Name = "운석", ManaCost = 40f, Chant = "하늘의 바위여, 별이 되어 떨어져 모든 것을 부숴라!",
                Shape = SpellShape.GroundSpike, AtCursor = true, Radius = 1f, Range = 16f, DamageMultiplier = 2.45f,
                Windup = 0.8f, CastDelay = 0.7f, Debuff = StatusDebuff.Stun, DustCost = 90,
                Description = "선딜 뒤 커서 자리에 운석: 바위 가시의 3.5배, 좁은 범위" },
        };

        // 마법진 단계 (0 견습 · 1 숙련 · 2 대마법): 같은 속성의 기본 마법 + 아카데미 마법 중 코스트 순위. 배웠는지와 상관없이 고정.
        public static int CircleTier(SpellDefinition spell, bool tailwind = false)
        {
            if (spell == null) return 0;
            var costs = new System.Collections.Generic.List<float>();
            int e = (int)spell.Element;
            if (e >= 0 && e < Spells.Length && Spells[e] != null) costs.Add(Spells[e].ManaCost);
            foreach (var extra in Academy)
                if (extra.Element == spell.Element) costs.Add(extra.ManaCost);
            return MagicCircleRules.TierFor(spell.ManaCost, costs, tailwind);
        }

        public static bool IsLearned(SpellDefinition spell)
        {
            int e = (int)spell.Element;
            return e >= 0 && e < Extra.Length && Extra[e].Contains(spell);
        }

        // 판을 새로 만들 때 배운 마법을 비우고 저장에서 다시 채운다 (PlaytestBootstrap).
        public static void ResetLearned()
        {
            foreach (var list in Extra) list.Clear();
        }

        public static void Learn(SpellDefinition spell)
        {
            int e = (int)spell.Element;
            if (e < 0 || e >= Extra.Length || Extra[e].Contains(spell)) return;
            Extra[e].Add(spell);
        }
    }
}
