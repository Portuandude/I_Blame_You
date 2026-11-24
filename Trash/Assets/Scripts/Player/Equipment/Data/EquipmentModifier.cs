using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 장비 수식어 (Docs/EquipmentDesign.md 4절, 2026-11-14 사람 결정으로 4종).
    // 등급 -1 비틀린 · 0 흔한 · 1 숙련자의 · 2 영웅의. 장비 기본 능력치는 그대로 두고,
    // 수식어가 "마법 공격 %"·"마나 소모 %"를 따로 더한다. 가격에는 배율을 곱한다.
    public static class EquipmentModifier
    {
        public const int MinGrade = -1;
        public const int MaxGrade = 2;

        // 마법 공격(마법 피해 배율에 더함)·마나 소모(마나 소모 배율에 더함) 크기. "--"·"++"는 MagicBig.
        public const float MagicBig = 0.15f;
        public const float CostStep = 0.1f;

        private static readonly string[] Names = { "비틀린", "흔한", "숙련자의", "영웅의" };
        private static readonly float[] MagicBonus = { -MagicBig, 0f, 0f, MagicBig };
        private static readonly float[] CostBonus = { CostStep, 0f, -CostStep, -CostStep };
        private static readonly float[] PriceMultipliers = { 0.7f, 1f, 1.4f, 2f };
        private static readonly int[] Weights = { 25, 45, 20, 10 }; // 합 100 (= % 확률)

        public static string Name(int grade) => Names[Index(grade)];
        public static float PriceMultiplier(int grade) => PriceMultipliers[Index(grade)];
        public static int Weight(int grade) => Weights[Index(grade)];

        // 수식어가 더하는 능력치 (흔한은 없음). 마법 공격 먼저, 마나 소모 다음.
        public static List<StatValue> Stats(int grade)
        {
            var list = new List<StatValue>();
            float magic = MagicBonus[Index(grade)];
            float cost = CostBonus[Index(grade)];
            if (magic != 0f) list.Add(new StatValue(EquipStat.MagicPower, magic));
            if (cost != 0f) list.Add(new StatValue(EquipStat.ManaCost, cost));
            return list;
        }

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
