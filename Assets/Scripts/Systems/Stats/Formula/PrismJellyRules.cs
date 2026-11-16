namespace IBlameYou.Systems
{
    // 중간보스 프리즘 젤리 규칙 (Docs/MidBossDraft.md 후보 D, 수치는 가안). 엔진 없는 어셈블리라 EditMode 테스트로 고정한다.
    // - 몸 색 = 몸 속성. 불/물/바람/땅 네 색 중 ShiftInterval초마다 지금과 다른 색으로 바뀌고, 바뀌기 WarnSeconds초 전부터 깜빡인다.
    // - 지금 색과 같은 속성 마법은 피해 SameColorMultiplier배. 그 색을 이기는 마법(1.5배)·지는 마법(0.5배)은
    //   몸 속성 상성(ElementAffinity, DamageCalculator)이 그대로 처리하므로 여기서는 같은 색만 줄인다.
    public static class PrismJellyRules
    {
        public const float ShiftInterval = 8f;
        public const float WarnSeconds = 1f;
        public const float SameColorMultiplier = 0.5f;

        public static readonly Element[] Colors = { Element.Fire, Element.Water, Element.Wind, Element.Earth };

        // 처음 색 (roll: 0 이상 1 미만 난수)
        public static Element FirstColor(double roll) => Colors[Pick(roll, Colors.Length)];

        // 다음 색: 지금 색을 뺀 세 색 중 하나. 지금 색이 네 색에 없으면(무속성) 네 색 중 하나.
        public static Element NextColor(Element current, double roll)
        {
            int at = System.Array.IndexOf(Colors, current);
            if (at < 0) return FirstColor(roll);
            int pick = Pick(roll, Colors.Length - 1);
            return Colors[(at + 1 + pick) % Colors.Length];
        }

        // 지금 색에 대한 속성 마법 배율 (같은 색만 줄인다. 물리 피해에는 쓰지 않는다)
        public static float ColorMultiplier(Element attack, Element color) => attack == color ? SameColorMultiplier : 1f;

        // 몸 속성 상성까지 곱한 최종 배율 (테스트·설명용)
        public static float TotalMultiplier(Element attack, Element color) =>
            ColorMultiplier(attack, color) * ElementAffinity.Multiplier(attack, color);

        private static int Pick(double roll, int count)
        {
            int i = (int)(roll * count);
            if (i < 0) i = 0;
            if (i >= count) i = count - 1;
            return i;
        }
    }
}
