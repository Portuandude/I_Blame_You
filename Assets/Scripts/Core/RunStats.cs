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
        // 속성별(불/물/바람/땅/무) 사용 횟수: B 공격 마법 발동, A 방어막 발동. 결과 창 "최애" 줄 (Chunks/RunTally).
        public static readonly int[] SpellCasts = new int[5];
        public static readonly int[] ShieldCasts = new int[5];

        public static void Reset()
        {
            Kills = 0;
            DamageTaken = 0f;
            ArenasCleared = 0;
            GatesOpened = 0;
            System.Array.Clear(SpellCasts, 0, SpellCasts.Length);
            System.Array.Clear(ShieldCasts, 0, ShieldCasts.Length);
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

        public static void AddSpell(int element)
        {
            if (!Ended && element >= 0 && element < SpellCasts.Length) SpellCasts[element]++;
        }

        public static void AddShield(int element)
        {
            if (!Ended && element >= 0 && element < ShieldCasts.Length) ShieldCasts[element]++;
        }

        public static string Summary(int level, int relics = 0) =>
            $"레벨 {level} · 처치 {Kills} · 받은 피해 {DamageTaken:0}(넘친 피해 포함) · 돌파 구역 {ArenasCleared} · 연 봉인문 {GatesOpened} · 유물 {relics}";
    }
}
