using System;

namespace IBlameYou.Systems
{
    // 낭떠러지 피해 (2026-11-24 사람 결정): 낙사로는 쓰러지지 않는다. 피해가 체력을 다 깎을 만큼이면 체력 1을 남기고,
    // 버티기(불사조 깃털·생명 20)도 쓰지 않는다. 기본 피해는 최대 체력의 15% (FallRecovery.DamageRatio, 깃털 부적이 낮춤).
    public static class FallDamageRule
    {
        public const float MinHealthLeft = 1f;

        // 실제로 깎을 피해 (0 이상). 남은 체력이 1 이하이면 0.
        public static float Damage(float current, float max, float ratio)
        {
            if (!(current > 0f) || !(max > 0f) || !(ratio > 0f)) return 0f;
            float full = max * ratio;
            float room = current - MinHealthLeft;
            if (room <= 0f) return 0f;
            return Math.Min(full, room);
        }

        // 이 피해가 체력 1에서 멈췄는지 (알림 글을 고르는 데 쓴다).
        public static bool Capped(float current, float max, float ratio)
        {
            if (!(current > 0f) || !(max > 0f) || !(ratio > 0f)) return false;
            return max * ratio > current - MinHealthLeft + 0.0001f; // 15% 같은 소수 오차로 딱 1이 남을 때를 "멈춤"으로 보지 않게
        }
    }
}
