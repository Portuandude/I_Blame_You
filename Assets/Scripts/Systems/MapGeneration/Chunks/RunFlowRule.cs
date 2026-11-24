using System;

namespace IBlameYou.Systems
{
    // 한 판 = 스테이지 1→5 연속 (2026-11-24 사람 결정 A1·S1). 스테이지 사이에는 쉼터(RestRule)를 거친다.
    // 판 시드 하나로 스테이지마다 다른 맵 시드를 만든다: 스테이지 1은 판 시드 그대로라 예전과 같은 맵, 2+는 해시.
    // 같은 판 시드면 늘 같은 스테이지 맵이 나와 "쉼터에서 이어하기"가 같은 판을 다시 만든다.
    public static class RunFlowRule
    {
        public static bool IsFinalStage(int stage) => stage >= StageRoster.LastStage;

        // 다음 스테이지 번호 (마지막이면 그대로)
        public static int NextStage(int stage) => StageRoster.Clamp(Math.Max(StageRoster.FirstStage, stage) + 1);

        // 이 스테이지를 깨면 쉼터로 가는지 (마지막 스테이지·기능 테스트는 결과 창 → 도시)
        public static bool HasRest(int stage, bool featureTest) => !featureTest && !IsFinalStage(stage);

        public static int StageSeed(int runSeed, int stage)
        {
            if (stage <= StageRoster.FirstStage) return runSeed;
            unchecked
            {
                uint h = (uint)runSeed * 2246822519u ^ (uint)stage * 3266489917u ^ 0x85EBCA6Bu;
                h ^= h >> 16;
                h *= 2654435761u;
                h ^= h >> 13;
                return (int)(h % 999999u) + 1; // 1 ~ 999999 (새 판 시드와 같은 범위)
            }
        }
    }
}
