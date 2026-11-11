namespace IBlameYou.Core
{
    // 한 판(런) 동안의 기록. 게임오버/클리어 창에 보여 준다. 새 런을 시작할 때(SkillHUD.Initialize) 초기화한다.
    public static class RunStats
    {
        public static int Kills { get; set; }
        public static float DamageTaken { get; set; }
        public static int ArenasCleared { get; set; }
        public static int GatesOpened { get; set; }
        public static int NewRelics { get; set; } // 이번 판에 처음 유물 도감에 오른 유물 수 (결과 창 최애 줄 끝)
        public static int NewRelicMask { get; private set; } // 이번 판에 처음 도감에 오른 유물 (RelicCodex 비트 마스크, 결과 창 유물 줄 NEW 점)
        public static bool CodexCompleted { get; set; } // 이번 판에 유물 도감을 다 채웠다 (결과 창 금색 "유물 도감 완성!")
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
            NewRelics = 0;
            NewRelicMask = 0;
            CodexCompleted = false;
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

        // 새 유물이 도감에 올랐다. completes: 그 유물로 도감이 다 찼다 (새로 오를 때만 완성으로 바뀔 수 있으니 '처음 완성').
        public static void AddNewRelic(int relicId, bool completes = false)
        {
            if (Ended) return;
            NewRelics++;
            NewRelicMask = IBlameYou.Systems.RelicCodex.With(NewRelicMask, relicId);
            if (completes) CodexCompleted = true;
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
