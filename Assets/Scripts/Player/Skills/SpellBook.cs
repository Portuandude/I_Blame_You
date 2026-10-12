using IBlameYou.Systems;

namespace IBlameYou.Player
{
    public enum SpellShape
    {
        Projectile,  // 직선 투사체, 첫 대상에 맞으면 사라짐
        Piercing,    // 관통 투사체
        Wave,        // 크고 느린 관통 투사체 (범위)
        GroundSpike  // 조준한 지점 아래 땅에서 솟아오름
    }

    public class SpellDefinition
    {
        public Element Element;
        public string Name;
        public float ManaCost;
        public string Chant;
        public SpellShape Shape;
        public float Speed;
        public float Radius;
        public float Range;
        public float DamageMultiplier = 1f;
    }

    // 플레이어 B의 공격 스킬 목록 (기획서 2.5). 영창문은 "불꽃이여, 작렬하라!"만 확정 예시이고 나머지는 가안.
    // 피해는 스탯 문서의 속성 데미지(각 25)에 DamageMultiplier를 곱하고 마법 공식으로 계산한다.
    public static class SpellBook
    {
        private static readonly SpellDefinition[] Spells =
        {
            new SpellDefinition { Element = Element.Fire, Name = "화염구", ManaCost = 20f, Chant = "불꽃이여, 작렬하라!",
                Shape = SpellShape.Projectile, Speed = 14f, Radius = 0.4f, Range = 14f, DamageMultiplier = 1.2f },
            new SpellDefinition { Element = Element.Water, Name = "물의 창", ManaCost = 15f, Chant = "물이여, 꿰뚫어라!",
                Shape = SpellShape.Piercing, Speed = 16f, Radius = 0.3f, Range = 16f },
            new SpellDefinition { Element = Element.Wind, Name = "돌풍", ManaCost = 20f, Chant = "바람이여, 휩쓸어라!",
                Shape = SpellShape.Wave, Speed = 9f, Radius = 1.1f, Range = 9f },
            new SpellDefinition { Element = Element.Earth, Name = "바위 가시", ManaCost = 25f, Chant = "대지여, 솟아올라라!",
                Shape = SpellShape.GroundSpike, Radius = 1.6f, Range = 12f, DamageMultiplier = 1.4f },
            new SpellDefinition { Element = Element.Neutral, Name = "마력탄", ManaCost = 10f, Chant = "마력이여, 모여라!",
                Shape = SpellShape.Projectile, Speed = 18f, Radius = 0.3f, Range = 14f, DamageMultiplier = 0.8f },
        };

        public static SpellDefinition Get(Element element) => Spells[(int)element];
    }
}
