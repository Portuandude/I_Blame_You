using System;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 골드 드롭 표 (스테이지 1): 확률과 범위, 쌍둥이 중간보스 나눔, 동전 나누기.
    public class GoldDropTests
    {
        private static readonly GoldDropTable Table = GoldDropTable.Stage1;

        [Test]
        public void Stage1Values()
        {
            Assert.AreEqual(0.5f, Table.Normal.Chance);
            Assert.AreEqual(1, Table.Normal.Min);
            Assert.AreEqual(4, Table.Normal.Max);
            Assert.AreEqual(1f, Table.Elite.Chance);
            Assert.AreEqual(10, Table.Elite.Min);
            Assert.AreEqual(20, Table.Elite.Max);
            Assert.AreEqual(50, Table.MidBoss.Min);
            Assert.AreEqual(100, Table.MidBoss.Max);
            Assert.AreEqual(100, Table.Boss.Min);
            Assert.AreEqual(200, Table.Boss.Max);
            Assert.AreSame(Table, GoldDropTable.ForStage(1));
        }

        [Test]
        public void NormalDropsAboutHalfTheTimeInRange()
        {
            var rng = new Random(7);
            int drops = 0, n = 4000;
            for (int i = 0; i < n; i++)
            {
                int g = Table.Roll(EnemyRank.Normal, rng);
                if (g == 0) continue;
                drops++;
                Assert.That(g, Is.InRange(1, 4));
            }
            Assert.That(drops / (float)n, Is.InRange(0.46f, 0.54f));
        }

        [Test]
        public void RangesAreInclusiveAtBothEnds()
        {
            Assert.AreEqual(0, Table.Roll(EnemyRank.Normal, 0.5, 0.0), "확률 경계 0.5는 안 떨어진다");
            Assert.AreEqual(1, Table.Roll(EnemyRank.Normal, 0.49, 0.0));
            Assert.AreEqual(4, Table.Roll(EnemyRank.Normal, 0.0, 0.9999));
            Assert.AreEqual(10, Table.Roll(EnemyRank.Elite, 0.9999, 0.0), "정예는 늘 떨어진다");
            Assert.AreEqual(20, Table.Roll(EnemyRank.Elite, 0.0, 0.9999));
        }

        [Test]
        public void TwinMidBossSumStaysInRange()
        {
            int lo = Table.Roll(EnemyRank.MidBoss, 0.0, 0.0);
            int hi = Table.Roll(EnemyRank.MidBoss, 0.0, 0.9999);
            Assert.AreEqual(25, lo);
            Assert.AreEqual(50, hi);
            Assert.That(lo * 2, Is.GreaterThanOrEqualTo(50));
            Assert.That(hi * 2, Is.LessThanOrEqualTo(100));
        }

        [Test]
        public void SplitKingSumStaysInRange()
        {
            Assert.AreEqual(50, Table.Roll(EnemyRank.Boss, 0.9999, 0.0), "슬라임 킹은 늘 떨어뜨리고 한 마리당 절반");
            Assert.AreEqual(100, Table.Roll(EnemyRank.Boss, 0.0, 0.9999));
        }

        [Test]
        public void SplitCoinsSumsToAmount()
        {
            for (int amount = 1; amount <= 400; amount++)
            {
                var coins = GoldDropTable.SplitCoins(amount);
                int sum = 0;
                foreach (int c in coins) sum += c;
                Assert.AreEqual(amount, sum, $"{amount}");
                Assert.That(coins.Length, Is.InRange(1, 24));
            }
            Assert.AreEqual(0, GoldDropTable.SplitCoins(0).Length);
        }

        [Test]
        public void CoinPileGoldRisesWithDifficulty()
        {
            Assert.AreEqual(3, GoldDropTable.CoinPileGold(0f));
            Assert.AreEqual(5, GoldDropTable.CoinPileGold(0.5f), "4.5는 올림");
            Assert.AreEqual(6, GoldDropTable.CoinPileGold(1f));
            Assert.AreEqual(3, GoldDropTable.CoinPileGold(-1f), "범위 밖은 끝값");
            Assert.AreEqual(6, GoldDropTable.CoinPileGold(2f));
            int last = 0;
            for (int i = 0; i <= 20; i++)
            {
                int gold = GoldDropTable.CoinPileGold(i / 20f);
                Assert.That(gold, Is.GreaterThanOrEqualTo(last));
                last = gold;
            }
        }
    }
}
