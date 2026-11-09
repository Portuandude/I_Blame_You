using System;

namespace IBlameYou.Systems
{
    // 기획서 5.2에서 확정한 비율식 데미지 공식 (순수 계산만, DamageCalculator가 쓴다).
    //   물리 피해 = 물리 데미지 × 100 / (100 + 방어력)
    //   마법 피해 = 속성 데미지 × 100 / (100 + 속성 방어력)
    // 방어력이 음수(디버프 등)여도 -50 아래로는 내려가지 않게 해서 피해가 2배를 넘지 않는다.
    public static class DamageFormula
    {
        public const float MinDefense = -50f;

        public static float Ratio(float attack, float defense)
        {
            // NaN 방어는 하한(-50)으로 본다 (예전 Mathf.Max와 같은 동작).
            float clamped = defense > MinDefense ? defense : MinDefense;
            return attack * 100f / (100f + clamped);
        }
    }
}
