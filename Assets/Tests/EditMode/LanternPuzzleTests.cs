using System.Linq;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 퍼즐 방 "등불 순서" 지형 (Docs/PuzzleRoomDesign.md 3절): 높이가 다른 등불 셋(속성·순번 모두 다름) + 계단 발판 + 벽화 + 상자 자리.
    public class LanternPuzzleTests
    {
        private static ChunkData Make(int seed, int entry = 7) =>
            ChunkGenerator.Generate(seed, 3, ChunkKind.LanternPuzzle, 0.5f, entry, 0);

        [Test]
        public void HasThreeLanternsWithDistinctElementsOrdersAndHeights()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                var chunk = Make(seed);
                string at = $"seed {seed}";
                var lanterns = chunk.Features.Where(f => f.Kind == FeatureKind.Lantern).ToList();
                Assert.AreEqual(ChunkGenerator.LanternCount, lanterns.Count, at);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, lanterns.Select(l => l.ElementIndex), at);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, lanterns.Select(l => l.Direction), at);
                CollectionAssert.AreEquivalent(new[] { 7, 7 + ChunkGenerator.LanternMidRise, 7 + ChunkGenerator.LanternHighRise },
                    lanterns.Select(l => l.Y), at);
                Assert.AreEqual(1, chunk.Features.Count(f => f.Kind == FeatureKind.LanternMural), at);
                Assert.AreEqual(1, chunk.Features.Count(f => f.Kind == FeatureKind.PuzzleChest), at);
                Assert.IsFalse(chunk.Features.Any(f => f.Kind == FeatureKind.Enemy), at);
            }
        }

        // 섞기가 실제로 섞는지: 60시드에서 켜는 순서 6가지가 모두 나온다.
        [Test]
        public void OrdersVaryAcrossSeeds()
        {
            var orders = Enumerable.Range(0, 60).Select(seed => string.Join(",",
                Make(seed).Features.Where(f => f.Kind == FeatureKind.Lantern).OrderBy(f => f.Y).Select(f => f.Direction))).Distinct().Count();
            Assert.AreEqual(6, orders);
        }

        // 등불마다 그 높이에 설 수 있다 (쏘려면 같은 높이에 서야 한다). 방은 지나갈 수 있다 (등불·발판이 길을 막지 않는다).
        [Test]
        public void EveryLanternStandIsReachableAndRoomPasses()
        {
            foreach (int entry in new[] { ChunkGenerator.MinGround, 7, ChunkGenerator.MaxGround })
            {
                var chunk = Make(11, entry);
                Assert.IsTrue(StageValidator.Validate(chunk, out string reason), $"entry {entry}: {reason}");
                foreach (var l in chunk.Features.Where(f => f.Kind == FeatureKind.Lantern))
                {
                    Assert.IsTrue(chunk.IsSolid(l.X, l.Y - 1) || chunk.Get(l.X, l.Y - 1) == StageTile.Platform, $"entry {entry} lantern at {l.X},{l.Y}");
                    Assert.AreEqual(StageTile.Empty, chunk.Get(l.X, l.Y), $"entry {entry} lantern body {l.X},{l.Y}");
                    Assert.AreEqual(StageTile.Empty, chunk.Get(l.X, l.Y + 1), $"entry {entry} lantern body {l.X},{l.Y + 1}");
                }
            }
        }

        [Test]
        public void FallbackKeepsThePuzzle()
        {
            var chunk = ChunkGenerator.Fallback(3, ChunkKind.LanternPuzzle, 0.5f, 7);
            Assert.AreEqual(ChunkGenerator.LanternCount, chunk.Features.Count(f => f.Kind == FeatureKind.Lantern));
            Assert.IsTrue(StageValidator.Validate(chunk, out string reason), reason);
        }

        [Test]
        public void FeatureTestMapHasLanternRoom()
        {
            var stage = FeatureTestStage.Build();
            var chunk = stage.Chunks[FeatureTestStage.LanternArea];
            Assert.AreEqual(ChunkKind.LanternPuzzle, chunk.Kind);
            Assert.AreEqual(ChunkGenerator.LanternCount, chunk.Features.Count(f => f.Kind == FeatureKind.Lantern));
            Assert.AreEqual("등불 순서 방", FeatureTestStage.Names[FeatureTestStage.LanternArea]);
        }
    }
}
