using UnityEngine;

namespace IBlameYou.Systems
{
    // 캐릭터 하나의 전투 스탯. 스포너가 StatTable 값으로 Configure한다.
    // 버프(예: 고블린 대전사 광폭화)는 배율만 바꾸고 원본 스탯은 건드리지 않는다.
    public class CombatStats : MonoBehaviour
    {
        [SerializeField] private StatBlock stats = new StatBlock();

        public StatBlock Stats => stats;

        public float MoveSpeedMultiplier { get; set; } = 1f;
        public float AttackSpeedMultiplier { get; set; } = 1f;

        // 디버프(젖음) 둔화 배율. 버프 배율과 따로 두어 서로 덮어쓰지 않게 한다.
        public float SlowMultiplier { get; set; } = 1f;

        // 붙잡기 둔화 배율 (슬라임 킹 촉수). 젖음과 겹쳐도 서로 지우지 않게 따로 둔다.
        public float GrabSlowMultiplier { get; set; } = 1f;

        // 버프와 디버프를 합친 최종 이동속도 배율.
        public float TotalMoveMultiplier => MoveSpeedMultiplier * SlowMultiplier * GrabSlowMultiplier * (TerrainSlowStacks > 0 ? TerrainSlow : 1f);

        // 바닥 지형 둔화 (숲 바닥 덩굴 VineFloor): 밟고 있는 덩굴 수. 겹쳐 밟아도 둔화는 한 번만 (젖음·붙잡기와는 곱한다)
        public const float TerrainSlow = 0.6f;
        public int TerrainSlowStacks { get; set; }

        // 주는 피해 배율 (레벨업 특성 등). 방어 계산 전 공격 수치에 곱한다.
        public float DamageMultiplier { get; set; } = 1f;

        // 마법 피해에만 곱하는 배율 (장비 "마법 공격" 능력치 — 옛 수식어용, 지금 주는 장비 없음. 끼고 빼며 더하고 뺀다). 0.1 아래로는 내려가지 않게 쓸 때 자른다.
        public float MagicDamageMultiplier { get; set; } = 1f;

        // 약점 속성으로 맞혔을 때(상성 배율 > 1) 더 주는 피해 비율 (유물 속성 공명석 +0.2, DamageCalculator)
        public float WeaknessDamageBonus { get; set; }

        // 이동속도(유닛/초) = 문서 이동속도 × 2 × 버프 배율 × 둔화 배율.
        public float MoveSpeedUnits => StatUnits.MoveSpeedToUnits(stats.moveSpeed) * TotalMoveMultiplier;

        // 공격 사이 간격(초) = 1 / (문서 공격 속도 × 버프 배율).
        public float AttackInterval => StatUnits.AttackInterval(stats.attackSpeed * AttackSpeedMultiplier);

        // 몸의 속성 (속성 슬라임 등). 있으면 마법 피해에 속성 상성이 적용된다. 없으면 상성 없음.
        public bool HasBodyElement { get; private set; }
        public Element BodyElement { get; private set; }

        public void SetBodyElement(Element element)
        {
            HasBodyElement = true;
            BodyElement = element;
        }

        public void Configure(StatBlock block)
        {
            stats = block.Clone();
        }
    }
}
