namespace IBlameYou.Systems
{
    // 한 판 동안 속성별 마법 사용 횟수에서 "최애 마법"을 고른다 (결과 창 한 줄). UnityEngine 없이 EditMode 테스트로 고정한다.
    // counts·names는 속성 순서(불/물/바람/땅/무)와 같은 길이. 횟수가 같으면 앞 속성.
    public static class RunTally
    {
        // 예: "B 최애 마법: 불(12회)". 모두 0이면 빈 문자열.
        // colors(선택): 속성별 "RRGGBB" 16진 색. 주면 속성 이름만 rich text 색 태그로 감싼다 (GUIStyle.richText 필요).
        public static string Favorite(string who, string kind, int[] counts, string[] names, string[] colors = null)
        {
            if (counts == null || names == null) return "";
            int best = -1;
            for (int i = 0; i < counts.Length && i < names.Length; i++)
                if (counts[i] > 0 && (best < 0 || counts[i] > counts[best])) best = i;
            if (best < 0) return "";
            string name = names[best];
            if (colors != null && best < colors.Length && !string.IsNullOrEmpty(colors[best]))
                name = $"<color=#{colors[best]}>{name}</color>";
            return $"{who} 최애 {kind}: {name}({counts[best]}회)";
        }

        // 결과 창 한 줄: B 공격 마법 최애. 없으면 빈 문자열. (A 방어막 최애는 방어막과 함께 2026-11-16 빠졌다)
        public static string Line(int[] spells, string[] names, string[] colors = null) => Favorite("B", "마법", spells, names, colors);
    }
}
