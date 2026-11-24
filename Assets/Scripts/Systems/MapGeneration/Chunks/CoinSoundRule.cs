using System;

namespace IBlameYou.Systems
{
    // 동전 줍는 소리 크기 (2026-11-24 사람 결정 D4: "골드 줍는 소리가 겹치면 겹친 수에 비례해 작게").
    // 동전 여러 개가 짧은 사이(Window초)에 한꺼번에 닿으면, 그 사이 닿은 개수(겹친 수)로 기본 크기를 나눈다.
    // 너무 작아져 안 들리지 않게 MinRatio 아래로는 줄이지 않는다. UnityEngine 없이 EditMode 테스트로 고정한다.
    public static class CoinSoundRule
    {
        public const float Window = 0.25f;   // 이 시간 안에 닿은 동전을 "겹쳤다"고 센다
        public const float MinRatio = 0.15f; // 기본 크기의 15% 아래로는 줄이지 않는다

        // overlapping: 이 동전을 포함해 Window 안에 닿은 동전 수 (1이면 기본 크기 그대로).
        public static float Volume(float baseVolume, int overlapping)
        {
            int n = Math.Max(1, overlapping);
            return baseVolume * Math.Max(MinRatio, 1f / n);
        }
    }
}
