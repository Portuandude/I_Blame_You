namespace IBlameYou.Systems
{
    // 스탯 문서의 5속성. 순서는 키 배치(넘패드/상단 숫자키 1~5 = 불/물/바람/땅/무)와 같다.
    // 이 폴더는 UnityEngine 없는 별도 어셈블리(IBlameYou.Formula)라 EditMode 테스트로 공식을 검증한다.
    // 이름·색 같은 표시 정보는 Element.cs의 ElementInfo에 있다.
    public enum Element
    {
        Fire,
        Water,
        Wind,
        Earth,
        Neutral
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
