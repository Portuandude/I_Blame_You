using System;

namespace IBlameYou.Systems
{
    // 코볼트 그물 속박 (2026-11-24 사람 결정 OpenDecisions F1): 그물에 맞으면 A(이동 담당)만 Duration초 묶인다.
    // 묶인 동안 A가 키를 한 번 누를 때마다 PerPress초 줄어든다. B(공격)는 그대로 공격할 수 있다.
    // 이미 묶인 채 다시 맞으면 시간을 되돌리지 않는다 (가안 — 연달아 맞아 못 빠져나가는 것을 막는다).
    public static class NetSnareRules
    {
        public const float Duration = 5f;
        public const float PerPress = 0.5f;

        public static float Remaining(float elapsed, int presses) =>
            Math.Max(0f, Duration - Math.Max(0f, elapsed) - PerPress * Math.Max(0, presses));

        public static bool Free(float elapsed, int presses) => Remaining(elapsed, presses) <= 0f;

        // 남은 몫 0~1 (그물 그림 투명도·크기에 쓴다)
        public static float Fraction(float elapsed, int presses) => Remaining(elapsed, presses) / Duration;
    }
}
