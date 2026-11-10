using System;

namespace IBlameYou.Systems
{
    // 결과 창의 "누구 탓?" 판정 (게임 이름 그대로: 마나를 더 많이 날린 쪽 탓).
    // 날린 마나 = 미니게임 실패로 헛되이 쓴 마나 (ManaSystem.WastedBy). UnityEngine 없이 돌아서 EditMode 테스트로 고정한다.
    public static class BlameVerdict
    {
        public enum Who { Nobody, A, B, Even }

        // 차이가 이 값 이하이거나 큰 쪽의 10% 이하이면 반반으로 본다 (조금 더 날렸다고 탓하면 억울하니까).
        public const int EvenMargin = 5;

        // 결과 창 "날린 마나 N"({:0}, 0.5는 올림)과 같은 정수로 맞춘다.
        private static int Whole(float wasted) => (int)Math.Round(Math.Max(0f, wasted), MidpointRounding.AwayFromZero);

        public static Who Judge(float wastedA, float wastedB)
        {
            int a = Whole(wastedA);
            int b = Whole(wastedB);
            if (a == 0 && b == 0) return Who.Nobody;
            int diff = Math.Abs(a - b);
            if (diff <= EvenMargin || diff * 10 <= Math.Max(a, b)) return Who.Even;
            return a > b ? Who.A : Who.B;
        }

        // 예: "판정: B 탓! (A보다 마나 32 더 날림)" / "판정: 반반 — 사이좋게 서로 탓하기" / "판정: 아무도 마나를 날리지 않았다!".
        public static string Describe(float wastedA, float wastedB)
        {
            int a = Whole(wastedA);
            int b = Whole(wastedB);
            switch (Judge(wastedA, wastedB))
            {
                case Who.A: return $"판정: A 탓! (B보다 마나 {a - b} 더 날림)";
                case Who.B: return $"판정: B 탓! (A보다 마나 {b - a} 더 날림)";
                case Who.Even: return "판정: 반반 — 사이좋게 서로 탓하기";
                default: return "판정: 아무도 마나를 날리지 않았다!";
            }
        }

        // 최근 탓 기록 (시작 화면에서 서로 놀릴 거리). 저장 문자열은 판정 글자(A·B·E 반반·N 없음)를 새 것부터 이어 붙인 것, RecentMax개.
        public const int RecentMax = 10;

        public static string AddRecent(string list, Who who)
        {
            char mark = who == Who.A ? 'A' : who == Who.B ? 'B' : who == Who.Even ? 'E' : 'N';
            var sb = new System.Text.StringBuilder().Append(mark);
            if (!string.IsNullOrEmpty(list))
                foreach (char c in list)
                {
                    if (sb.Length >= RecentMax) break;
                    if (c == 'A' || c == 'B' || c == 'E' || c == 'N') sb.Append(c);
                }
            return sb.ToString();
        }

        // 예: "최근 10판 탓: A 4 · B 5 · 반반 1". 반반·없음은 0이면 뺀다. 기록이 없으면 빈 문자열.
        public static string RecentSummary(string list)
        {
            int a = 0, b = 0, even = 0, none = 0, count = 0;
            if (!string.IsNullOrEmpty(list))
                foreach (char c in list)
                {
                    if (count >= RecentMax) break;
                    switch (c)
                    {
                        case 'A': a++; break;
                        case 'B': b++; break;
                        case 'E': even++; break;
                        case 'N': none++; break;
                        default: continue;
                    }
                    count++;
                }
            if (count == 0) return "";
            string text = $"최근 {count}판 탓: A {a} · B {b}";
            if (even > 0) text += $" · 반반 {even}";
            if (none > 0) text += $" · 탓 없음 {none}";
            return text;
        }
    }
}
