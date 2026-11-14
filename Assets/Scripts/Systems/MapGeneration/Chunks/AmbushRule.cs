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
