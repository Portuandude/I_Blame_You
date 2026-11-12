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
        private static readonly Vector3 TargetOffset = Vector3.up * 0.8f;
        private static readonly Vector3 PouchGlintOffset = new Vector3(0.55f, 1.4f, 0f); // 복주머니 (도토리·훈장 머리 위와 안 겹치게 오른쪽)
        private static readonly Vector3 PiggyGlintOffset = new Vector3(-0.55f, 1.4f, 0f); // 돼지 저금통 (복주머니 반대쪽)
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
        }

        public static EnemyRank RankOf(EnemyController enemy)
        {
            if (enemy is SlimeKingController) return EnemyRank.Boss; // 2페이즈에 둘로 나뉜다: 한 마리당 표의 절반 (BossParts)
            if (enemy.GetComponent<SlimeKnight>() != null) return EnemyRank.MidBoss; // 쌍둥이 기사: 한 마리당 표의 절반
            if (enemy.GetComponent<EnrageAbility>() != null || enemy.GetComponent<SplitOnDeath>() != null) return EnemyRank.Elite;
            return EnemyRank.Normal;
        }

        // 적이 죽었을 때 (SkillHUD.OnEnemyKilled). target: 동전이 날아갈 플레이어.
        public static void OnEnemyKilled(EnemyController enemy, Transform target)
        {
            if (enemy == null || RunStats.Ended && !(enemy is SlimeKingController)) return;
            if (enemy.GetComponent<BossSummon>() != null) return; // 보스가 되풀이해 소환하는 적은 골드 없음
            var rank = RankOf(enemy);
            int amount = GoldDropTable.ForStage(Stage).Roll(rank, UnityEngine.Random.value, UnityEngine.Random.value);
            var relics = target != null ? target.GetComponent<PlayerRelics>() : null;
            if (relics != null && relics.Has(RelicId.LuckyPouch))
            {
                int boosted = RelicBook.LuckyPouchGold(amount); // 복주머니 +50%
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
        }

        // 돼지 저금통 (2026-11-12): 판에서 처음 찾은 마을에 들어갈 때 (ChunkStreamer.EnterRoom). 화면에 보이는 골드(아직 날아오는 몫 제외)의
        // 25%(최대 50)를 이자로 받는다. 머리 위에 저금통이 톡 뜨고 동전이 저금통 자리에서 튀어 빨려 온다. 받은 이자를 돌려준다 (없으면 0).
        public static int OnVillageFound(Transform player)
        {
            if (player == null || RunStats.Ended) return 0;
            var relics = PlayerRelics.For(player.gameObject);
            if (relics == null || !relics.Has(RelicId.PiggyBank)) return 0;
            int interest = RelicBook.PiggyBankInterest(Mathf.Max(0, RunStats.Gold - InFlight));
            if (interest <= 0) return 0;
            RunStats.AddGold(interest);
            VfxPlayer.PiggyBankGlint(player, PiggyGlintOffset);
            LaunchCoins(player.position + PiggyGlintOffset, player, interest);
            return interest;
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
            if (Time.time - lastCoinSfx >= CoinSfxGap)
            {
                lastCoinSfx = Time.time;
                GameAudio.PlaySfx("Coin", 0.55f, 0.08f);
            }
            Arrived?.Invoke(value);
        }
    }
}
