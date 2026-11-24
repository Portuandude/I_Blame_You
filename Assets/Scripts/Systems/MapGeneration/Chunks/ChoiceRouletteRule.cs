using System;

namespace IBlameYou.Systems
{
    // A·B 선택지 + 룰렛 (2026-11-24 사람 결정, Docs/OpenDecisions.md 2장·DesignDecisionsTodo B0 2번).
    // 아루(A)·벨(B)에게 같은 선택지를 주고 동시에 몰래 고른다 (제한 시간 없음, R2·R3).
    // 같은 것을 골랐으면 그대로, 다르면 50:50 룰렛 (R1, 숨은 값으로 기울이지 않음). 이긴 쪽 보너스 없음 (R4).
    // 선악도는 룰렛 결과가 아니라 각자 고른 선택지대로 오른다.
    public static class ChoiceRouletteRule
    {
        public const float SpinSeconds = 1.6f; // 룰렛이 도는 시간 (HUD 연출)

        // a, b: 아루·벨이 고른 선택지 번호. roll: 0~1 난수. 실제로 일어나는 선택지 번호를 돌려준다.
        public static int Resolve(int a, int b, double roll, out bool spun)
        {
            spun = a != b;
            if (!spun) return a;
            return roll < 0.5 ? a : b;
        }

        // 고른 선택지의 선악도 변화 (선택지마다 정한 값, 없으면 0)
        public static int AlignmentDelta(int[] alignment, int choice) =>
            alignment != null && choice >= 0 && choice < alignment.Length ? alignment[choice] : 0;

        // 룰렛 바늘이 지금 가리키는 쪽 (t: 돈 시간 0~SpinSeconds). 처음엔 빨리, 끝으로 갈수록 느리게 번갈아 깜빡이다 결과에서 멈춘다.
        // 0 = a 쪽, 1 = b 쪽.
        public static int Pointer(float t, bool landsOnB)
        {
            if (t >= SpinSeconds) return landsOnB ? 1 : 0;
            float u = Math.Max(0f, t) / SpinSeconds;
            int flips = Math.Min(14, (int)(14.99f * (1f - (1f - u) * (1f - u)))); // 느려지며 14번 바뀌고, 마지막 1/4쯤은 결과 쪽에 멈춰 있다
            int target = landsOnB ? 1 : 0;
            return ((14 - flips) % 2 == 0) ? target : 1 - target; // 마지막(flips = 14)에 결과 쪽
        }
    }
}
