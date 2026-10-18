namespace IBlameYou.Player
{
    // 레벨업 특성 (던그리드의 특성 방식): 레벨이 오를 때마다 특성 포인트 1을 받아 다섯 갈래 중 하나에 찍는다.
    // 한 포인트마다 작은 능력치가 오르고, 5 / 10 / 20 포인트에서 특별한 효과가 열린다. 갈래마다 최대 20.
    // 포인트는 A와 B가 함께 쓰는 하나의 주머니라서, 누가 어디에 찍을지도 서로 탓할 거리다.
    public enum Trait
    {
        Wrath,     // 분노 (B 공격)
        Swiftness, // 신속 (A 이동)
        Patience,  // 인내 (A 방어)
        Mystic,    // 신비 (공유 마나)
        Vitality   // 생명 (공유 체력)
    }

    public static class TraitTree
    {
        public const int MaxRank = 20;
        public static readonly int[] Milestones = { 5, 10, 20 };
        public static readonly Trait[] All = { Trait.Wrath, Trait.Swiftness, Trait.Patience, Trait.Mystic, Trait.Vitality };

        public static string Name(Trait trait)
        {
            switch (trait)
            {
                case Trait.Wrath: return "분노";
                case Trait.Swiftness: return "신속";
                case Trait.Patience: return "인내";
                case Trait.Mystic: return "신비";
                default: return "생명";
            }
        }

        // 이 특성을 주로 원하는 쪽 (HUD 표시용).
        public static string Owner(Trait trait)
        {
            switch (trait)
            {
                case Trait.Wrath: return "B";
                case Trait.Swiftness:
                case Trait.Patience: return "A";
                default: return "공용";
            }
        }

        public static string PerPoint(Trait trait)
        {
            switch (trait)
            {
                case Trait.Wrath: return "포인트당 주는 피해 +3%";
                case Trait.Swiftness: return "포인트당 이동속도 +2%";
                case Trait.Patience: return "포인트당 방어력 +2";
                case Trait.Mystic: return "포인트당 최대 마나 +5";
                default: return "포인트당 최대 체력 +5";
            }
        }

        // milestone: 5, 10, 20
        public static string MilestoneText(Trait trait, int milestone)
        {
            switch (trait)
            {
                case Trait.Wrath:
                    return milestone == 5 ? "평타 마나 5 → 3" : milestone == 10 ? "치명타 확률 +10%" : "적 처치 시 마나 +5";
                case Trait.Swiftness:
                    return milestone == 5 ? "대쉬 기력 50 → 35" : milestone == 10 ? "순간이동 거리 4 → 6" : "대쉬 거리 1.5배";
                case Trait.Patience:
                    return milestone == 5 ? "피격 후 무적 0.2 → 0.5초" : milestone == 10 ? "디버프 저항도 +25" : "체력 30% 이하일 때 받는 피해 -25%";
                case Trait.Mystic:
                    return milestone == 5 ? "마나 재생 +30%" : milestone == 10 ? "실패로 날린 마나 절반 돌려받기" : "모든 마나 소모 -20%";
                default:
                    return milestone == 5 ? "체크포인트 회복 50% → 75%" : milestone == 10 ? "적 처치 시 체력 +2" : "한 번 쓰러질 피해를 체력 30%로 버팀";
            }
        }
    }
}
