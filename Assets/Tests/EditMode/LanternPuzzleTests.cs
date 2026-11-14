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
    
        // 거울 룬: 바닥 등불 셋(왼쪽·가운데·오른쪽, 속성·순번 모두 다름) + 거울 + 상자 자리. 방은 지나갈 수 있다.
        [Test]
        public void MirrorPuzzleHasFloorLanternsAndMirror()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                var chunk = ChunkGenerator.Generate(seed, 3, ChunkKind.MirrorPuzzle, 0.5f, 7, 0);
                string at = $"seed {seed}";
                var lanterns = chunk.Features.Where(f => f.Kind == FeatureKind.Lantern).ToList();
                Assert.AreEqual(ChunkGenerator.LanternCount, lanterns.Count, at);
                Assert.IsTrue(lanterns.All(l => l.Y == 7), at);
                CollectionAssert.AreEquivalent(new[] { ChunkGenerator.MirrorLeftX, ChunkGenerator.MirrorCenterX, ChunkGenerator.MirrorRightX }, lanterns.Select(l => l.X), at);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, lanterns.Select(l => l.ElementIndex), at);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, lanterns.Select(l => l.Direction), at);
                Assert.AreEqual(1, chunk.Features.Count(f => f.Kind == FeatureKind.MirrorMural), at);
                Assert.AreEqual(1, chunk.Features.Count(f => f.Kind == FeatureKind.PuzzleChest), at);
                Assert.IsTrue(StageValidator.Validate(chunk, out string reason), $"{at}: {reason}");
            }
            var fallback = ChunkGenerator.Fallback(3, ChunkKind.MirrorPuzzle, 0.5f, 7);
            Assert.AreEqual(ChunkGenerator.LanternCount, fallback.Features.Count(f => f.Kind == FeatureKind.Lantern));
        }

        // 둘째 퍼즐 방은 시드에 따라 등불 순서·거울 룬이 번갈아 나온다.
        [Test]
        public void BothDeductionPuzzlesAppearAcrossSeeds()
        {
            Assert.AreEqual(ChunkKind.LanternPuzzle, RoomLayout.DeductionPuzzle(0));
            Assert.AreEqual(ChunkKind.MirrorPuzzle, RoomLayout.DeductionPuzzle(1));
            Assert.AreEqual(RoomKind.Puzzle, RoomLayout.KindOf(ChunkKind.MirrorPuzzle));
            var stage = FeatureTestStage.Build();
            Assert.AreEqual(ChunkKind.MirrorPuzzle, stage.Chunks[FeatureTestStage.MirrorArea].Kind);
            Assert.AreEqual("거울 룬 방", FeatureTestStage.Names[FeatureTestStage.MirrorArea]);
        }
    
        // 방어막 다리: 단단한 선반 위 서로 다른 속성 분출 둘(입구 열로 튕김), 끝에 동전 셋, 마력 안개. 선반 끝까지 걸어서 갈 수 있고 방은 지나간다.
        [Test]
        public void ShieldBridgeHasTwoDifferentJetsAndCoinsAtTheEnd()
        {
            foreach (int entry in new[] { ChunkGenerator.MinGround, 7, ChunkGenerator.MaxGround })
                for (int seed = 0; seed < 30; seed++)
                {
                    var chunk = ChunkGenerator.Generate(seed, 3, ChunkKind.ShieldBridge, 0.5f, entry, 0);
                    string at = $"seed {seed} entry {entry}";
                    int stand = entry + ChunkGenerator.ShieldLedgeRise;
                    var jets = chunk.Features.Where(f => f.Kind == FeatureKind.ElementJet).ToList();
                    Assert.AreEqual(2, jets.Count, at);
                    Assert.AreNotEqual(jets[0].ElementIndex, jets[1].ElementIndex, at);
                    Assert.IsTrue(jets.All(j => j.Y == stand && j.Direction == ChunkGenerator.ShieldReturnX && j.ElementIndex < ChunkGenerator.GateElementCount), at);
                    var coins = chunk.Features.Where(f => f.Kind == FeatureKind.CoinPile).ToList();
                    Assert.AreEqual(ChunkGenerator.ShieldCoinCount, coins.Count, at);
                    Assert.IsTrue(coins.All(c => c.Y == stand && c.X > jets.Max(j => j.X + j.Width - 1)), at);
                    for (int x = ChunkGenerator.ShieldLedgeX0; x <= ChunkGenerator.ShieldLedgeX1; x++)
                        Assert.IsTrue(chunk.IsSolid(x, stand - 1), $"{at}: 선반 {x}");
                    Assert.AreEqual(StageTile.Empty, chunk.Get(ChunkGenerator.ShieldReturnX, stand), at);
                    Assert.IsTrue(chunk.Features.Any(f => f.Kind == FeatureKind.BlinkWard), at);
                    Assert.IsTrue(StageValidator.Validate(chunk, out string reason), $"{at}: {reason}");
                    // 선반 끝(동전)까지 가는 길이 있다: 동전 칸을 출구로 삼아 검사
                    var probe = ChunkGenerator.Generate(seed, 3, ChunkKind.ShieldBridge, 0.5f, entry, 0);
                    probe.Features.Add(new ChunkFeature { Kind = FeatureKind.Exit, X = coins[0].X, Y = stand, Width = 1 });
                    Assert.IsTrue(StageValidator.Validate(probe, out reason), $"{at}: 동전까지 {reason}");
                }
        }

        [Test]
        public void SecondPuzzleCyclesThroughThreeKinds()
        {
            Assert.AreEqual(ChunkKind.ShieldBridge, RoomLayout.DeductionPuzzle(2));
            Assert.AreEqual(ChunkKind.LanternPuzzle, RoomLayout.DeductionPuzzle(3));
            Assert.AreEqual(RoomKind.Puzzle, RoomLayout.KindOf(ChunkKind.ShieldBridge));
            var stage = FeatureTestStage.Build();
            Assert.AreEqual(ChunkKind.ShieldBridge, stage.Chunks[FeatureTestStage.ShieldArea].Kind);
            Assert.AreEqual("방어막 다리 방", FeatureTestStage.Names[FeatureTestStage.ShieldArea]);
        }
    }
}
