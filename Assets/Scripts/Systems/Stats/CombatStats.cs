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

        // 버프와 디버프를 합친 최종 이동속도 배율.
        public float TotalMoveMultiplier => MoveSpeedMultiplier * SlowMultiplier;

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
