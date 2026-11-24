using System;

namespace IBlameYou.Systems
{
    // 스테이지 2 보스 코볼트 족장 + 와르그 둘 (가안, 플레이테스트로 맞춘다). 엔진 없는 어셈블리라 EditMode 테스트로 고정한다.
    // - 무리 하나가 쓰러질 때마다 남은 몸이 화가 난다: 이동 속도 ×EnrageSpeedStep, 돌격 사이 간격 ×CooldownStep^단계 (최소 MinCooldown).
    // - 족장은 돌격 사이에 그물을 던진다: NetInterval초마다, 체력이 절반 아래면 NetIntervalLow초마다.
    public static class KoboldChiefRules
    {
        public const float EnrageSpeedStep = 1.2f;
        public const float CooldownStep = 0.7f;
        public const float MinCooldown = 0.4f;
        public const float NetInterval = 6f;
        public const float NetIntervalLow = 4f;
        public const float NetMinRange = 3f;
        public const float NetMaxRange = 9f;

        public static float CooldownFor(int rage) =>
            Math.Max(MinCooldown, (float)Math.Pow(CooldownStep, Math.Max(0, rage)));

        public static float NetIntervalFor(float healthFraction) => healthFraction < 0.5f ? NetIntervalLow : NetInterval;

        public static bool InNetRange(float distance) => distance >= NetMinRange && distance <= NetMaxRange;
    }
}
