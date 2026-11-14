namespace IBlameYou.Systems
{
    // 방 이벤트 "저주받은 상자" (2026-11-14 사람 결정, ContentIdeas 이벤트 2): 어떤 이동 방 가운데에 보라색 상자가 있다.
    // 열면(닿으면) 정예가 깨어나고, 정예를 쓰러뜨리면 유물 상자(후보 OfferSize개 중 하나)가 된다.
    // 시드 + 방 번호 해시로 정해 지형 난수를 쓰지 않는다. 보물 고블린 방과 겹치면 놓지 않는다 (ChunkStreamer). EditMode 테스트로 고정한다.
    public static class CursedChestRule
    {
        public const float MinDifficulty = 0.3f;
        public const int ChancePercent = 25;
        public const int OfferSize = 2; // 유물 2개 중 하나 (네잎 클로버면 +1)

        public static bool IsCursedChestRoom(int seed, int roomIndex, float difficulty, bool travelRoom)
        {
            if (!travelRoom || difficulty < MinDifficulty) return false;
            return Hash(seed, roomIndex) % 100u < (uint)ChancePercent;
        }

        // 깨어나는 정예: 짝수면 어미 슬라임, 홀수면 고블린 대전사
        public static StageEnemy EliteFor(int seed, int roomIndex) =>
            (Hash(seed, roomIndex) >> 8) % 2u == 0u ? StageEnemy.MotherSlime : StageEnemy.GoblinWarrior;

        // 변형 "쫄몹 무리" (2026-11-14 자동 실행): 저주 상자의 1/3은 정예 대신 쫄몹 SwarmSize마리가 상자 양옆에서 깨어난다.
        // 모두 쓰러뜨려야 저주가 풀린다. 정예 종류와 다른 해시 비트를 써서 정예 비율은 그대로 반반.
        public const int SwarmSize = 4;
        public static readonly int[] SwarmOffsets = { -5, -3, 3, 5 }; // 상자 열에서 떨어진 칸 (양옆 둘씩)

        public static bool IsSwarm(int seed, int roomIndex) => (Hash(seed, roomIndex) >> 16) % 3u == 0u;

        // 무리 구성: 슬라임·고블린 둘씩, 난이도 높은 방이 아니어도 견딜 만하게 궁수는 하나만 (짝수 해시면 고블린 하나를 궁수로)
        public static StageEnemy[] SwarmFor(int seed, int roomIndex)
        {
            bool archer = (Hash(seed, roomIndex) >> 20) % 2u == 0u;
            return new[] { StageEnemy.Slime, StageEnemy.Goblin, archer ? StageEnemy.GoblinArcher : StageEnemy.Goblin, StageEnemy.Slime };
        }

        private static uint Hash(int seed, int roomIndex)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u ^ (uint)(roomIndex + 3) * 2654435769u;
                h ^= h >> 15;
                h *= 2246822507u;
                h ^= h >> 13;
                return h;
            }
        }
    }
}
