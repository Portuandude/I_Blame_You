using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 청크식 스테이지 생성기(순수 C#) 검증. Tools/StageGen/StagePreview가 하던 검사를 CI에서도 돌린다.
    public class StageGeneratorTests
    {
        private const float TargetSeconds = 15f * 60f; // 스테이지 1 목표 15분 (2026-11-10 사람 결정)
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
                // 15분 안팎 = 13~17분: 마지막 청크가 넘치는 몫(최대 100초 남짓)이 15분에서는 10%를 넘을 수 있어 ±15%
                Assert.That(stage.EstimatedSeconds, Is.InRange(TargetSeconds * 0.85f, TargetSeconds * 1.15f), $"seed {seed}");
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

        // 진행도 후반의 봉인문은 서로 다른 두 속성을 순서대로 요구한다.
        [Test]
        public void LateElementGatesNeedTwoDifferentElements()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (float difficulty in new[] { 0.3f, 0.59f, 0.6f, 0.85f })
                {
                    var gate = GateOf(ChunkGenerator.Generate(seed, 5, ChunkKind.ElementGate, difficulty, 7, 0));
                    bool late = difficulty >= ChunkGenerator.TwoElementGateMinDifficulty;
                    Assert.AreEqual(late, gate.HasSecondElement, $"seed {seed} difficulty {difficulty}");
                    if (!late) continue;
                    Assert.That(gate.SecondElementIndex, Is.InRange(0, ChunkGenerator.GateElementCount - 1));
                    Assert.IsFalse(gate.SecondElementIndex == gate.ElementIndex, $"seed {seed}: 같은 속성 두 번");
                }
            }
        }

        // ---- 스테이지 배치 규칙 (StagePlanner) ----

        // 정예 전투는 정해진 진행도를 넘은 뒤 첫 전투 구역에 하나씩, 그 뒤에는 바로 휴식이 온다.
        // (정예 2개는 목표 30분 기준. 목표 시간을 크게 줄이면 둘째 정예 전에 보스 구간이 시작될 수 있다.)
        [Test]
        public void ElitesAppearAtTheirProgressAndAreFollowedByRest()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var plan = StagePlanner.Plan(seed, TargetSeconds);
                int elites = 0;
                for (int i = 0; i < plan.Count; i++)
                {
                    bool due = elites < StagePlanner.EliteProgress.Count && plan[i].Value >= StagePlanner.EliteProgress[elites];
                    Assert.IsFalse(plan[i].Key == ChunkKind.Arena && due, $"seed {seed}: 정예 차례인데 일반 전투 구역 ({i})");
                    if (plan[i].Key != ChunkKind.EliteArena) continue;
                    Assert.Less(elites, StagePlanner.EliteProgress.Count, $"seed {seed}: 정예가 너무 많음");
                    Assert.That(plan[i].Value, Is.InRange(StagePlanner.EliteProgress[elites], 1.0), $"seed {seed}: {elites + 1}번째 정예 위치");
                    Assert.AreEqual(ChunkKind.Rest, plan[i + 1].Key, $"seed {seed}: 정예 뒤 휴식");
                    elites++;
                }
                Assert.AreEqual(StagePlanner.EliteProgress.Count, elites, $"seed {seed}: 정예 개수");
            }
        }

        // 시작 → 평지로 열고, 휴식 → 보스로 닫는다. 전투 구역이 휴식 없이 너무 이어지지 않는다.
        [Test]
        public void PlanOpensAndClosesAndRestsRegularly()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var plan = StagePlanner.Plan(seed, TargetSeconds);
                Assert.AreEqual(ChunkKind.Start, plan[0].Key, $"seed {seed}");
                Assert.AreEqual(ChunkKind.Plains, plan[1].Key, $"seed {seed}");
                Assert.AreEqual(ChunkKind.Rest, plan[plan.Count - 2].Key, $"seed {seed}");
                Assert.AreEqual(ChunkKind.Boss, plan[plan.Count - 1].Key, $"seed {seed}");

                int arenasSinceRest = 0;
                foreach (var entry in plan)
                {
                    if (entry.Key == ChunkKind.Rest) arenasSinceRest = 0;
                    else if (entry.Key == ChunkKind.Arena || entry.Key == ChunkKind.EliteArena) arenasSinceRest++;
                    Assert.That(arenasSinceRest, Is.InRange(0, StagePlanner.ArenasPerRest), $"seed {seed}: 휴식 없이 전투 구역 연속");
                }
            }
        }

        // 이동 청크는 같은 종류가 연달아 나오지 않고, 어려운 지형은 진행도 문턱을 넘은 뒤에만 나온다.
        [Test]
        public void TravelChunksRespectProgressFloorsAndDontRepeat()
        {
            var floors = new Dictionary<ChunkKind, float>
            {
                { ChunkKind.Cave, 0.15f }, { ChunkKind.Climb, 0.2f }, { ChunkKind.MovingBridge, 0.2f },
                { ChunkKind.CrumbleBridge, 0.3f }, { ChunkKind.ElementGate, StagePlanner.GateMinProgress },
                { ChunkKind.Pillars, StagePlanner.PillarsMinProgress }, { ChunkKind.TwinPath, StagePlanner.TwinPathMinProgress },
            };
            var travel = new HashSet<ChunkKind>
            {
                ChunkKind.Plains, ChunkKind.Hills, ChunkKind.SpikeField, ChunkKind.Pits, ChunkKind.Cave, ChunkKind.Climb,
                ChunkKind.CrumbleBridge, ChunkKind.MovingBridge, ChunkKind.ElementGate, ChunkKind.Pillars, ChunkKind.TwinPath,
            };
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var plan = StagePlanner.Plan(seed, TargetSeconds);
                for (int i = 2; i < plan.Count; i++) // 0, 1은 고정된 시작·평지
                {
                    var kind = plan[i].Key;
                    if (!travel.Contains(kind)) continue;
                    Assert.AreNotEqual(plan[i - 1].Key, kind, $"seed {seed}: {kind} 연속 ({i})");
                    if (floors.TryGetValue(kind, out float floor))
                        Assert.That(plan[i].Value, Is.InRange(floor, 1.0), $"seed {seed}: {kind}가 진행도 {plan[i].Value:0.00}에 나옴");
                }
            }
        }

        // ---- 움직이는 발판 청크 (컨트롤 퍼즐) ----

        [Test]
        public void MovingBridgeNeedsThePlatform()
        {
            int doubles = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (float difficulty in new[] { 0.3f, 0.69f, 0.7f, 0.9f })
                {
                    var chunk = ChunkGenerator.Generate(seed, 6, ChunkKind.MovingBridge, difficulty, 7, 0);
                    Assert.IsTrue(StageValidator.Validate(chunk, out string reason), $"seed {seed}: {reason}");

                    var platforms = chunk.Features.FindAll(f => f.Kind == FeatureKind.MovingPlatform);
                    bool canBeDouble = difficulty >= ChunkGenerator.DoubleBridgeMinDifficulty;
                    Assert.That(platforms.Count, Is.InRange(1, canBeDouble ? 2 : 1), $"seed {seed}");
                    if (platforms.Count == 2)
                    {
                        doubles++;
                        // 갈아타기 변형의 낭떠러지는 달리기 점프(약 7.3칸)로 못 건너는 폭이어야 한다 (검증기는 걷기 점프 기준).
                        foreach (var p in platforms)
                            Assert.That(p.Width + p.Direction, Is.InRange(8, ChunkData.Width), $"seed {seed}: 갈아타기 낭떠러지 폭");
                    }

                    foreach (var platform in platforms)
                    {
                        // 발판 윗면(Y + 1)은 두 절벽 중 높은 쪽과 같은 높이에서 출발한다.
                        Assert.AreEqual(System.Math.Max(chunk.EntryHeight, chunk.ExitHeight), platform.Y + 1, $"seed {seed}: 발판 높이");

                        // 발판이 지나는 범위가 낭떠러지를 끝에서 끝까지 덮는다.
                        int left = platform.X, right = platform.X + platform.Width + platform.Direction - 1;
                        Assert.IsTrue(chunk.IsSolid(left - 1, platform.Y), $"seed {seed}: 출발 쪽 절벽");
                        Assert.IsFalse(chunk.IsSolid(left, 0), $"seed {seed}: 낭떠러지 시작");
                        Assert.IsFalse(chunk.IsSolid(right, 0), $"seed {seed}: 낭떠러지 끝");
                        Assert.IsTrue(chunk.IsSolid(right + 1, 0), $"seed {seed}: 건너편 절벽");
                    }

                    // 발판이 하나라도 없으면 건널 수 없어야 퍼즐이다 (갈아타기 변형은 각 발판마다).
                    for (int skip = 0; skip < platforms.Count; skip++)
                    {
                        var without = ChunkGenerator.Generate(seed, 6, ChunkKind.MovingBridge, difficulty, 7, 0);
                        var removed = platforms[skip];
                        without.Features.RemoveAll(f => f.Kind == FeatureKind.MovingPlatform && f.X == removed.X);
                        Assert.IsFalse(StageValidator.Validate(without, out _), $"seed {seed}: {skip + 1}번째 발판 없이도 건널 수 있음");
                    }
                }
            }
            Assert.Greater(doubles, 0, "갈아타기 변형이 한 번도 나오지 않음");
        }

        // 마법 스위치 발판: 진행도 50% 이후 한 발판 낭떠러지, 80% 이후 갈아타기(이중 스위치)에도. k번째 룬은 k번째 스위치 발판의
        // 건너편 첫 칸에 서고, 그 발판 출발 쪽 땅 끝에서 수평으로 쏜 마법이 닿는 거리·높이여야 한다 (사거리가 가장 짧은 물의 창 12칸).
        [Test]
        public void SwitchBridgeRuneIsReachableFromTheEntrySide()
        {
            int singles = 0, doubles = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (float difficulty in new[] { 0.3f, 0.49f, 0.5f, 0.6f, 0.79f, 0.8f, 0.9f })
                {
                    var chunk = ChunkGenerator.Generate(seed, 6, ChunkKind.MovingBridge, difficulty, 7, 0);
                    var runes = chunk.Features.FindAll(f => f.Kind == FeatureKind.PlatformSwitch);
                    var platforms = chunk.Features.FindAll(f => f.Kind == FeatureKind.MovingPlatform);
                    var driven = platforms.FindAll(f => f.SwitchDriven);
                    var wards = chunk.Features.FindAll(f => f.Kind == FeatureKind.BlinkWard);
                    if (difficulty < ChunkGenerator.SwitchBridgeMinDifficulty)
                    {
                        Assert.AreEqual(0, runes.Count, $"seed {seed}: 50% 전에는 스위치 없음");
                        Assert.AreEqual(0, wards.Count, $"seed {seed}: 스위치 없으면 마력 안개 없음");
                        continue;
                    }
                    Assert.AreEqual(runes.Count, driven.Count, $"seed {seed}: 룬 하나에 스위치 발판 하나");
                    Assert.AreEqual(driven.Count, wards.Count, $"seed {seed}: 스위치 발판 낭떠러지마다 마력 안개 하나");
                    if (runes.Count == 0) continue;
                    Assert.AreEqual(platforms.Count, driven.Count, $"seed {seed}: 스위치 변형이면 발판 모두 스위치");
                    if (driven.Count == 2)
                    {
                        Assert.GreaterOrEqual(difficulty, ChunkGenerator.DoubleSwitchBridgeMinDifficulty, $"seed {seed}: 이중 스위치는 80% 이후");
                        Assert.AreNotEqual(runes[0].ElementIndex, runes[1].ElementIndex, $"seed {seed}: 이중 스위치 룬 속성은 서로 다름");
                        doubles++;
                    }
                    else singles++;
                    Assert.IsTrue(StageValidator.Validate(chunk, out string reason), $"seed {seed}: {reason}");

                    for (int k = 0; k < runes.Count; k++)
                    {
                        var rune = runes[k];
                        var platform = driven[k];
                        int farCliff = platform.X + platform.Width + platform.Direction; // 발판 건너편 첫 칸

                        // 2026-11-12 플레이테스트: 순간이동·대시로 건너면 안 된다. 낭떠러지 전체를 마력 안개(순간이동·대시 금지)가 덮고,
                        // 폭은 달리기 점프(약 7.3칸)보다 넓다. 멈춘 스위치 발판은 밟을 수 없는 잠든 발판이라 그 위에서 뛰어 줄일 수도 없다 (MovingPlatform).
                        int gap = farCliff - platform.X;
                        Assert.Greater(gap, RunJumpReach, $"seed {seed}: {k}번 스위치 낭떠러지 폭 {gap}");
                        Assert.AreEqual(platform.X, wards[k].X, $"seed {seed}: {k}번 안개 시작");
                        Assert.AreEqual(gap, wards[k].Width, $"seed {seed}: {k}번 안개 폭 = 낭떠러지 폭");
                        for (int x = wards[k].X; x < wards[k].X + wards[k].Width; x++)
                            Assert.AreEqual(0, Surface(chunk, x), $"seed {seed}: 안개 ({x}열)는 낭떠러지 위만");
                        Assert.AreEqual(farCliff, rune.X, $"seed {seed}: {k}번 룬은 발판 건너편 첫 칸");
                        Assert.AreEqual(Surface(chunk, rune.X), rune.Y, $"seed {seed}: {k}번 룬은 땅 위");
                        Assert.That(rune.ElementIndex, Is.InRange(0, ChunkGenerator.GateElementCount - 1), $"seed {seed}");

                        // 출발 쪽 땅 끝(발판 바로 왼쪽 칸 가운데)에서 룬 가운데까지 12칸 안.
                        float distance = (rune.X + 0.5f) - (platform.X - 0.5f);
                        Assert.LessOrEqual(distance, 12f - 0.5f, $"seed {seed}: {k}번 룬까지 거리");
                        // 마법은 선 높이 + 약 0.7에서 수평으로 난다. 룬 트리거는 룬 바닥 + 0.1 ~ 1.8.
                        float shot = Surface(chunk, platform.X - 1) + 0.7f;
                        Assert.That(shot, Is.InRange(rune.Y + 0.1f, rune.Y + 1.8f), $"seed {seed}: {k}번 룬 높이");
                    }

                    // 발판이 없으면 건널 수 없다 (스위치를 안 맞히면 발판이 오지 않는다).
                    var without = ChunkGenerator.Generate(seed, 6, ChunkKind.MovingBridge, difficulty, 7, 0);
                    without.Features.RemoveAll(f => f.Kind == FeatureKind.MovingPlatform);
                    Assert.IsFalse(StageValidator.Validate(without, out _), $"seed {seed}: 스위치 발판 없이도 건널 수 있음");
                }
            }
            Assert.Greater(singles, 0, "마법 스위치 변형이 한 번도 나오지 않음");
            Assert.Greater(doubles, 0, "이중 스위치 변형이 한 번도 나오지 않음");
        }

        // 이중 스위치 퍼즐: 판마다(외길·방 격자 모두) 정확히 하나, 진행도 SwitchBridgeDepth 뒤에. 그 청크에는 룬 두 개·스위치 발판 두 개·안개 두 개.
        [Test]
        public void EveryStageHasOneDoubleSwitchPuzzle()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var stage = StageGenerator.Generate(seed, TargetSeconds);
                var bridges = stage.Chunks.FindAll(c => c.Kind == ChunkKind.SwitchBridge);
                Assert.AreEqual(1, bridges.Count, $"seed {seed}: 외길 이중 스위치 수");
                Assert.GreaterOrEqual(bridges[0].Difficulty, StagePlanner.SwitchBridgeDepth, $"seed {seed}");
                Assert.AreEqual(2, bridges[0].Features.FindAll(f => f.Kind == FeatureKind.PlatformSwitch).Count, $"seed {seed}: 룬 둘");
                Assert.AreEqual(2, bridges[0].Features.FindAll(f => f.Kind == FeatureKind.MovingPlatform && f.SwitchDriven).Count, $"seed {seed}: 스위치 발판 둘");
                Assert.AreEqual(2, bridges[0].Features.FindAll(f => f.Kind == FeatureKind.BlinkWard).Count, $"seed {seed}: 안개 둘");

                var rooms = RoomLayout.Generate(seed, TargetSeconds).Rooms.FindAll(r => r.Chunk == ChunkKind.SwitchBridge);
                Assert.AreEqual(1, rooms.Count, $"seed {seed}: 방 격자 이중 스위치 수");
                Assert.AreEqual(RoomKind.Puzzle, rooms[0].Kind, $"seed {seed}");
            }
        }

        // 깜빡 발판 다리 (Docs/ContentIdeas.md 퍼즐 1): 무너지는 발판 다리의 변형. 난이도 40% 이상에서만, 박자 순번이 왼쪽부터 0, 1, 2…
        [Test]
        public void BlinkBridgesAppearLateWithSequentialBeats()
        {
            int blinkBridges = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                foreach (float difficulty in new[] { 0.2f, 0.6f })
                {
                    var chunk = ChunkGenerator.Generate(seed, 3, ChunkKind.CrumbleBridge, difficulty, 6, 0);
                    var blinks = chunk.Features.FindAll(f => f.Kind == FeatureKind.BlinkPlatform);
                    if (blinks.Count == 0) continue;
                    Assert.GreaterOrEqual(difficulty, ChunkGenerator.BlinkBridgeMinDifficulty, $"seed {seed}");
                    Assert.AreEqual(0, chunk.Features.FindAll(f => f.Kind == FeatureKind.Crumble).Count, $"seed {seed}: 한 다리는 한 종류");
                    for (int i = 0; i < blinks.Count; i++) Assert.AreEqual(i, blinks[i].Direction, $"seed {seed}");
                    blinkBridges++;
                }
            }

            Assert.Greater(blinkBridges, 20, "깜빡 발판 다리가 거의 안 나옴");
        }

        // 기둥 숲 (레벨 재설계 2차): 낭떠러지가 여럿, 기둥은 폭 1~2칸, 오르는 낭떠러지는 2칸 이하, 검증기 통과.
        // 무너지는 머리는 난이도 문턱 이상에서만, 그 아래에 한 칸 낮은 기둥 몸통이 있다.
        [Test]
        public void PillarsAreNarrowJumpableAndCrumbleLate()
        {
            int crumbleHeads = 0, narrow = 0, valid = 0, total = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                foreach (float difficulty in new[] { 0.2f, 0.45f, 0.8f })
                {
                    foreach (int entry in new[] { 3, 6, 12 })
                    {
                        var chunk = ChunkGenerator.Generate(seed, 3, ChunkKind.Pillars, difficulty, entry, 0);
                        string tag = $"seed {seed} diff {difficulty} entry {entry}";
                        var crumbles = chunk.Features.FindAll(f => f.Kind == FeatureKind.Crumble);
                        foreach (var c in crumbles)
                        {
                            Assert.GreaterOrEqual(difficulty, ChunkGenerator.PillarCrumbleMinDifficulty, tag);
                            for (int i = 0; i < c.Width; i++) Assert.AreEqual(c.Y, Surface(chunk, c.X + i), tag + ": 무너지는 머리 아래 몸통");
                            crumbleHeads++;
                        }

                        int pits = 0, x = 0;
                        while (x < ChunkData.Width)
                        {
                            if (Surface(chunk, x) > 0) { x++; continue; }
                            int start = x;
                            while (x < ChunkData.Width && Surface(chunk, x) == 0) x++;
                            pits++;
                            Assert.Less(x, ChunkData.Width, tag + ": 낭떠러지로 끝남");
                            int gap = x - start;
                            int before = Surface(chunk, start - 1), after = Surface(chunk, x);
                            foreach (var c in crumbles)
                            {
                                if (c.X == x) after = c.Y + 1;
                                if (c.X + c.Width - 1 == start - 1) before = c.Y + 1; // 무너지기 전 머리 위에서 뛴다
                            }
                            Assert.LessOrEqual(gap, 3, tag);
                            if (after > before) Assert.LessOrEqual(gap, 2, tag + $": 오르며 {gap}칸 ({start})");
                            Assert.LessOrEqual(after - before, 1, tag + $": 한 번에 {after - before}칸 오름 ({start})");
                            int width = 0;
                            while (x + width < ChunkData.Width && Surface(chunk, x + width) > 0) width++;
                            if (x + width < ChunkData.Width) Assert.LessOrEqual(width, 2, tag + $": 기둥 폭 ({x})");
                            if (width == 1) narrow++;
                        }

                        Assert.GreaterOrEqual(pits, 4, tag + ": 낭떠러지가 적음");
                        if (StageValidator.Validate(chunk, out _)) valid++;
                        total++;
                    }
                }
            }

            Assert.Greater(crumbleHeads, 50, "무너지는 머리가 거의 안 나옴");
            Assert.Greater(narrow, 100, "폭 1칸 기둥이 거의 안 나옴");
            Assert.Greater(valid, total * 0.8, $"첫 시도 검증 통과 {valid}/{total} (실패하면 다른 시도 번호로 다시 만든다)");
        }

        // 두 갈래 길 (레벨 재설계 2차): 아래 길은 평평하고 가시·적이 계단 밑에 없다. 위 길은 2칸씩 오르는 계단 → 바닥 + 6 발판 줄,
        // 첫 발판은 단단하고 틈은 1~2칸, 출구 앞 4칸은 비어 내려설 수 있다. 무너지는 발판은 위 길 줄에만.
        [Test]
        public void TwinPathHasSafeStairsAndJumpableWalkway()
        {
            int crumbles = 0, enemies = 0, spikes = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                foreach (float difficulty in new[] { 0.3f, 0.7f })
                {
                    foreach (int entry in new[] { 3, 8, 13 })
                    {
                        var chunk = ChunkGenerator.Generate(seed, 3, ChunkKind.TwinPath, difficulty, entry, 0);
                        string tag = $"seed {seed} diff {difficulty} entry {entry}";
                        for (int x = 0; x < ChunkData.Width; x++) Assert.AreEqual(entry, Surface(chunk, x), tag + $": 아래 길 높이 ({x})");
                        Assert.AreEqual(entry, chunk.ExitHeight, tag);
                        Assert.AreEqual(StageTile.Platform, chunk.Get(3, entry + 1), tag + ": 계단 1");
                        Assert.AreEqual(StageTile.Platform, chunk.Get(6, entry + 3), tag + ": 계단 2");

                        int row = entry + ChunkGenerator.TwinPathWalkRise - 1;
                        Assert.AreEqual(StageTile.Platform, chunk.Get(ChunkGenerator.TwinPathWalkStart, row), tag + ": 첫 발판은 단단");
                        var walk = new bool[ChunkData.Width];
                        for (int x = 0; x < ChunkData.Width; x++) walk[x] = chunk.Get(x, row) == StageTile.Platform;
                        foreach (var f in chunk.Features)
                        {
                            if (f.Kind == FeatureKind.Crumble)
                            {
                                Assert.AreEqual(row, f.Y, tag + ": 무너지는 발판은 위 길에만");
                                for (int i = 0; i < f.Width; i++) walk[f.X + i] = true;
                                crumbles++;
                            }
                            if (f.Kind == FeatureKind.Spikes)
                            {
                                Assert.GreaterOrEqual(f.X, 9, tag + ": 계단 밑 가시");
                                Assert.LessOrEqual(f.X + f.Width, ChunkData.Width - 4, tag + ": 출구 가시");
                                spikes++;
                            }
                            if (f.Kind == FeatureKind.Enemy) { Assert.AreEqual(entry, f.Y, tag); enemies++; }
                        }

                        int gap = 0;
                        for (int x = ChunkGenerator.TwinPathWalkStart; x <= ChunkGenerator.TwinPathWalkEnd; x++)
                        {
                            gap = walk[x] ? 0 : gap + 1;
                            Assert.LessOrEqual(gap, 2, tag + $": 위 길 틈 ({x})");
                        }
                        for (int x = ChunkGenerator.TwinPathWalkEnd + 1; x < ChunkData.Width; x++) Assert.IsFalse(walk[x], tag + ": 출구 위는 비어야");
                        Assert.IsTrue(StageValidator.Validate(chunk, out string reason), tag + ": " + reason);
                    }
                }
            }

            Assert.Greater(crumbles, 300, "무너지는 발판이 거의 없음");
            Assert.Greater(enemies, 600, "아래 길 적이 거의 없음");
            Assert.Greater(spikes, 600, "아래 길 가시가 거의 없음");
        }

        // 동전 무더기 (위험한 길 보상): 두 갈래 길은 위 길 마지막 단단한 발판 위, 기둥 숲은 문턱 이상에서 가장 높은 단단한 기둥 머리 위.
        // 늘 서는 칸(빈 칸, 바로 아래가 땅이나 발판)에 놓이고 청크마다 하나 이하.
        [Test]
        public void CoinPilesSitOnRiskyPathEnds()
        {
            int twin = 0, pillar = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                foreach (float difficulty in new[] { 0.2f, 0.45f, 0.8f })
                {
                    foreach (int entry in new[] { 3, 8, 12 })
                    {
                        string tag = $"seed {seed} diff {difficulty} entry {entry}";
                        var path = ChunkGenerator.Generate(seed, 3, ChunkKind.TwinPath, difficulty, entry, 0);
                        var piles = path.Features.FindAll(f => f.Kind == FeatureKind.CoinPile);
                        Assert.LessOrEqual(piles.Count, 1, tag);
                        int row = entry + ChunkGenerator.TwinPathWalkRise - 1;
                        foreach (var c in piles)
                        {
                            Assert.AreEqual(row + 1, c.Y, tag + ": 위 길 발판 위");
                            Assert.AreEqual(StageTile.Platform, path.Get(c.X, row), tag + ": 단단한 발판 위");
                            Assert.Greater(c.X, ChunkGenerator.TwinPathWalkStart + 4, tag + ": 위 길 끝 쪽");
                            twin++;
                        }

                        var pillars = ChunkGenerator.Generate(seed, 3, ChunkKind.Pillars, difficulty, entry, 0);
                        piles = pillars.Features.FindAll(f => f.Kind == FeatureKind.CoinPile);
                        Assert.LessOrEqual(piles.Count, 1, tag);
                        if (difficulty < ChunkGenerator.PillarCoinMinDifficulty) Assert.AreEqual(0, piles.Count, tag + ": 문턱 아래");
                        foreach (var c in piles)
                        {
                            Assert.AreEqual(c.Y, Surface(pillars, c.X), tag + ": 기둥 머리 위");
                            foreach (var f in pillars.Features)
                                if (f.Kind == FeatureKind.Crumble)
                                    Assert.IsFalse(c.X >= f.X && c.X < f.X + f.Width, tag + ": 무너지는 머리 위");
                            for (int x = 4; x < ChunkData.Width - 4; x++)
                            {
                                bool crumbleCol = pillars.Features.Exists(f => f.Kind == FeatureKind.Crumble && x >= f.X && x < f.X + f.Width);
                                if (!crumbleCol) Assert.LessOrEqual(Surface(pillars, x), c.Y, tag + $": 더 높은 기둥 ({x})");
                            }
                            pillar++;
                        }
                    }
                }
            }

            Assert.Greater(twin, 1000, "두 갈래 길 동전이 거의 없음");
            Assert.Greater(pillar, 800, "기둥 숲 동전이 거의 없음");
        }

        // 떨어지는 종유석: 동굴 난이도 문턱 이상에서만, 천장 바로 아래 빈 칸에 매달리고, 분출구와 2칸 넘게·서로 6칸 이상 떨어진다.
        [Test]
        public void StalactitesHangUnderCaveCeilingLate()
        {
            int total = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                foreach (float difficulty in new[] { 0.2f, 0.5f, 0.8f })
                {
                    var chunk = ChunkGenerator.Generate(seed, 3, ChunkKind.Cave, difficulty, 6, 0);
                    var stones = chunk.Features.FindAll(f => f.Kind == FeatureKind.Stalactite);
                    string tag = $"seed {seed} diff {difficulty}";
                    if (difficulty < ChunkGenerator.StalactiteMinDifficulty) Assert.AreEqual(0, stones.Count, tag);
                    Assert.LessOrEqual(stones.Count, difficulty >= ChunkGenerator.StalactiteSecondDifficulty ? 2 : 1, tag);
                    foreach (var s in stones)
                    {
                        Assert.IsFalse(chunk.IsSolid(s.X, s.Y), tag + ": 매달린 칸은 비어야");
                        Assert.IsTrue(chunk.IsSolid(s.X, s.Y + 1), tag + ": 바로 위가 천장");
                        Assert.Greater(s.Y, Surface(chunk, s.X), tag + ": 바닥보다 위");
                        foreach (var f in chunk.Features)
                            if (f.Kind == FeatureKind.FlameJet) Assert.Greater(System.Math.Abs(f.X - s.X), 2, tag + ": 분출구 옆");
                        foreach (var o in stones)
                            if (o.X != s.X) Assert.GreaterOrEqual(System.Math.Abs(o.X - s.X), 6, tag + ": 종유석끼리");
                        total++;
                    }
                }
            }

            Assert.Greater(total, 300, "종유석이 거의 안 나옴");
        }

        private const float RunJumpReach = 7.3f; // 달리기 점프로 건너는 가로 칸 (PlayerMovement 수치 기준 어림)

        // x열에서 땅 위 첫 빈 칸 높이 (서 있는 칸).
        private static int Surface(ChunkData chunk, int x)
        {
            int y = 0;
            while (y < ChunkData.Height && chunk.IsSolid(x, y)) y++;
            return y;
        }
    }
}
