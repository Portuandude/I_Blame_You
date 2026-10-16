using IBlameYou.Systems;

namespace IBlameYou.Player
{
    public enum SpellShape
    {
        Projectile,  // 유도 투사체, 첫 대상에 맞으면 사라짐
        Piercing,    // 유도 관통 투사체
        GroundSpike  // 조준점에서 가장 가까운 적을 노려 발밑에서 솟아오름
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
        public float HomingTurnRate;      // 초당 최대 회전 각도(도). 클수록 잘 따라간다.
        public float Windup;              // 바위 가시: 시전 후 예고가 생기기까지의 선딜 (그동안 다른 공격 스킬 불가)
        public float CastDelay;           // 바위 가시: 예고가 생긴 뒤 솟아오르기까지의 시간
        public StatusDebuff Debuff;       // 맞은 적에게 거는 디버프 (확률은 StatusEffects.Chance)
    }

    // 플레이어 B의 공격 스킬 목록 (기획서 2.5). 영창문은 "불꽃이여, 작렬하라!"만 확정 예시이고 나머지는 가안.
    // 피해는 스탯 문서의 속성 데미지(각 25)에 DamageMultiplier를 곱하고 마법 공식으로 계산한다.
    // 모든 투사체는 수평으로 출발하고 유도로 방향을 꺾는다.
    // 속성별 성격 (2026-10-12 사용자 지시):
    //   불: 강한 피해, 낮은 유도, 중첩 화상 / 물: 느린 탄, 낮은 피해, 젖음(둔화)
    //   바람: 빠른 탄, 높은 유도, 낮은 피해 / 대지: 선딜 + 긴 딜레이, 낮은 피해, 타겟팅, 경직
    //   무: 보통 유도·속도·피해
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
            new SpellDefinition { Element = Element.Neutral, Name = "마력탄", ManaCost = 10f, Chant = "마력이여, 모여라!",
                Shape = SpellShape.Projectile, Speed = 14f, Radius = 0.3f, Range = 14f, DamageMultiplier = 1f,
                HomingTurnRate = 270f },
        };

        public static SpellDefinition Get(Element element) => Spells[(int)element];
    }
}
