namespace IBlameYou.Systems
{
    // 적 배치 변형 "매복" (2026-11-14 사람 결정, ContentIdeas 적 1): 일반 전투 방(ChunkKind.Arena, 중간보스 방 제외)에 들어가 문이 닫히면
    // 적이 바닥에서 소환되는 대신 천장 쪽에서 떨어진다. 떨어지기 전에 바닥 경고 타원과 천장 먼지로 알려 준다 (ArenaZone).
    // 지형 난수를 쓰지 않고 시드 + 방 번호 해시로 정해서 기존 시드의 지형·적은 그대로다. UnityEngine 없이 EditMode 테스트로 고정한다.
    public static class AmbushRule
    {
        public const float MinDifficulty = 0.2f; // 시작 방 근처 첫 전투는 보통 등장으로 (처음 보는 사람이 놀라지 않게)
        public const int ChancePercent = 50;     // 조건에 맞는 전투 방의 절반
        public const int ClearGold = 8;          // 매복 방을 돌파하면 받는 골드 (2026-11-14, 일반 적 1~4 몇 마리 몫)

        // 빠른 돌파 (2026-11-14 자동 실행): 문이 닫힌 뒤 FastClearTime초 안에 돌파하면 FastClearGold를 더 준다.
        public const float FastClearTime = 20f;
        public const int FastClearGold = 4;

        public static bool IsFastClear(float seconds) => seconds <= FastClearTime;

        public static int ClearGoldFor(float seconds) => ClearGold + (IsFastClear(seconds) ? FastClearGold : 0);

        // 변형 "땅속 매복" (2026-11-14 자동 실행): 매복 방의 1/3은 적이 천장 대신 바닥에서 튀어나온다 (벽 위 궁수 방 제외는 ChunkStreamer).
        // IsAmbush와 다른 해시라 어떤 방이 매복인지는 그대로다.
        public const int BurrowPercent = 33;

        public static bool IsBurrow(int seed, int roomIndex)
        {
            unchecked
            {
                uint h = (uint)seed * 3266489917u ^ (uint)(roomIndex + 7) * 668265263u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                return h % 100u < (uint)BurrowPercent;
            }
        }

        // 섞인 매복 (2026-11-14 자동 실행): 난이도 MixedDifficulty 이상 방의 땅속 매복은 적이 번갈아(짝수 순번 땅속, 홀수 순번 천장) 나온다.
        public const float MixedDifficulty = 0.7f;

        public static bool IsMixed(int seed, int roomIndex, float difficulty) =>
            difficulty >= MixedDifficulty && IsBurrow(seed, roomIndex);

        // 섞인 매복에서 spawnIndex번째 적이 땅속에서 나오는지 (아니면 천장에서 떨어진다). 섞이지 않은 땅속 매복이면 모두 땅속.
        public static bool BurrowsAt(bool burrow, bool mixed, int spawnIndex) => burrow && (!mixed || spawnIndex % 2 == 0);

        public static bool IsAmbush(int seed, int roomIndex, float difficulty, bool plainArena)
        {
            if (!plainArena || difficulty < MinDifficulty) return false;
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)(roomIndex + 1) * 40503u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return h % 100u < (uint)ChancePercent;
            }
        }
    }
}
