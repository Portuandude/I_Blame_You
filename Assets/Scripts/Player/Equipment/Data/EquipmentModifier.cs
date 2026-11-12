using System;

namespace IBlameYou.Player
{
    // 장비 수식어 (Docs/EquipmentDesign.md 4절). 등급 -2 ~ +3, 0은 "보통"(이름에 안 붙음).
    // 능력치 전체에 배율을 곱하고 가격에도 배율을 곱한다.
    public static class EquipmentModifier
    {
        public const int MinGrade = -2;
        public const int MaxGrade = 3;

        private static readonly string[] Names = { "부서진", "낡은", "", "튼튼한", "빛나는", "전설의" };
        private static readonly float[] StatMultipliers = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
        private static readonly float[] PriceMultipliers = { 0.4f, 0.7f, 1f, 1.4f, 1.9f, 2.8f };
        private static readonly int[] Weights = { 8, 20, 40, 20, 9, 3 }; // 합 100 (= % 확률)

        public static string Name(int grade) => Names[Index(grade)];
        public static float StatMultiplier(int grade) => StatMultipliers[Index(grade)];
        public static float PriceMultiplier(int grade) => PriceMultipliers[Index(grade)];
        public static int Weight(int grade) => Weights[Index(grade)];

        public static int TotalWeight
        {
            get
            {
                int sum = 0;
                foreach (int w in Weights) sum += w;
                return sum;
            }
        }

        // 확률표대로 등급을 뽑는다. bonus: 등급을 그만큼 올린다 (중간보스 상자 +1). 최고 등급은 넘지 않는다.
        public static int Roll(Random rng, int bonus = 0)
        {
            int pick = rng.Next(TotalWeight);
            int grade = MinGrade;
            for (int i = 0; i < Weights.Length; i++)
            {
                if (pick < Weights[i])
                {
                    grade = MinGrade + i;
                    break;
                }

                pick -= Weights[i];
            }

            return Math.Min(MaxGrade, grade + bonus);
        }

        private static int Index(int grade) => Math.Max(MinGrade, Math.Min(MaxGrade, grade)) - MinGrade;
    }
}
