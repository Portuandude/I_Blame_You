namespace IBlameYou.Core
{
    // 한 판(런) 동안의 기록. 게임오버/클리어 창에 보여 준다. 새 런을 시작할 때(SkillHUD.Initialize) 초기화한다.
    public static class RunStats
    {
        public static int Kills { get; set; }
        public static float DamageTaken { get; set; }
        public static int ArenasCleared { get; set; }
        public static int GatesOpened { get; set; }
        public static bool Ended { get; set; } // 쓰러지거나 클리어한 뒤에는 더 세지 않는다

        public static void Reset()
        {
            Kills = 0;
            DamageTaken = 0f;
            ArenasCleared = 0;
            GatesOpened = 0;
            Ended = false;
        }

        public static void AddArenaCleared()
        {
            if (!Ended) ArenasCleared++;
        }

        public static void AddGateOpened()
        {
            if (!Ended) GatesOpened++;
        }

        public static string Summary(int level) =>
            $"레벨 {level} · 처치 {Kills} · 받은 피해 {DamageTaken:0}(넘친 피해 포함) · 돌파 구역 {ArenasCleared} · 연 봉인문 {GatesOpened}";
    }
}
