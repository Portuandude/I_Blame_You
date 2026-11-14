using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 매복 전투 방 고르기 (Chunks/AmbushRule): 일반 전투 방만, 난이도 20% 이상, 시드+방 번호로 늘 같게, 대략 절반.
    public class AmbushRuleTests
    {
        [Test]
        public void OnlyPlainArenasPastMinDifficulty()
        {
            for (int seed = 0; seed < 50; seed++)
                for (int room = 0; room < 12; room++)
                {
                    Assert.IsFalse(AmbushRule.IsAmbush(seed, room, 0.9f, false), "일반 전투 방이 아니면 매복 없음");
                    Assert.IsFalse(AmbushRule.IsAmbush(seed, room, AmbushRule.MinDifficulty - 0.01f, true), "시작 근처는 매복 없음");
                }
        }

        [Test]
        public void SameSeedAndRoomGiveSameAnswer()
        {
            for (int seed = 0; seed < 30; seed++)
                for (int room = 0; room < 12; room++)
                    Assert.AreEqual(AmbushRule.IsAmbush(seed, room, 0.5f, true), AmbushRule.IsAmbush(seed, room, 0.8f, true));
        }

        [Test]
        public void RoughlyHalfOfEligibleRooms()
        {
            int yes = 0, total = 0;
            for (int seed = 0; seed < 400; seed++)
                for (int room = 0; room < 12; room++)
                {
                    total++;
                    if (AmbushRule.IsAmbush(seed * 7919 + 13, room, 0.5f, true)) yes++;
                }

            double ratio = yes / (double)total;
            Assert.AreEqual(AmbushRule.ChancePercent / 100.0, ratio, 0.06);
        }

        [Test]
        public void BurrowIsAboutAThirdAndFixed()
        {
            int yes = 0, total = 0;
            for (int seed = 0; seed < 300; seed++)
                for (int room = 0; room < 12; room++)
                {
                    total++;
                    bool b = AmbushRule.IsBurrow(seed, room);
                    Assert.AreEqual(b, AmbushRule.IsBurrow(seed, room));
                    if (b) yes++;
                }
            Assert.AreEqual(AmbushRule.BurrowPercent / 100.0, yes / (double)total, 0.06);
        }

        [Test]
        public void MixedOnlyInDeepBurrowRooms()
        {
            for (int s = 0; s < 200; s++)
                for (int room = 0; room < 12; room++)
                {
                    Assert.IsFalse(AmbushRule.IsMixed(s, room, AmbushRule.MixedDifficulty - 0.01f), "얕은 방은 섞이지 않는다");
                    Assert.AreEqual(AmbushRule.IsBurrow(s, room), AmbushRule.IsMixed(s, room, AmbushRule.MixedDifficulty), "깊은 땅속 매복 방만 섞인다");
                }
        }

        [Test]
        public void MixedSplitsSpawnsHalfAndHalf()
        {
            int burrow = 0;
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(AmbushRule.BurrowsAt(true, false, i), "섞이지 않은 땅속 매복은 모두 땅속");
                Assert.IsFalse(AmbushRule.BurrowsAt(false, true, i), "땅속 매복이 아니면 땅속 없음");
                if (AmbushRule.BurrowsAt(true, true, i)) burrow++;
            }
            Assert.AreEqual(3, burrow);
        }

        [Test]
        public void FastClearAddsGold()
        {
            Assert.AreEqual(AmbushRule.ClearGold + AmbushRule.FastClearGold, AmbushRule.ClearGoldFor(AmbushRule.FastClearTime));
            Assert.AreEqual(AmbushRule.ClearGold + AmbushRule.FastClearGold, AmbushRule.ClearGoldFor(5f));
            Assert.AreEqual(AmbushRule.ClearGold, AmbushRule.ClearGoldFor(AmbushRule.FastClearTime + 0.1f));
            Assert.IsFalse(AmbushRule.IsFastClear(60f));
        }
    }
}
