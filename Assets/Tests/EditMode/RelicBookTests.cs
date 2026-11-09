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
    }
}
