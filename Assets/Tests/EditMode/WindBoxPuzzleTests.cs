using System.Linq;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 퍼즐 방 "바람 상자" (2026-11-22, Docs/PuzzleRoomDesign.md 4번): 높은 한쪽 발판 선반(회복 구슬 + 동전) + 마력 안개 + 칸 단위로 미는 돌 상자.
    public class WindBoxPuzzleTests
    {
        private static ChunkData Make(int seed, int entry = 7) =>
            ChunkGenerator.Generate(seed, 3, ChunkKind.WindBoxPuzzle, 0.5f, entry, 0);

        private static ChunkFeature Box(ChunkData chunk) => chunk.Features.Single(f => f.Kind == FeatureKind.WindBox);

        [Test]
        public void RoomHasLedgeRewardWardAndBox()
        {
            bool sawLeft = false, sawRight = false;
            for (int seed = 0; seed < 60; seed++)
            {
                var chunk = Make(seed);
                string at = $"seed {seed}";
                var box = Box(chunk);
                int x0 = box.Direction, w = box.Width;
                Assert.AreEqual(ChunkGenerator.WindLedgeWidth, w, at);
                Assert.IsTrue(x0 == ChunkGenerator.WindLedgeX0(true) || x0 == ChunkGenerator.WindLedgeX0(false), at);
                if (x0 == ChunkGenerator.WindLedgeX0(true)) sawRight = true; else sawLeft = true;

                // 선반: 바닥 + 2 행에 한쪽 발판 w칸, 아래는 비어 있어 걸어 지나간다
                int row = 7 + ChunkGenerator.WindLedgeRise - 1;
                for (int x = x0; x < x0 + w; x++)
                {
                    Assert.AreEqual(StageTile.Platform, chunk.Get(x, row), $"{at} ledge {x}");
                    Assert.AreEqual(StageTile.Empty, chunk.Get(x, 7), $"{at} under ledge {x}");
                    Assert.AreEqual(StageTile.Empty, chunk.Get(x, 8), $"{at} under ledge {x}");
                }

                // 보상은 선반 위 서는 칸
                var reward = chunk.Features.Single(f => f.Kind == FeatureKind.Reward);
                var coins = chunk.Features.Single(f => f.Kind == FeatureKind.CoinPile);
                foreach (var f in new[] { reward, coins })
                {
                    Assert.AreEqual(7 + ChunkGenerator.WindLedgeRise, f.Y, at);
                    Assert.That(f.X, Is.InRange(x0, x0 + w - 1), at);
                }

                // 안개는 선반 양옆으로 넓게, 상자 처음 자리는 안개 밖 (안개 안에서도 밀 수 있지만 처음엔 알아보기 쉽게)
                var ward = chunk.Features.Single(f => f.Kind == FeatureKind.BlinkWard);
                Assert.AreEqual(x0 - ChunkGenerator.WindWardMargin, ward.X, at);
                Assert.AreEqual(w + 2 * ChunkGenerator.WindWardMargin, ward.Width, at);
                Assert.IsFalse(box.X >= ward.X && box.X < ward.X + ward.Width, $"{at} box {box.X} in ward");

                // 상자는 바닥 위, 몸 칸이 비어 있고, 벽 틈 안쪽
                Assert.AreEqual(7, box.Y, at);
                Assert.IsTrue(chunk.IsSolid(box.X, 6), at);
                Assert.AreEqual(StageTile.Empty, chunk.Get(box.X, 7), at);
                Assert.That(box.X, Is.InRange(WindBoxRules.MinX, WindBoxRules.MaxX(chunk)), at);
                Assert.IsFalse(chunk.Features.Any(f => f.Kind == FeatureKind.Enemy), at);
                Assert.IsTrue(StageValidator.Validate(chunk, out string reason), $"{at}: {reason}");
            }
            Assert.IsTrue(sawLeft && sawRight, "선반이 양쪽 모두 나온다");
        }

        // 걷기 점프(약 2.45칸)로는 선반(바닥 + 3)에 못 오르고, 상자(바닥 + 1) 위에서는 오른다.
        [Test]
        public void LedgeNeedsTheBox()
        {
            Assert.Greater(ChunkGenerator.WindLedgeRise, 2.45f);
            Assert.LessOrEqual(ChunkGenerator.WindLedgeRise - 1, 2.45f);
        }

        // 상자를 선반 쪽으로 WindBoxStartGap - 1번 밀면 디딤 자리에 닿는다. 벽 쪽으로는 벽 틈에서 멈춘다.
        [Test]
        public void PushingReachesTheStepAndStopsBeforeWalls()
        {
            foreach (int entry in new[] { ChunkGenerator.MinGround, 7, ChunkGenerator.MaxGround })
                for (int seed = 0; seed < 20; seed++)
                {
                    var chunk = Make(seed, entry);
                    var box = Box(chunk);
                    string at = $"entry {entry} seed {seed}";
                    Assert.AreEqual(ChunkGenerator.WindBoxStartGap - 1, WindBoxRules.PushesToStep(chunk, box.X, box.Y, box.Direction, box.Width), at);

                    int x = box.X;
                    for (int i = 0; i < chunk.Cols; i++) x = WindBoxRules.Push(chunk, x, box.Y, -1);
                    Assert.AreEqual(WindBoxRules.MinX, x, at);
                    for (int i = 0; i < chunk.Cols; i++) x = WindBoxRules.Push(chunk, x, box.Y, 1);
                    Assert.AreEqual(WindBoxRules.MaxX(chunk), x, at);
                }
        }

        [Test]
        public void PushRules()
        {
            var chunk = new ChunkData();
            for (int x = 0; x < ChunkData.Width; x++) chunk.FillColumn(x, 5);
            Assert.AreEqual(11, WindBoxRules.Push(chunk, 10, 5, 1));
            Assert.AreEqual(9, WindBoxRules.Push(chunk, 10, 5, -1));
            Assert.AreEqual(10, WindBoxRules.Push(chunk, 10, 5, 0));

            chunk.Tiles[11, 5] = StageTile.Solid; // 앞이 땅이면 쿵
            Assert.AreEqual(10, WindBoxRules.Push(chunk, 10, 5, 1));
            chunk.Tiles[9, 4] = StageTile.Empty;  // 앞 칸 아래가 구멍이면 쿵 (떨어지지 않는다)
            Assert.AreEqual(10, WindBoxRules.Push(chunk, 10, 5, -1));

            Assert.AreEqual(1, WindBoxRules.DirectionFrom(22f));
            Assert.AreEqual(-1, WindBoxRules.DirectionFrom(-0.5f));
            Assert.AreEqual(0, WindBoxRules.DirectionFrom(0f));

            Assert.IsTrue(WindBoxRules.IsStep(19, 20, 4));
            Assert.IsTrue(WindBoxRules.IsStep(24, 20, 4));
            Assert.IsFalse(WindBoxRules.IsStep(18, 20, 4));
            Assert.IsFalse(WindBoxRules.IsStep(25, 20, 4));
        }

        [Test]
        public void FallbackKeepsThePuzzleAndFeatureMapHasIt()
        {
            var fallback = ChunkGenerator.Fallback(3, ChunkKind.WindBoxPuzzle, 0.5f, 7);
            Assert.AreEqual(1, fallback.Features.Count(f => f.Kind == FeatureKind.WindBox));
            Assert.IsTrue(StageValidator.Validate(fallback, out string reason), reason);

            var stage = FeatureTestStage.Build();
            Assert.AreEqual(ChunkKind.WindBoxPuzzle, stage.Chunks[FeatureTestStage.WindBoxArea].Kind);
            Assert.AreEqual("바람 상자 방", FeatureTestStage.Names[FeatureTestStage.WindBoxArea]);
            Assert.AreEqual(RoomKind.Puzzle, RoomLayout.KindOf(ChunkKind.WindBoxPuzzle));
        }

        // 판마다 둘째 퍼즐로 바람 상자가 나오는 시드가 있고, 그 방은 방 격자에서도 상자·선반을 그대로 갖는다 (몹 없음).
        [Test]
        public void WindBoxRoomsAppearInLayouts()
        {
            int found = 0;
            for (int seed = 0; seed < 30 && found < 5; seed++)
            {
                if (RoomLayout.DeductionPuzzle(seed) != ChunkKind.WindBoxPuzzle) continue;
                var layout = RoomLayout.Generate(seed, 20f * 60f);
                var room = layout.Rooms.Find(r => r.Chunk == ChunkKind.WindBoxPuzzle);
                Assert.IsNotNull(room, $"seed {seed}");
                var geometry = RoomShaper.Build(seed, room);
                Assert.IsTrue(geometry.Valid, $"seed {seed}: {geometry.Reason}");
                Assert.AreEqual(1, geometry.Chunk.Features.Count(f => f.Kind == FeatureKind.WindBox), $"seed {seed}");
                Assert.IsFalse(geometry.Chunk.Features.Any(f => f.Kind == FeatureKind.Enemy), $"seed {seed}");
                found++;
            }
            Assert.AreEqual(5, found);
        }
    }
}
