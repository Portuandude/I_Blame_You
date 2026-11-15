namespace IBlameYou.Player
{
    // 얼음 족쇄(아카데미 물 마법, Docs/CityDesign.md 5절) 둔화 세기 (순수 C#, EditMode 테스트 대상).
    // 일반 몹은 다른 물 마법의 젖음과 같은 40%, 정예·중간보스·보스는 20% (반드시 걸리는 대신 강한 적에게는 약하게).
    public static class ShackleRules
    {
        public const float NormalSlow = 0.4f;
        public const float StrongSlow = 0.2f;

        public static float Slow(bool strongTarget) => strongTarget ? StrongSlow : NormalSlow;
    }
}
