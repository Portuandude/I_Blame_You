using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace IBlameYou.Systems
{
    // 스탯 문서(스탯문서.ods)의 한 열(캐릭터 하나)에 해당하는 수치 묶음.
    // 이동속도/공격 속도는 문서 단위 그대로 저장하고, 게임 단위 변환은 StatUnits로 한다.
    [Serializable]
    public class StatBlock
    {
        public float maxHealth;
        public float maxMana;
        public float maxStamina;
        public float moveSpeed;
        [FormerlySerializedAs("resistance")] public float debuffResistance; // 디버프 저항도: 디버프가 걸릴 확률만 낮춘다
        public float defense;
        [Range(0f, 1f)] public float critChance;
        public float critDamage = 1.5f;
        public float[] elementDamage = new float[ElementInfo.Count];
        [FormerlySerializedAs("elementResistance")] public float[] elementDefense = new float[ElementInfo.Count]; // 속성 방어력
        public float attackSpeed;
        public float physicalDamage;
        public float experience;

        public float ElementDamage(Element element) => elementDamage[(int)element];
        public float ElementDefense(Element element) => elementDefense[(int)element];

        public StatBlock Clone()
        {
            var copy = (StatBlock)MemberwiseClone();
            copy.elementDamage = (float[])elementDamage.Clone();
            copy.elementDefense = (float[])elementDefense.Clone();
            return copy;
        }
    }

    // 기획서 5.1에서 확정한 스탯 단위.
    public static class StatUnits
    {
        public const float UnitsPerMoveSpeed = 2f; // 이동속도 1 = 초당 2유닛

        public static float MoveSpeedToUnits(float moveSpeed) => moveSpeed * UnitsPerMoveSpeed;

        // 공격 속도 = 초당 공격 횟수 → 공격 사이 간격(초).
        public static float AttackInterval(float attackSpeed) => attackSpeed > 0f ? 1f / attackSpeed : float.PositiveInfinity;
    }
}
