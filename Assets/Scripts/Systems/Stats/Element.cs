namespace IBlameYou.Systems
{
    // 스탯 문서의 5속성. 순서는 키 배치(넘패드/상단 숫자키 1~5 = 불/물/바람/땅/무)와 같다.
    public enum Element
    {
        Fire,
        Water,
        Wind,
        Earth,
        Neutral
    }

    public static class ElementInfo
    {
        public const int Count = 5;

        public static string KoreanName(Element element)
        {
            switch (element)
            {
                case Element.Fire: return "불";
                case Element.Water: return "물";
                case Element.Wind: return "바람";
                case Element.Earth: return "땅";
                default: return "무";
            }
        }

        public static UnityEngine.Color Color(Element element)
        {
            switch (element)
            {
                case Element.Fire: return new UnityEngine.Color(1f, 0.45f, 0.15f);
                case Element.Water: return new UnityEngine.Color(0.25f, 0.6f, 1f);
                case Element.Wind: return new UnityEngine.Color(0.55f, 1f, 0.7f);
                case Element.Earth: return new UnityEngine.Color(0.75f, 0.55f, 0.3f);
                default: return new UnityEngine.Color(0.85f, 0.75f, 1f);
            }
        }
    }

    // 속성 상성 (2026-10-16 확정): 물 > 불 > 무 > 바람 > 땅 > 물.
    // 공격 속성이 이기면 ×1.5, 지면 ×0.5, 관계가 없으면(같은 속성 포함) ×1.
    public static class ElementAffinity
    {
        public const float Advantage = 1.5f;
        public const float Disadvantage = 0.5f;

        // 각 속성이 이기는 상대.
        public static Element Beats(Element element)
        {
            switch (element)
            {
                case Element.Water: return Element.Fire;
                case Element.Fire: return Element.Neutral;
                case Element.Neutral: return Element.Wind;
                case Element.Wind: return Element.Earth;
                default: return Element.Water; // Earth
            }
        }

        public static float Multiplier(Element attack, Element defender)
        {
            if (Beats(attack) == defender) return Advantage;
            if (Beats(defender) == attack) return Disadvantage;
            return 1f;
        }
    }
}
