using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 저주받은 상자 방 고르기·정예 종류 (Chunks/CursedChestRule).
    public class CursedChestRuleTests
    {
        [Test]
        public void OnlyTravelRoomsPastMinDifficulty()
        {
            for (int seed = 0; seed < 50; seed++)
                for (int room = 0; room < 12; room++)
                {
                    Assert.IsFalse(CursedChestRule.IsCursedChestRoom(seed, room, 0.9f, false));
                    Assert.IsFalse(CursedChestRule.IsCursedChestRoom(seed, room, CursedChestRule.MinDifficulty - 0.01f, true));
                }
        }

        [Test]
        public void ChanceAndBothElitesAppear()
        {
            int yes = 0, total = 0, mother = 0, warrior = 0;
            for (int seed = 0; seed < 400; seed++)
                for (int room = 0; room < 12; room++)
                {
                    int s = seed * 7907 + 1;
                    total++;
                    if (CursedChestRule.IsCursedChestRoom(s, room, 0.6f, true)) yes++;
                    var elite = CursedChestRule.EliteFor(s, room);
                    Assert.AreEqual(elite, CursedChestRule.EliteFor(s, room));
                    if (elite == StageEnemy.MotherSlime) mother++;
                    else if (elite == StageEnemy.GoblinWarrior) warrior++;
                    else Assert.Fail("정예만 깨어난다");
                }

            Assert.AreEqual(CursedChestRule.ChancePercent / 100.0, yes / (double)total, 0.06);
            Assert.Greater(mother, total / 3);
            Assert.Greater(warrior, total / 3);
        }

        [Test]
        public void OfferIsTwoRelics()
        {
            Assert.AreEqual(2, CursedChestRule.OfferSize);
            var offer = IBlameYou.Player.RelicBook.Offer(5, 3, null, CursedChestRule.OfferSize);
            Assert.AreEqual(2, offer.Length);
        }

        [Test]
        public void SwarmIsAboutAThirdAndFixedPerRoom()
        {
            int swarm = 0, total = 0;
            for (int s = 0; s < 300; s++)
                for (int room = 0; room < 12; room++)
                {
                    total++;
                    bool a = CursedChestRule.IsSwarm(s, room);
                    Assert.AreEqual(a, CursedChestRule.IsSwarm(s, room));
                    if (a) swarm++;
                    var group = CursedChestRule.SwarmFor(s, room);
                    Assert.AreEqual(CursedChestRule.SwarmSize, group.Length);
                    Assert.AreEqual(CursedChestRule.SwarmSize, CursedChestRule.SwarmOffsets.Length);
                    foreach (var e in group)
                        Assert.IsTrue(e == StageEnemy.Slime || e == StageEnemy.Goblin || e == StageEnemy.GoblinArcher, "쫄몹만");
                }
            Assert.AreEqual(1 / 3.0, swarm / (double)total, 0.06);
        }
    }
}
