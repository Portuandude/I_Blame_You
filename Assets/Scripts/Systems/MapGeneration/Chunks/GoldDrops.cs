using System;

namespace IBlameYou.Systems
{
    // 몬스터 골드 드롭 표 (2026-11-12 사람 확정). 골드는 그 판 안에서만 쓰고 다음 판으로 넘어가지 않는다.
    // 스테이지마다 표를 바꿀 수 있게 데이터(ForStage)로 둔다. UnityEngine 없이 EditMode 테스트로 확률·범위를 고정한다.
    public enum EnemyRank
    {
        Normal,   // 일반 몹 (슬라임·고블린·궁수, 분열·소환된 작은 몹 포함)
        Elite,    // 정예 (어미 슬라임·고블린 전사)
        MidBoss,  // 중간보스 (쌍둥이 슬라임 기사: 둘이 합쳐 이 범위가 되도록 기사 하나당 절반씩)
        Boss      // 최종보스 (슬라임 킹: 2페이즈에 둘로 나뉘어 따로 죽으므로 한 마리당 절반씩)
    }

    public struct GoldDrop
    {
        public float Chance; // 0~1
        public int Min;      // 이상
        public int Max;      // 이하

        public GoldDrop(float chance, int min, int max)
        {
            Chance = chance;
            Min = min;
            Max = max;
        }
    }

    public class GoldDropTable
    {
        public GoldDrop Normal, Elite, MidBoss, Boss;
        public int MidBossParts = 2; // 중간보스가 몇 마리로 나뉘어 나오는지 (쌍둥이 기사 = 2)
        public int BossParts = 2;    // 최종보스가 몇 마리로 나뉘어 죽는지 (슬라임 킹 분열 = 2)

        // 스테이지 1: 일반 50% 1~4, 정예 100% 10~20, 중간보스 100% 50~100, 최종보스 100% 100~200
        public static readonly GoldDropTable Stage1 = new GoldDropTable
        {
            Normal = new GoldDrop(0.5f, 1, 4),
            Elite = new GoldDrop(1f, 10, 20),
            MidBoss = new GoldDrop(1f, 50, 100),
            Boss = new GoldDrop(1f, 100, 200),
        };

        // 아직 스테이지 1만 있다. 새 스테이지 표는 여기에 더한다.
        public static GoldDropTable ForStage(int stage) => Stage1;

        public GoldDrop For(EnemyRank rank)
        {
            switch (rank)
            {
                case EnemyRank.Elite: return Elite;
                case EnemyRank.MidBoss: return MidBoss;
                case EnemyRank.Boss: return Boss;
                default: return Normal;
            }
        }

        // 이 적이 떨어뜨릴 골드 (0이면 안 떨어뜨림). chanceRoll·amountRoll은 0 이상 1 미만 난수.
        // 중간보스·최종보스는 한 마리당 범위를 MidBossParts·BossParts로 나눈다 (둘의 합이 원래 범위 안).
        public int Roll(EnemyRank rank, double chanceRoll, double amountRoll)
        {
            var drop = For(rank);
            if (chanceRoll >= drop.Chance) return 0;
            int min = drop.Min, max = drop.Max;
            int parts = rank == EnemyRank.MidBoss ? MidBossParts : rank == EnemyRank.Boss ? BossParts : 1;
            if (parts > 1)
            {
                min = (min + parts - 1) / parts;
                max /= parts;
            }
            if (max < min) max = min;
            int span = max - min + 1;
            int pick = (int)Math.Floor(amountRoll * span);
            if (pick >= span) pick = span - 1;
            if (pick < 0) pick = 0;
            return min + pick;
        }

        public int Roll(EnemyRank rank, Random rng) => Roll(rank, rng.NextDouble(), rng.NextDouble());

        // 동전 무더기 (위험한 길 끝 보상): 난이도 0 → 3골드, 1 → 6골드. 일반 몹 한 마리(평균 1.25)보다 넉넉하고 정예(10~20)보다는 적게.
        public const int CoinPileMin = 3;
        public const int CoinPileMax = 6;

        public static int CoinPileGold(float difficulty)
        {
            if (difficulty < 0f) difficulty = 0f;
            if (difficulty > 1f) difficulty = 1f;
            return CoinPileMin + (int)Math.Round((CoinPileMax - CoinPileMin) * difficulty, MidpointRounding.AwayFromZero);
        }

        // 골드 양을 동전 몇 개로 나눠 뿌릴지 (동전 하나 = 1·5·10·50 골드). 화면이 동전으로 덮이지 않게 큰 단위부터.
        public static readonly int[] CoinValues = { 50, 10, 5, 1 };

        public static int[] SplitCoins(int amount)
        {
            var list = new System.Collections.Generic.List<int>();
            foreach (int v in CoinValues)
                while (amount >= v && list.Count < 24)
                {
                    list.Add(v);
                    amount -= v;
                }
            if (amount > 0 && list.Count > 0) list[list.Count - 1] += amount; // 24개를 넘는 나머지는 마지막 동전에 얹는다
            return list.ToArray();
        }
    }
}
