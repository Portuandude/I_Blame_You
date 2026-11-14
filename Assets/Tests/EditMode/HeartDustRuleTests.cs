using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 마음 가루 양 (Docs/CityDesign.md 3절): 쫄몹 1 · 정예 5 · 중간보스 둘 합 15 안팎 · 보스 둘 합 40 · 퍼즐 5 · 클리어 20.
    public class HeartDustRuleTests
    {
        [Test]
        public void KillAmountsFollowTheDesign()
        {
            Assert.AreEqual(1, HeartDustRule.ForKill(EnemyRank.Normal));
            Assert.AreEqual(5, HeartDustRule.ForKill(EnemyRank.Elite));
            Assert.That(HeartDustRule.ForKill(EnemyRank.MidBoss) * 2, Is.InRange(14, 16));
            Assert.AreEqual(40, HeartDustRule.ForKill(EnemyRank.Boss) * 2);
            Assert.AreEqual(5, HeartDustRule.Puzzle);
            Assert.AreEqual(20, HeartDustRule.StageClear);
        }
    }
}
