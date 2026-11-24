using System;

namespace IBlameYou.Systems
{
    // 인과율 구간 (Docs/DesignDecisionsTodo.md B0·B1, OpenDecisions 4장). 낮음 0~25 · 보통 25~75 · 높음 75~ · 임계치 100에 닿음.
    public enum CausalityBand
    {
        Low,
        Normal,
        High,
        Overflow
    }

    // 숨은 능력치 "선악도"·"인과율" 규칙 (2026-11-24 사람 결정). 화면에 숫자도 힌트도 없다 — 드러나는 것은 게임 안 결과뿐.
    // - 선악도: 아루(A)·벨(B) 각자. 시작 StartAlignment, 0 이하 = 악, 양수 = 선, 한도 없음. NPC는 둘의 합을 본다 (OpenDecisions).
    // - 인과율: 한 몸이라 공용 0~100. 쓰러져 도시에서 다시 깨어날 때(부활)마다 +PerRevival (가안, 플레이 테스트 후 확정).
    //   개발자 모드·기능 테스트 모드 부활은 세지 않는다 (SaveData.RecordRevival). 부활이 적을수록 좋은 엔딩.
    //   오를수록 대사·배경·음악이 어두워진다 (Gloom, 아직 연출에 쓰는 곳 없음 — 아트·연출 사람 확인 뒤).
    public static class HiddenStatsRule
    {
        public const int StartAlignment = 30;
        public const int PerRevival = 5;
        public const int MaxCausality = 100;
        public const int NormalFrom = 25;
        public const int HighFrom = 75;

        public static int Causality(int revivals) => Math.Min(MaxCausality, Math.Max(0, revivals) * PerRevival);

        public static CausalityBand Band(int causality)
        {
            if (causality >= MaxCausality) return CausalityBand.Overflow;
            if (causality >= HighFrom) return CausalityBand.High;
            return causality >= NormalFrom ? CausalityBand.Normal : CausalityBand.Low;
        }

        // 어두워지는 정도 0~1 (인과율 / 최대). 배경 색·음악·대사를 어둡게 할 때 쓴다.
        public static float Gloom(int causality) => Math.Min(1f, Math.Max(0f, causality / (float)MaxCausality));

        public static bool IsGood(int alignment) => alignment > 0;

        // NPC가 보는 선악도: 아루·벨 두 값의 합 (2026-11-24 사람 결정)
        public static int NpcView(int alignmentA, int alignmentB) => alignmentA + alignmentB;
    }
}
