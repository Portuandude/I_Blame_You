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

        public static Who Judge(float wastedA, float wastedB)
        {
            int a = (int)Math.Floor(Math.Max(0f, wastedA));
            int b = (int)Math.Floor(Math.Max(0f, wastedB));
            if (a == 0 && b == 0) return Who.Nobody;
            int diff = Math.Abs(a - b);
            if (diff <= EvenMargin || diff * 10 <= Math.Max(a, b)) return Who.Even;
            return a > b ? Who.A : Who.B;
        }

        // 예: "판정: B 탓! (A보다 마나 32 더 날림)" / "판정: 반반 — 사이좋게 서로 탓하기" / "판정: 아무도 마나를 날리지 않았다!".
        public static string Describe(float wastedA, float wastedB)
        {
            int a = (int)Math.Floor(Math.Max(0f, wastedA));
            int b = (int)Math.Floor(Math.Max(0f, wastedB));
            switch (Judge(wastedA, wastedB))
            {
                case Who.A: return $"판정: A 탓! (B보다 마나 {a - b} 더 날림)";
                case Who.B: return $"판정: B 탓! (A보다 마나 {b - a} 더 날림)";
                case Who.Even: return "판정: 반반 — 사이좋게 서로 탓하기";
                default: return "판정: 아무도 마나를 날리지 않았다!";
            }
        }
    }
}
