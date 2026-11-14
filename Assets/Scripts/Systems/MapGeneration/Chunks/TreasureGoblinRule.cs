using System;

namespace IBlameYou.Systems
{
    // 방 이벤트 "보물 고블린" (2026-11-14 사람 결정, ContentIdeas 이벤트 1): 어떤 이동 방에 처음 들어가면 금화 자루를 멘 고블린이 나타나 도망친다.
    // 10초 안에 잡으면 골드 Gold (일반 적 1~4, 정예 10~20보다 훨씬 많이). 어느 방인지는 시드 + 방 번호 해시로 정해 지형 난수를 쓰지 않는다.
    // UnityEngine 없이 EditMode 테스트로 고정한다. 놓는 곳·도망 행동은 ChunkStreamer·TreasureGoblinController.
    public static class TreasureGoblinRule
    {
        public const float MinDifficulty = 0.15f; // 시작 방 바로 옆은 빼고
        public const int ChancePercent = 30;      // 조건에 맞는 이동 방의 30% (스테이지당 1~2번쯤)
        public const int MinGold = 40;
        public const int MaxGold = 80;

        public static bool IsTreasureGoblinRoom(int seed, int roomIndex, float difficulty, bool travelRoom)
        {
            if (!travelRoom || difficulty < MinDifficulty) return false;
            unchecked
            {
                uint h = (uint)seed * 2246822519u ^ (uint)(roomIndex + 7) * 3266489917u;
                h ^= h >> 13;
                h *= 668265263u;
                h ^= h >> 16;
                return h % 100u < (uint)ChancePercent;
            }
        }

        // 맞을 때마다 흘리는 금화 (2026-11-14): HitGold씩, 한 마리에서 MaxHitDrops번까지 (잡았을 때 Gold와 따로)
        public const int HitGold = 2;
        public const int MaxHitDrops = 5;

        // roll: 0 이상 1 미만 난수. MinGold~MaxGold (양 끝 포함).
        public static int Gold(float roll)
        {
            float r = Math.Max(0f, Math.Min(0.9999f, roll));
            return MinGold + (int)(r * (MaxGold - MinGold + 1));
        }
    }
}
