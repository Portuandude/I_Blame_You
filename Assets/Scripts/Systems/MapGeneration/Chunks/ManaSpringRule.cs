using System;

namespace IBlameYou.Systems
{
    // 마나 샘 방 (2026-11-24 사람 결정, Docs/DesignDecisionsTodo.md·OpenDecisions P2): 스테이지마다 0~1개.
    // 판에서 처음 들어가면 RegenSeconds초 동안 마나 재생 +RegenBonus (유물 샘물 반지 +10%와 더해져 중첩된다).
    // 다시 들어가도 다시 걸리지 않는다 (ChunkStreamer.EnterRoom의 처음 방문에서만).
    public static class ManaSpringRule
    {
        public const float RegenBonus = 0.1f;
        public const float RegenSeconds = 180f;
        public const int Chance = 2; // 1/Chance 확률로 그 판에 마나 샘 방이 하나 (0~1개)

        // 지금(now) 받는 재생 보너스. until은 샘 효과가 끝나는 시각 (안 걸렸으면 음수).
        public static float Bonus(float now, float until) => now < until ? RegenBonus : 0f;

        // 남은 초 (HUD 표시용, 0 아래로 내려가지 않음)
        public static float Remaining(float now, float until) => Math.Max(0f, until - now);
    }
}
