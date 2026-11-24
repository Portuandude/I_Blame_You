using System;
using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 골드 드롭 (2026-11-12 사람 확정, 표는 Chunks/GoldDrops.cs). 적이 죽으면 표대로 골드를 굴려 바로 RunStats.Gold에 넣고,
    // 적 자리에서 동전들이 부채꼴로 톡 튀었다가 플레이어에게 빨려 온다 (StarShardFx). HUD는 아직 날아오는 몫(InFlight)을 빼고 보여 줘서
    // 동전이 닿을 때마다 숫자가 올라간다. 골드는 그 판 안에서만 쓴다 (SkillHUD.Initialize에서 RunStats.Reset + Reset).
    public static class RunGold
    {
        public const int Stage = 1;                       // 스테이지가 늘면 지금 스테이지 번호로
        private const float CoinScale = 0.42f;            // 48px 그림 → 약 0.2유닛
        private const float CoinSfxGap = 0.05f;           // 동전 소리는 이 간격보다 촘촘하게 겹치지 않는다
        private const float CoinSfxVolume = 0.55f;        // 하나만 닿을 때 크기. 겹치면 CoinSoundRule로 겹친 수에 비례해 작게 (2026-11-24)
        private static readonly System.Collections.Generic.Queue<float> recentCoins = new System.Collections.Generic.Queue<float>();
        private static readonly Vector3 TargetOffset = Vector3.up * 0.8f;
        private static readonly Vector3 PouchGlintOffset = new Vector3(0.55f, 1.4f, 0f); // 복주머니 (도토리·훈장 머리 위와 안 겹치게 오른쪽)
        private const float PouchGlintGap = 0.6f;         // 여럿을 한꺼번에 잡아도 복주머니는 이 간격보다 자주 뜨지 않는다
        private static float lastPouchGlint = -10f;

        private static int generation;                    // 판이 바뀌면 이전 판 동전 콜백을 무시한다
        private static float lastCoinSfx = -1f;

        public static int InFlight { get; private set; }
        public static event Action<int> Arrived;         // 동전 하나가 닿았다 (골드 양). HUD 숫자 튐

        public static void Reset()
        {
            generation++;
            InFlight = 0;
            lastPouchGlint = -10f; // 새 판: 지난 판 간격 기록을 지운다 (도메인 리로드를 꺼도)
            recentCoins.Clear();
        }

        public static EnemyRank RankOf(EnemyController enemy)
        {
            if (enemy is SlimeKingController) return EnemyRank.Boss; // 2페이즈에 둘로 나뉜다: 한 마리당 표의 절반 (BossParts)
            if (enemy.GetComponent<SlimeKnight>() != null) return EnemyRank.MidBoss; // 쌍둥이 기사: 한 마리당 표의 절반
            if (enemy.GetComponent<PrismJelly>() != null) return EnemyRank.MidBoss;  // 프리즘 젤리: 한 마리가 중간보스 몫 전부 (Shares)
            var pack = enemy.GetComponent<KoboldPack>(); // 스테이지 2 보스 무리: 족장 = 보스, 와르그 = 정예
            if (pack != null) return pack.IsChief ? EnemyRank.Boss : EnemyRank.Elite;
            if (enemy.GetComponent<EnrageAbility>() != null || enemy.GetComponent<SplitOnDeath>() != null) return EnemyRank.Elite;
            return EnemyRank.Normal;
        }

        // 골드 표·영혼석의 중간보스 몫은 쌍둥이 기사 한 마리 기준(절반), 최종보스 몫은 슬라임 킹 반쪽 기준(절반)이다.
        // 한 몸으로 나오는 프리즘 젤리(중간보스)와 코볼트 족장(스테이지 2 보스)은 그 몫을 두 번 받는다.
        public static int Shares(EnemyController enemy)
        {
            if (enemy == null) return 1;
            if (enemy.GetComponent<PrismJelly>() != null) return 2;
            var pack = enemy.GetComponent<KoboldPack>();
            return pack != null && pack.IsChief ? 2 : 1;
        }

        // 적이 죽었을 때 (SkillHUD.OnEnemyKilled). target: 동전이 날아갈 플레이어.
        public static void OnEnemyKilled(EnemyController enemy, Transform target)
        {
            if (enemy == null || RunStats.Ended && !(enemy is SlimeKingController)) return;
            if (enemy.GetComponent<BossSummon>() != null) return; // 보스가 되풀이해 소환하는 적은 골드 없음
            var rank = RankOf(enemy);
            bool treasure = enemy is TreasureGoblinController; // 보물 고블린: 10초 안에 잡으면 큰 골드 (Chunks/TreasureGoblinRule)
            int amount = 0;
            if (treasure) amount = TreasureGoblinRule.Gold(UnityEngine.Random.value);
            else
                for (int share = Shares(enemy); share > 0; share--) // 프리즘 젤리·코볼트 족장: 둘 몫을 한 번에
                    amount += GoldDropTable.ForStage(Stage).Roll(rank, UnityEngine.Random.value, UnityEngine.Random.value);
            var relics = target != null ? target.GetComponent<PlayerRelics>() : null;
            if (relics != null && relics.Has(RelicId.LuckyPouch))
            {
                int boosted = RelicBook.LuckyPouchGold(amount); // 복주머니 +25%
                if (boosted > amount && target != null && Time.time - lastPouchGlint >= PouchGlintGap)
                {
                    lastPouchGlint = Time.time;
                    VfxPlayer.LuckyPouchGlint(target, PouchGlintOffset);
                }
                amount = boosted;
            }
            if (amount <= 0) return;

            RunStats.AddGold(amount);
            LaunchCoins(enemy.transform.position + Vector3.up * 0.5f, target, amount);
            if (treasure)
            {
                StageEvents.PostNotice($"보물 고블린을 잡았다! 골드 +{amount}");
                GameAudio.PlaySfx("CoinPile", 0.9f, 0f);
            }
        }

        // 보물 고블린을 맞혔을 때 (TreasureGoblinController.Hit → SkillHUD): 자루에서 금화 TreasureGoblinRule.HitGold가 흘러 플레이어에게 온다. 복주머니는 안 붙는다.
        public static void OnTreasureGoblinHit(TreasureGoblinController goblin, Transform target)
        {
            if (goblin == null || target == null || RunStats.Ended) return;
            RunStats.AddGold(TreasureGoblinRule.HitGold);
            LaunchCoins(goblin.transform.position + Vector3.up * 0.7f, target, TreasureGoblinRule.HitGold);
            GameAudio.PlaySfx("CoinPile", 0.35f);
        }

        // 동전 무더기를 주웠을 때 (Stage/CoinPile). 몬스터 골드가 아니라 복주머니는 붙지 않는다. 받은 골드를 돌려준다 (판이 끝났으면 0).
        public static int OnCoinPile(Transform player, Vector3 from, int amount)
        {
            if (player == null || RunStats.Ended || amount <= 0) return 0;
            RunStats.AddGold(amount);
            LaunchCoins(from, player, amount);
            return amount;
        }

        // amount 골드를 동전 여러 개로 나눠 from에서 부채꼴로 튀운 뒤 target에게 빨려 오게 한다. 닿을 때마다 InFlight가 줄고 HUD 숫자가 오른다.
        private static void LaunchCoins(Vector3 from, Transform target, int amount)
        {
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Coin");
            var coins = GoldDropTable.SplitCoins(amount);
            int gen = generation;
            for (int i = 0; i < coins.Length; i++)
            {
                int value = coins[i];
                InFlight += value;
                // 부채꼴: 가운데는 위로, 양옆은 비스듬히. 동전이 많으면 조금씩 흩어진다
                float spread = coins.Length == 1 ? 0f : (i / (coins.Length - 1f) - 0.5f) * 2f;
                var pop = new Vector3(spread * 3f + UnityEngine.Random.Range(-0.4f, 0.4f), 4f + UnityEngine.Random.Range(0f, 1.5f), 0f);
                StarShardFx.Launch(from, target, TargetOffset, frames, () => OnCoinArrived(gen, value), CoinScale, null, pop);
            }
        }

        private static void OnCoinArrived(int gen, int value)
        {
            if (gen != generation) return;
            InFlight = Mathf.Max(0, InFlight - value);
            // 겹친 수: CoinSoundRule.Window 안에 닿은 동전 (이번 것 포함). 소리 간격(CoinSfxGap)과 상관없이 모두 센다.
            recentCoins.Enqueue(Time.time);
            while (recentCoins.Count > 0 && Time.time - recentCoins.Peek() > CoinSoundRule.Window) recentCoins.Dequeue();
            if (Time.time - lastCoinSfx >= CoinSfxGap)
            {
                lastCoinSfx = Time.time;
                GameAudio.PlaySfx("Coin", CoinSoundRule.Volume(CoinSfxVolume, recentCoins.Count), 0.08f);
            }
            Arrived?.Invoke(value);
        }
    }
}
