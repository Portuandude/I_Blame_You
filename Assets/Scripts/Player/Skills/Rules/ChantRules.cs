namespace IBlameYou.Player
{
    // 영창 문장 규칙 (순수 C#, EditMode 테스트 대상).
    // 순풍(아카데미 바람 마법, Docs/CityDesign.md 5절 — 바람 = 마법 속도): 버프 동안 영창 문장이 쉼표 앞 부르는 말 + "!"로 짧아진다.
    //   "불꽃이여, 작렬하라!" → "불꽃이여!". 쉼표가 없거나 이미 짧으면 그대로.
    public static class ChantRules
    {
        public static string Short(string chant)
        {
            if (string.IsNullOrEmpty(chant)) return chant;
            int comma = chant.IndexOf(',');
            if (comma <= 0) return chant;
            return chant.Substring(0, comma).TrimEnd() + "!";
        }

        public static string For(string chant, bool tailwind) => tailwind ? Short(chant) : chant;
    }
}
