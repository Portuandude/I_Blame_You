using System.Collections.Generic;
using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 유물 상자 후보 고르기(RelicBook.Offer) 규칙 검증.
    public class RelicBookTests
    {
        [Test]
        public void OfferHasThreeDistinctRelics()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var offer = RelicBook.Offer(seed, 7, null);
                Assert.AreEqual(RelicBook.OfferSize, offer.Length, $"seed {seed}");
                Assert.AreEqual(offer.Length, new HashSet<RelicId>(offer).Count, $"seed {seed}: 후보가 겹친다");
            }
        }

        [Test]
        public void OfferIsDeterministic()
        {
            var owned = new List<RelicId> { RelicId.IronCloak };
            CollectionAssert.AreEqual(RelicBook.Offer(1234, 12, owned), RelicBook.Offer(1234, 12, owned));
        }

        [Test]
        public void OfferSkipsOwnedRelics()
        {
            var owned = new List<RelicId> { RelicId.ManaCrystal, RelicId.WrathCharm, RelicId.LifeSeed, RelicId.HawkEye };
            for (int seed = 0; seed < 50; seed++)
            {
                var offer = RelicBook.Offer(seed, seed % 9, owned);
                Assert.AreEqual(3, offer.Length);
                foreach (var id in offer) CollectionAssert.DoesNotContain(owned, id, $"seed {seed}");
            }
        }

        [Test]
        public void OfferShrinksWhenFewRemain()
        {
            var owned = new List<RelicId>(RelicBook.All);
            owned.Remove(RelicId.SpringRing);
            CollectionAssert.AreEqual(new[] { RelicId.SpringRing }, RelicBook.Offer(5, 3, owned));
            Assert.AreEqual(0, RelicBook.Offer(5, 3, RelicBook.All).Length);
        }

        // 정예 구역 두 곳(다른 청크)의 후보가 시드마다 늘 같으면 고르는 재미가 없다.
        [Test]
        public void DifferentChunksGiveDifferentOffers()
        {
            int differ = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                var a = RelicBook.Offer(seed, 10, null);
                var b = RelicBook.Offer(seed, 20, null);
                if (!new HashSet<RelicId>(a).SetEquals(b)) differ++;
            }

            Assert.Greater(differ, 30);
        }

        [Test]
        public void EveryRelicHasText()
        {
            foreach (var id in RelicBook.All)
            {
                Assert.IsNotEmpty(RelicBook.Name(id));
                Assert.IsNotEmpty(RelicBook.Description(id));
                Assert.IsNotEmpty(RelicBook.Owner(id));
            }

            Assert.AreEqual(System.Enum.GetValues(typeof(RelicId)).Length, RelicBook.All.Length);
        }

        // 저장(RunSave)이 유물을 번호로 기억하므로 예전 유물 번호가 바뀌면 안 된다.
        [Test]
        public void RelicNumbersStayStable()
        {
            Assert.AreEqual(0, (int)RelicId.ManaCrystal);
            Assert.AreEqual(9, (int)RelicId.FocusBead);
            Assert.AreEqual(10, (int)RelicId.ShadowVeil);
            Assert.AreEqual(11, (int)RelicId.BloodPact);
            Assert.AreEqual(12, (int)RelicId.PhoenixFeather);
            Assert.AreEqual(13, (int)RelicId.Hourglass);
            Assert.AreEqual(14, (int)RelicId.TwinRing);
            Assert.AreEqual(15, (int)RelicId.Compass);
        }

        [Test]
        public void BloodPactPaysShortfallWithHealth()
        {
            Assert.IsTrue(RelicBook.BloodPactCost(50f, 8f, out float cost));
            Assert.AreEqual(8f * RelicBook.BloodPactHealthPerMana, cost, 0.001f);
        }

        // 피의 계약으로는 쓰러지지 않는다: 낸 뒤 체력이 1 미만이면 거절.
        [Test]
        public void BloodPactNeverKills()
        {
            Assert.IsFalse(RelicBook.BloodPactCost(8f, 8f, out _));
            Assert.IsFalse(RelicBook.BloodPactCost(5f, 20f, out _));
            Assert.IsTrue(RelicBook.BloodPactCost(9f, 8f, out _));
        }

        [Test]
        public void BloodPactIgnoresNoShortfall()
        {
            Assert.IsFalse(RelicBook.BloodPactCost(50f, 0f, out float cost));
            Assert.AreEqual(0f, cost);
        }

        [Test]
        public void Clover_OffersFour()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var plain = RelicBook.Offer(seed, 9, new[] { RelicId.IronCloak });
                var lucky = RelicBook.Offer(seed, 9, new[] { RelicId.IronCloak, RelicId.Clover });
                Assert.AreEqual(RelicBook.OfferSize, plain.Length);
                Assert.AreEqual(RelicBook.CloverOfferSize, lucky.Length, $"seed {seed}");
                CollectionAssert.DoesNotContain(lucky, RelicId.Clover);
                CollectionAssert.AllItemsAreUnique(lucky);
            }
            // 남은 유물이 모자라면 남은 것만
            var almostAll = new System.Collections.Generic.List<RelicId>(RelicBook.All);
            almostAll.Remove(RelicId.SpringRing);
            almostAll.Remove(RelicId.HawkEye);
            Assert.AreEqual(2, RelicBook.Offer(3, 1, almostAll).Length);
            Assert.AreEqual(0, RelicBook.Offer(3, 1, null, -1).Length);
        }
    }
}
