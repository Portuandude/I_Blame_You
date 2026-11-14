namespace IBlameYou.Systems
{
    // 마음 가루 (2026-11-14, Docs/CityDesign.md 3절): 판 밖 도시에서 쓰는 화폐. 쓰러져도 모두 남는다.
    // 양은 가안 — 한 판 평균 40~70. 중간보스(쌍둥이 기사)·최종보스(슬라임 킹 2페이즈 둘)는 나뉘어 죽으므로 한 마리 몫.
    public static class HeartDustRule
    {
        public const int Normal = 1;
        public const int Elite = 5;
        public const int MidBossPart = 8;  // 기사 둘 = 16 (문서 15 안팎)
        public const int BossPart = 20;    // 반쪽 둘 = 40
        public const int Puzzle = 5;       // 퍼즐 방 하나 풀기 (등불 순서·거울 룬·방어막 다리)
        public const int StageClear = 20;

        public static int ForKill(EnemyRank rank)
        {
            switch (rank)
            {
                case EnemyRank.Elite: return Elite;
                case EnemyRank.MidBoss: return MidBossPart;
                case EnemyRank.Boss: return BossPart;
                default: return Normal;
            }
        }
    }
}
