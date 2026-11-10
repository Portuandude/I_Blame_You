namespace IBlameYou.Systems
{
    // 한 판 동안 속성별 마법 사용 횟수에서 "최애 마법"을 고른다 (결과 창 한 줄). UnityEngine 없이 EditMode 테스트로 고정한다.
    // counts·names는 속성 순서(불/물/바람/땅/무)와 같은 길이. 횟수가 같으면 앞 속성.
    public static class RunTally
    {
        // 예: "B 최애 마법: 불(12회)". 모두 0이면 빈 문자열.
        public static string Favorite(string who, string kind, int[] counts, string[] names)
        {
            if (counts == null || names == null) return "";
            int best = -1;
            for (int i = 0; i < counts.Length && i < names.Length; i++)
                if (counts[i] > 0 && (best < 0 || counts[i] > counts[best])) best = i;
            return best < 0 ? "" : $"{who} 최애 {kind}: {names[best]}({counts[best]}회)";
        }

        // 결과 창 한 줄: B 공격 마법과 A 방어막 최애를 " · "로 잇는다. 둘 다 없으면 빈 문자열.
        public static string Line(int[] spells, int[] shields, string[] names)
        {
            string b = Favorite("B", "마법", spells, names);
            string a = Favorite("A", "방어막", shields, names);
            if (b.Length == 0) return a;
            return a.Length == 0 ? b : b + " · " + a;
        }
    }
}
