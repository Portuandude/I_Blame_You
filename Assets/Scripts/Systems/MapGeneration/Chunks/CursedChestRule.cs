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
