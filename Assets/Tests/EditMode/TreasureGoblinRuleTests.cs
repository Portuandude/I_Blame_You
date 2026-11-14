using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 보물 고블린 방 고르기와 골드 (Chunks/TreasureGoblinRule).
    public class TreasureGoblinRuleTests
    {
        [Test]
        public void OnlyTravelRoomsPastMinDifficulty()
        {
            for (int seed = 0; seed < 50; seed++)
                for (int room = 0; room < 12; room++)
                {
                    Assert.IsFalse(TreasureGoblinRule.IsTreasureGoblinRoom(seed, room, 0.9f, false));
                    Assert.IsFalse(TreasureGoblinRule.IsTreasureGoblinRoom(seed, room, TreasureGoblinRule.MinDifficulty - 0.01f, true));
                }
        }

        [Test]
        public void ChanceIsRoughlyThirtyPercentAndDeterministic()
        {
            int yes = 0, total = 0;
            for (int seed = 0; seed < 400; seed++)
                for (int room = 0; room < 12; room++)
                {
                    bool a = TreasureGoblinRule.IsTreasureGoblinRoom(seed * 104729 + 3, room, 0.5f, true);
                    Assert.AreEqual(a, TreasureGoblinRule.IsTreasureGoblinRoom(seed * 104729 + 3, room, 0.9f, true));
                    total++;
                    if (a) yes++;
                }

            Assert.AreEqual(TreasureGoblinRule.ChancePercent / 100.0, yes / (double)total, 0.06);
        }

        [Test]
        public void NotTheSameRoomsAsAmbush()
        {
            int both = 0, goblin = 0;
            for (int seed = 0; seed < 300; seed++)
                for (int room = 0; room < 12; room++)
                {
                    if (!TreasureGoblinRule.IsTreasureGoblinRoom(seed, room, 0.5f, true)) continue;
                    goblin++;
                    if (AmbushRule.IsAmbush(seed, room, 0.5f, true)) both++;
                }

            Assert.Less(both / (double)goblin, 0.65, "두 해시가 같은 패턴이면 안 된다 (독립이면 약 50%)");
        }

        [Test]
        public void GoldStaysInRange()
        {
            Assert.AreEqual(TreasureGoblinRule.MinGold, TreasureGoblinRule.Gold(0f));
            Assert.AreEqual(TreasureGoblinRule.MaxGold, TreasureGoblinRule.Gold(0.99999f));
            Assert.AreEqual(TreasureGoblinRule.MaxGold, TreasureGoblinRule.Gold(1f));
            Assert.AreEqual(TreasureGoblinRule.MinGold, TreasureGoblinRule.Gold(-1f));
            for (int i = 0; i < 100; i++)
            {
                int g = TreasureGoblinRule.Gold(i / 100f);
                Assert.That(g, Is.InRange(TreasureGoblinRule.MinGold, TreasureGoblinRule.MaxGold));
            }
        }

        [Test]
        public void EquipmentDropIsRareAndIndependent()
        {
            int goblin = 0, drops = 0;
            for (int seed = 1; seed <= 400; seed++)
                for (int room = 0; room < 12; room++)
                {
                    Assert.AreEqual(TreasureGoblinRule.DropsEquipment(seed, room), TreasureGoblinRule.DropsEquipment(seed, room), "같은 시드·방이면 같아야 한다");
                    if (!TreasureGoblinRule.IsTreasureGoblinRoom(seed, room, 0.5f, true)) continue;
                    goblin++;
                    if (TreasureGoblinRule.DropsEquipment(seed, room)) drops++;
                }

            double rate = drops / (double)goblin;
            Assert.That(rate, Is.InRange(0.05, 0.16), "보물 고블린 방의 약 10%");
        }
    }
}
