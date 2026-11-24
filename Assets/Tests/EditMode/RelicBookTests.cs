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
                Assert.IsFalse(RelicBook.Description(id).StartsWith("("), RelicBook.Name(id));
            }
        }

        // 2026-11-24 사람 결정 (Docs/RelicList.md): 후보는 26종, 겹치지 않는다. 빠진 7종(삭제 5 + 합쳐져 없어진 2)은 enum에만 남는다.
        [Test]
        public void TwentySixRelicsAndRetiredOnesStayOut()
        {
            Assert.AreEqual(26, RelicBook.All.Length);
            CollectionAssert.AllItemsAreUnique(RelicBook.All);
            var retired = new[]
            {
                RelicId.Featherfall, RelicId.Hourglass, RelicId.HorseshoeMagnet, RelicId.RegularStamp, RelicId.PiggyBank,
                RelicId.Compass, RelicId.JellyCushion
            };
            foreach (var id in retired)
            {
                Assert.IsTrue(RelicBook.IsRetired(id), id.ToString());
                CollectionAssert.DoesNotContain(RelicBook.All, id);
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(RelicId)).Length, RelicBook.All.Length + retired.Length);
            for (int seed = 0; seed < 200; seed++)
                foreach (var id in RelicBook.Offer(seed, seed % 13, null)) Assert.IsFalse(RelicBook.IsRetired(id));
        }

        // 예전 저장의 나침반·젤리 방석은 보물 지도·솜구름 방석으로 친다 (PlayerRelics.Add).
        [Test]
        public void MergedRelicsMapToTheirNewRelic()
        {
            Assert.AreEqual(RelicId.TreasureMap, RelicBook.Merged(RelicId.Compass));
            Assert.AreEqual(RelicId.CottonCloud, RelicBook.Merged(RelicId.JellyCushion));
            foreach (var id in RelicBook.All) Assert.AreEqual(id, RelicBook.Merged(id));
            Assert.AreEqual(RelicId.PiggyBank, RelicBook.Merged(RelicId.PiggyBank), "삭제된 유물은 그대로 (효과 없음)");
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
            Assert.AreEqual(16, (int)RelicId.Clover);
            Assert.AreEqual(17, (int)RelicId.CheerRibbon);
            Assert.AreEqual(18, (int)RelicId.StarShard);
            Assert.AreEqual(19, (int)RelicId.HeroMedal);
            Assert.AreEqual(20, (int)RelicId.AcornPouch);
            Assert.AreEqual(21, (int)RelicId.HoneyJar);
            Assert.AreEqual(22, (int)RelicId.JellyCushion);
            Assert.AreEqual(23, (int)RelicId.StarCandy);
            Assert.AreEqual(24, (int)RelicId.HorseshoeMagnet);
            Assert.AreEqual(25, (int)RelicId.PaperPlane);
            Assert.AreEqual(26, (int)RelicId.LuckyPouch);
            Assert.AreEqual(27, (int)RelicId.RegularStamp);
            Assert.AreEqual(28, (int)RelicId.PiggyBank);
            Assert.AreEqual(29, (int)RelicId.TreasureMap);
            Assert.AreEqual(30, (int)RelicId.CottonCloud);
            Assert.AreEqual(31, (int)RelicId.EchoCrystal);
            Assert.AreEqual(32, (int)RelicId.ElementResonance);
        }

        [Test]
        public void ChangedNumbersMatchRelicList()
        {
            Assert.AreEqual("마나 재생 +10%", RelicBook.Description(RelicId.SpringRing));
            Assert.AreEqual("레벨이 오를 때마다 최대 체력 +1", RelicBook.Description(RelicId.AcornPouch));
            Assert.AreEqual("레벨이 오를 때마다 최대 마나 +1", RelicBook.Description(RelicId.StarCandy));
            Assert.AreEqual("적 처치 시 마나 +10", RelicBook.Description(RelicId.StarShard));
            Assert.AreEqual("중간보스나 보스를 처치하면 체력 15% 회복", RelicBook.Description(RelicId.HeroMedal));
            Assert.AreEqual("보상 구슬을 얻으면 체력 회복량 +15%", RelicBook.Description(RelicId.HoneyJar));
            Assert.AreEqual("몬스터가 떨어뜨리는 골드 +25%", RelicBook.Description(RelicId.LuckyPouch));
            Assert.AreEqual("순간이동 마나 -25%", RelicBook.Description(RelicId.PaperPlane));
            Assert.AreEqual("마나가 모자라면 필요한 마나의 50%만큼 체력으로 대신 내기", RelicBook.Description(RelicId.BloodPact));
        }

        [Test]
        public void MergedRelicsDescribeBothEffects()
        {
            Assert.AreEqual("보물 지도", RelicBook.Name(RelicId.TreasureMap));
            Assert.AreEqual("미니맵에 안 가 본 방이 흐리게, 마을·보스 방은 아이콘까지 보인다", RelicBook.Description(RelicId.TreasureMap));
            Assert.AreEqual("솜구름 방석", RelicBook.Name(RelicId.CottonCloud));
            Assert.AreEqual("무너지는 발판이 0.9초 버티고, 깜빡 발판은 사라진 뒤에도 0.5초 더 밟힌다", RelicBook.Description(RelicId.CottonCloud));
            Assert.Greater(RelicBook.CottonCloudGrace, 0f);
            Assert.Less(RelicBook.CottonCloudGrace, 1.4f, "받쳐 주는 시간은 사라짐(1.4초)보다 짧아야 박자가 남는다");
        }

        [Test]
        public void NewRelicsDescribeThemselves()
        {
            Assert.AreEqual("메아리 수정", RelicBook.Name(RelicId.EchoCrystal));
            Assert.AreEqual("같은 속성 마법을 연달아 쓰면 두 번째는 마나 50%", RelicBook.Description(RelicId.EchoCrystal));
            Assert.AreEqual("속성 공명석", RelicBook.Name(RelicId.ElementResonance));
            Assert.AreEqual("적의 약점 속성으로 맞히면 피해 +20%", RelicBook.Description(RelicId.ElementResonance));
        }

        // 메아리 수정: 불·불·불 = 온값·절반·온값, 불·물·물 = 온값·온값·절반, 실패하면 사슬이 끊긴다(-1).
        [Test]
        public void EchoCrystalHalvesEverySecondSameElement()
        {
            int last = -1;
            var paid = new List<bool>();
            foreach (int element in new[] { 0, 0, 0, 0, 1, 1, 2 })
            {
                paid.Add(RelicBook.EchoDiscounted(last, element));
                last = RelicBook.EchoAfterCast(last, element);
            }
            CollectionAssert.AreEqual(new[] { false, true, false, true, false, true, false }, paid);
            Assert.IsFalse(RelicBook.EchoDiscounted(-1, 0));
            Assert.AreEqual(0.5f, RelicBook.EchoCrystalCostRate);
        }

        [Test]
        public void LuckyPouch_RaisesMonsterGoldByQuarter()
        {
            Assert.AreEqual(0, RelicBook.LuckyPouchGold(0));
            Assert.AreEqual(1, RelicBook.LuckyPouchGold(1));  // 1.25 → 1
            Assert.AreEqual(3, RelicBook.LuckyPouchGold(2));  // 2.5 → 3 (.5 올림)
            Assert.AreEqual(5, RelicBook.LuckyPouchGold(4));
            Assert.AreEqual(13, RelicBook.LuckyPouchGold(10)); // 12.5 → 13
            Assert.AreEqual(125, RelicBook.LuckyPouchGold(100));
        }

        // 피의 계약 (2026-11-24): 마법값 전체를 그 절반의 체력으로 낸다 (40마나 → 체력 20). 횟수 제한 없음.
        [Test]
        public void BloodPactPaysHalfTheSpellInHealth()
        {
            Assert.IsTrue(RelicBook.BloodPactCost(50f, 40f, out float cost));
            Assert.AreEqual(20f, cost, 0.001f);
        }

        [Test]
        public void BloodPactNeverKills()
        {
            Assert.IsFalse(RelicBook.BloodPactCost(8f, 16f, out _)); // 체력 8 - 8 = 0
            Assert.IsFalse(RelicBook.BloodPactCost(5f, 20f, out _));
            Assert.IsTrue(RelicBook.BloodPactCost(9f, 16f, out _));  // 체력 1 남김
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

        [Test]
        public void RetiredRelicsHaveNoChestText()
        {
            Assert.AreEqual("(빠진 유물)", RelicBook.Description(RelicId.PiggyBank));
            Assert.AreEqual("(보물 지도로 합쳐짐)", RelicBook.Description(RelicId.Compass));
            Assert.AreEqual("(솜구름 방석으로 합쳐짐)", RelicBook.Description(RelicId.JellyCushion));
        }
    }
}
