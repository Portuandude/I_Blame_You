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

        // 이동속도(유닛/초) = 문서 이동속도 × 2 × 버프 배율.
        public float MoveSpeedUnits => StatUnits.MoveSpeedToUnits(stats.moveSpeed) * MoveSpeedMultiplier;

        // 공격 사이 간격(초) = 1 / (문서 공격 속도 × 버프 배율).
        public float AttackInterval => StatUnits.AttackInterval(stats.attackSpeed * AttackSpeedMultiplier);

        public void Configure(StatBlock block)
        {
            stats = block.Clone();
        }
    }
}
