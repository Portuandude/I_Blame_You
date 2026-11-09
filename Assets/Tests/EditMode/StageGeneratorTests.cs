using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 청크식 스테이지 생성기(순수 C#) 검증. Tools/StageGen/StagePreview가 하던 검사를 CI에서도 돌린다.
    public class StageGeneratorTests
    {
        private const float TargetSeconds = 30f * 60f;
        private const int SeedCount = 40;

        [Test]
        public void AllChunksPassValidator()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var stage = StageGenerator.Generate(seed, TargetSeconds);
                for (int i = 0; i < stage.Chunks.Count; i++)
                {
                    bool valid = StageValidator.Validate(stage.Chunks[i], out string reason);
                    Assert.IsTrue(valid, $"seed {seed} chunk {i} ({stage.Chunks[i].Kind}): {reason}");
                }
            }
        }

        // 생성이 계속 실패할 때 쓰는 대체 청크도 검증을 통과해야 한다 (Generate는 보통 검증된 청크만 남기므로 따로 본다).
        [Test]
        public void FallbackChunksPassValidator()
        {
            foreach (ChunkKind kind in System.Enum.GetValues(typeof(ChunkKind)))
            {
                for (int height = 4; height <= 10; height += 3)
                {
                    var chunk = ChunkGenerator.Fallback(1, kind, 0.5f, height);
                    bool valid = StageValidator.Validate(chunk, out string reason);
                    Assert.IsTrue(valid, $"fallback {kind} entry {height}: {reason}");
                }
            }
        }

        [Test]
        public void ChunkSeamsHaveMatchingHeights()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var stage = StageGenerator.Generate(seed, TargetSeconds);
                for (int i = 1; i < stage.Chunks.Count; i++)
                {
                    Assert.AreEqual(stage.Chunks[i - 1].ExitHeight, stage.Chunks[i].EntryHeight, $"seed {seed} chunk {i}");
                }
            }
        }

        [Test]
        public void StageStartsWithStartAndEndsWithBoss()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var stage = StageGenerator.Generate(seed, TargetSeconds);
                Assert.Greater(stage.Chunks.Count, 2, $"seed {seed}");
                Assert.AreEqual(ChunkKind.Start, stage.Chunks[0].Kind, $"seed {seed}");
                Assert.AreEqual(ChunkKind.Boss, stage.Chunks[stage.Chunks.Count - 1].Kind, $"seed {seed}");
                Assert.IsTrue(stage.Chunks[stage.Chunks.Count - 1].HasFeature(FeatureKind.Exit), $"seed {seed}: 보스 청크에 출구 없음");
            }
        }

        [Test]
        public void ChunkIndicesAreSequential()
        {
            var stage = StageGenerator.Generate(7, TargetSeconds);
            for (int i = 0; i < stage.Chunks.Count; i++) Assert.AreEqual(i, stage.Chunks[i].Index);
            Assert.AreEqual(stage.Chunks.Count * ChunkData.Width, stage.WidthTiles);
        }

        [Test]
        public void EstimatedTimeIsNearTarget()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var stage = StageGenerator.Generate(seed, TargetSeconds);
                Assert.That(stage.EstimatedSeconds, Is.InRange(TargetSeconds * 0.9f, TargetSeconds * 1.1f), $"seed {seed}");
            }
        }

        [Test]
        public void SameSeedGivesSameStage()
        {
            var a = StageGenerator.Generate(1234, TargetSeconds);
            var b = StageGenerator.Generate(1234, TargetSeconds);
            Assert.AreEqual(a.Chunks.Count, b.Chunks.Count);
            for (int i = 0; i < a.Chunks.Count; i++)
            {
                Assert.AreEqual(a.Chunks[i].Kind, b.Chunks[i].Kind, $"chunk {i}");
                Assert.AreEqual(a.Chunks[i].Features.Count, b.Chunks[i].Features.Count, $"chunk {i}");
                for (int x = 0; x < ChunkData.Width; x++)
                {
                    for (int y = 0; y < ChunkData.Height; y++)
                    {
                        Assert.AreEqual(a.Chunks[i].Tiles[x, y], b.Chunks[i].Tiles[x, y], $"chunk {i} ({x},{y})");
                    }
                }
            }
        }

        // ---- 봉인문 퍼즐 청크 ----

        private static ChunkFeature GateOf(ChunkData chunk)
        {
            ChunkFeature gate = default(ChunkFeature);
            int count = 0;
            foreach (var f in chunk.Features)
            {
                if (f.Kind != FeatureKind.ElementGate) continue;
                gate = f;
                count++;
            }
            Assert.AreEqual(1, count, $"chunk {chunk.Index}: 봉인문 개수");
            return gate;
        }

        [Test]
        public void ElementGateChunkHasOneGateWithStandingRoomOnBothSides()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (int entry in new[] { 4, 7, 10 })
                {
                    var chunk = ChunkGenerator.Generate(seed, 5, ChunkKind.ElementGate, 0.5f, entry, 0);
                    Assert.IsTrue(StageValidator.Validate(chunk, out string reason), $"seed {seed} entry {entry}: {reason}");
                    var gate = GateOf(chunk);
                    Assert.That(gate.ElementIndex, Is.InRange(0, ChunkGenerator.GateElementCount - 1));
                    Assert.IsFalse(chunk.HasFeature(FeatureKind.Enemy), $"seed {seed}: 퍼즐 청크에 적");
                    for (int y = gate.Y; y < ChunkData.Height; y++)
                        Assert.AreEqual(StageTile.Empty, chunk.Tiles[gate.X, y], $"seed {seed}: 봉인문 열 ({gate.X},{y})가 비어 있지 않음");
                    foreach (int x in new[] { gate.X - 1, gate.X + 1 })
                    {
                        Assert.IsTrue(chunk.IsSolid(x, gate.Y - 1), $"seed {seed}: ({x}) 발밑");
                        Assert.IsFalse(chunk.IsSolid(x, gate.Y) || chunk.IsSolid(x, gate.Y + 1), $"seed {seed}: ({x}) 설 자리");
                    }
                }
            }
        }

        // 봉인문 열을 땅으로 메우면 지나갈 수 없어야 한다 = 문을 열지 않고 돌아갈 길이 없다.
        [Test]
        public void ElementGateCannotBeBypassed()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var chunk = ChunkGenerator.Generate(seed, 5, ChunkKind.ElementGate, 0.5f, 7, 0);
                var gate = GateOf(chunk);
                for (int y = gate.Y; y < ChunkData.Height; y++) chunk.Tiles[gate.X, y] = StageTile.Solid;
                Assert.IsFalse(StageValidator.Validate(chunk, out _), $"seed {seed}: 봉인문을 우회할 수 있음");
            }
        }

        [Test]
        public void ElementGatePlacementRules()
        {
            int stagesWithGate = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var stage = StageGenerator.Generate(seed, TargetSeconds);
                int gates = 0;
                foreach (var chunk in stage.Chunks)
                {
                    if (chunk.Kind != ChunkKind.ElementGate) continue;
                    gates++;
                    Assert.That(chunk.Difficulty, Is.InRange(StagePlanner.GateMinProgress, StagePlanner.GateMaxProgress), $"seed {seed}");
                }
                Assert.That(gates, Is.InRange(0, 2), $"seed {seed}: 봉인문 개수");
                if (gates > 0) stagesWithGate++;
            }
            Assert.Greater(stagesWithGate, 0, "봉인문이 한 번도 나오지 않음");
        }
    }
}
