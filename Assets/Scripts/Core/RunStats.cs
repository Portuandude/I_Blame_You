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
        public static long NewRelicMask { get; private set; } // 이번 판에 처음 도감에 오른 유물 (RelicCodex 비트 마스크, 결과 창 유물 줄 NEW 점)
        public static bool CodexCompleted { get; set; } // 이번 판에 유물 도감을 다 채웠다 (결과 창 금색 "유물 도감 완성!")
        public static int Gold { get; private set; } // 이번 판 골드 (그 판 안에서만, 다음 판으로 안 넘어감 — RunGold)
        public static int GoldEarned { get; private set; } // 이번 판에 들어온 골드 합계 (동전·장비 판매, 결과 창 "모음")
        public static int GoldSpent { get; private set; }  // 이번 판에 마을에서 쓴 골드 합계 (식당·상점, 결과 창 "씀")
        public static event System.Action<int> GoldSpentNow; // 골드를 썼다 (쓴 양). HUD 골드 알약 아래 빨간 "-N" (SkillHUD.Gold)
        public static bool Ended { get; set; } // 쓰러지거나 클리어한 뒤에는 더 세지 않는다
        public static int HeartDust { get; private set; } // 이번 판에 얻은 영혼석 (도시 화폐, 얻는 순간 SaveData에 쌓인다 — Docs/CityDesign.md)
        // 속성별(불/물/바람/땅/무) 사용 횟수: B 공격 마법 발동. 결과 창 "최애" 줄 (Chunks/RunTally).
        public static readonly int[] SpellCasts = new int[5];

        // 이번 판에 유물·장비를 고른 횟수. 고르는 메뉴는 P1·P2가 번갈아 고른다 (PickTurnRule, 2026-11-24)
        public static int Picks { get; set; }

        // 마을 식당에서 먹은 판 동안 남는 음식 효과 합 (최대 체력 +·마나 재생 배율 +). 스테이지를 넘을 때 새 플레이어에 다시 붙인다 (RunSave, 버그 점검 2026-11-24)
        public static float FoodMaxHealth { get; set; }
        public static float FoodManaRegen { get; set; }

        public static void Reset()
        {
            Kills = 0;
            Picks = 0;
            FoodMaxHealth = 0f;
            FoodManaRegen = 0f;
            DamageTaken = 0f;
            ArenasCleared = 0;
            GatesOpened = 0;
            NewRelics = 0;
            NewRelicMask = 0;
            CodexCompleted = false;
            Gold = 0;
            GoldEarned = 0;
            GoldSpent = 0;
            HeartDust = 0;
            System.Array.Clear(SpellCasts, 0, SpellCasts.Length);
            Ended = false;
        }

        // 쉼터에서 다음 스테이지로 (또는 쉼터 저장 이어하기): 앞 스테이지까지의 판 기록을 되살린다 (Reset 뒤에 부른다).
        // 영혼석은 얻을 때 이미 SaveData에 쌓였으니 이번 판 표시값만 되돌린다.
        public static void Restore(int gold, int earned, int spent, int heartDust, int picks, int[] casts)
        {
            Gold = System.Math.Max(0, gold);
            GoldEarned = System.Math.Max(0, earned);
            GoldSpent = System.Math.Max(0, spent);
            HeartDust = System.Math.Max(0, heartDust);
            Picks = System.Math.Max(0, picks);
            if (casts != null)
                for (int i = 0; i < SpellCasts.Length && i < casts.Length; i++) SpellCasts[i] = System.Math.Max(0, casts[i]);
        }

        // 쉼터를 넘어 이어지는 도감 집계 (결과 창 "새로 도감에 오른 유물"·NEW 점·"유물 도감 완성!"이 판 전체 몫이 되게). Reset 뒤에 부른다.
        public static void RestoreFood(float maxHealth, float manaRegen)
        {
            FoodMaxHealth = System.Math.Max(0f, maxHealth);
            FoodManaRegen = System.Math.Max(0f, manaRegen);
        }

        public static void RestoreCodex(int newRelics, long newRelicMask, bool codexCompleted)
        {
            NewRelics = System.Math.Max(0, newRelics);
            NewRelicMask = newRelicMask;
            CodexCompleted = codexCompleted;
        }

        // 골드는 판이 끝난 뒤(보스 처치 → 클리어)에도 받는다: 보스 골드가 클리어와 같은 순간에 들어오기 때문.
        public static void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            GoldEarned += amount;
        }

        // 마을에서 골드를 쓴다 (식당·장비 상점). reserved: 아직 날아오는 동전 몫(RunGold.InFlight) — 화면 숫자만큼만 쓸 수 있게.
        // 모자라면 false, 아무것도 바꾸지 않는다.
        public static bool TrySpendGold(int amount, int reserved)
        {
            if (amount < 0 || Gold - System.Math.Max(0, reserved) < amount) return false;
            Gold -= amount;
            GoldSpent += amount;
            if (amount > 0) GoldSpentNow?.Invoke(amount);
            return true;
        }

        // 영혼석: 이번 판 집계 + 저장된 누적에 바로 더한다 (쓰러져도 남는다). 보스 처치·클리어는 판이 끝나는 순간과 겹쳐서 Ended여도 받는다.
        public static void AddHeartDust(int amount)
        {
            if (amount <= 0 || GameSession.FeatureTest) return; // 기능 테스트는 저장도 안 하므로 결과 창에도 안 보인다 (버그 점검 2026-11-24)
            HeartDust += amount;
            SaveData.AddHeartDust(amount);
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

        public static string Summary(int level, int relics = 0) =>
            $"레벨 {level} · 처치 {Kills} · 받은 피해 {DamageTaken:0}(넘친 피해 포함) · 돌파 구역 {ArenasCleared} · 연 봉인문 {GatesOpened} · 유물 {relics}";
    }
}
