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

        // 패턴 (2단계, JellyPatterns). 출렁 착지와 젤리 방울을 번갈아, 체력 50% 이하에서는 프리즘 광선을 따로 12초마다.
        public enum Pattern { Slam, Droplets }

        public const float FirstPatternDelay = 2.5f;
        public const float PatternInterval = 3.5f;    // 한 패턴이 끝나고 다음 패턴까지
        public const float SlamWindup = 0.6f;
        public const float SlamJumpSpeed = 13f;       // 높이 뛴다 (유닛/초)
        public const float ShockwaveSpeed = 9f;       // 착지 충격파가 좌우로 바닥을 타고 가는 속도
        public const float ShockwaveSeconds = 0.9f;   // 약 8유닛
        public const float ShockwaveHeight = 0.7f;    // 바닥에서 이 높이까지만 맞는다 (점프·위로 순간이동으로 피함)
        public const float DropletWindup = 0.5f;
        public const int DropletMin = 4;
        public const int DropletMax = 6;
        public const float PuddleSeconds = 2f;
        public const float PuddleTick = 0.5f;         // 웅덩이 위에 있으면 이 간격으로 상태이상 시도
        public const float BeamHealthRatio = 0.5f;
        public const float BeamInterval = 12f;
        public const float BeamWarn = 1.2f;
        public const float BeamSeconds = 0.35f;
        public const float BeamHalfHeight = 0.45f;    // 광선 두께 절반 (층을 바꾸면 피한다)

        public static Pattern NextPattern(Pattern previous) => previous == Pattern.Slam ? Pattern.Droplets : Pattern.Slam;

        public static int DropletCount(double roll) => DropletMin + Pick(roll, DropletMax - DropletMin + 1);

        public static bool BeamUnlocked(float health, float maxHealth) => maxHealth > 0f && health <= maxHealth * BeamHealthRatio;

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
