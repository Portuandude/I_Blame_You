namespace IBlameYou.Systems
{
    // 속성 enum과 상성(ElementAffinity)은 Formula/ElementCore.cs (엔진 없는 어셈블리, 테스트용).
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
}
