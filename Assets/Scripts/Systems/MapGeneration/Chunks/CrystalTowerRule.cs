namespace IBlameYou.Systems
{
    // 수정 탑 영구 능력치 (Docs/CityDesign.md 4절, 도시 3단계 2026-11-15). 영혼석을 바쳐 항목마다 5단계까지.
    // "강해지기보다 넓어지기": 단계와 폭을 작게. 값은 단계마다 같은 만큼, 판 시작 때 플레이어에 더한다 (기능 테스트 제외).
    // 넷째 칸은 원래 "방어막 지속"이었다. 방어막이 빠지며(2026-11-16) "마법진 여유"(P2 마법진 허용 폭)로 바뀌었다. 저장 키가 이름이라 예전 단계는 이어지지 않는다.
    // 2026-11-24 사람 결정(OpenDecisions G1): 순간이동 강화(순간이동 마나 -4%/단계)·점프력 강화(점프 힘 +2%/단계)를 더해 6항목. 수치는 가안.
    public enum TowerStat { MaxHealth, MaxMana, ManaRegen, CircleEase, Teleport, Jump }

    public static class CrystalTowerRule
    {
        public const int MaxLevel = 5;
        public const int StatCount = 6;
        public const int TotalLevels = StatCount * MaxLevel; // 수정의 금 수 (CityTalkRule.Cracks)

        private static readonly int[] CostsSmall = { 20, 35, 55, 80, 110 }; // 최대 체력·최대 마나
        private static readonly int[] CostsLarge = { 25, 40, 60, 85, 115 }; // 마나 재생·마법진 여유·순간이동·점프력

        // 지금 단계(0~4)에서 다음 단계로 올리는 영혼석. 이미 최대면 -1.
        public static int Cost(TowerStat stat, int level)
        {
            if (level < 0 || level >= MaxLevel) return -1;
            return (stat == TowerStat.MaxHealth || stat == TowerStat.MaxMana ? CostsSmall : CostsLarge)[level];
        }

        // 한 단계 값: 체력·마나 +4, 마나 재생 +3% (배율 0.03), 마법진 허용 폭 +4% (배율 0.04),
        // 순간이동 마나 -4% (배율 0.04를 뺌, 5단계 -20%), 점프 힘 +2% (배율 0.02, 5단계 +10% → 높이 약 +21%).
        public static float PerLevel(TowerStat stat)
        {
            switch (stat)
            {
                case TowerStat.MaxHealth: return 4f;
                case TowerStat.MaxMana: return 4f;
                case TowerStat.ManaRegen: return 0.03f;
                case TowerStat.Teleport: return 0.04f;
                case TowerStat.Jump: return 0.02f;
                default: return 0.04f;
            }
        }

        // 판 시작 때 곱하는 배율: 순간이동 마나 배율(1 - 보너스), 점프 힘 배율(1 + 보너스).
        public static float TeleportCostMultiplier(int level) => 1f - Bonus(TowerStat.Teleport, level);
        public static float JumpMultiplier(int level) => 1f + Bonus(TowerStat.Jump, level);

        public static float Bonus(TowerStat stat, int level) => PerLevel(stat) * Clamp(level);

        public static string Name(TowerStat stat)
        {
            switch (stat)
            {
                case TowerStat.MaxHealth: return "최대 체력";
                case TowerStat.MaxMana: return "최대 마나";
                case TowerStat.ManaRegen: return "마나 재생";
                case TowerStat.Teleport: return "순간이동 마나";
                case TowerStat.Jump: return "점프력";
                default: return "마법진 여유";
            }
        }

        // 화면 글: "+12", "+9%", 순간이동 마나는 "-8%"
        public static string BonusText(TowerStat stat, int level)
        {
            float bonus = Bonus(stat, level);
            switch (stat)
            {
                case TowerStat.Teleport: return $"-{System.Math.Round(bonus * 100f)}%";
                case TowerStat.ManaRegen:
                case TowerStat.Jump:
                case TowerStat.CircleEase: return $"+{System.Math.Round(bonus * 100f)}%";
                default: return $"+{System.Math.Round(bonus)}";
            }
        }

        public static int TotalCost()
        {
            int sum = 0;
            for (int s = 0; s < StatCount; s++)
                for (int l = 0; l < MaxLevel; l++) sum += Cost((TowerStat)s, l);
            return sum;
        }

        // 영혼석 dust로 stat을 level에서 한 단계 올릴 수 있는지
        public static bool CanBuy(TowerStat stat, int level, int dust)
        {
            int cost = Cost(stat, level);
            return cost > 0 && dust >= cost;
        }

        private static int Clamp(int level) => level < 0 ? 0 : level > MaxLevel ? MaxLevel : level;
    }
}
