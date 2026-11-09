using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 방 지형(RoomShaper): 어느 문으로 들어와도 모든 문으로 나갈 수 있고, 문 자리가 실제로 뚫려 있는지.
    public class RoomShaperTests
    {
        private const float TargetSeconds = 30f * 60f;
        private const int SeedCount = 30;
        private static readonly DoorDir[] Dirs = { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down };

        [Test]
        public void EveryRoomPassesRoomValidator()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var layout = RoomLayout.Generate(seed, TargetSeconds);
                foreach (var room in layout.Rooms)
                {
                    var geometry = RoomShaper.Build(seed, room);
                    Assert.IsTrue(geometry.Valid, $"seed {seed} room {room.Index} ({room.Kind}/{room.Chunk}, {room.Doors}): {geometry.Reason}");
                }
            }
        }

        [Test]
        public void EveryDoorHasArrivalAndExitSpots()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    var geometry = RoomShaper.Build(seed, room);
                    foreach (var dir in Dirs)
                    {
                        bool door = (room.Doors & dir) != 0;
                        Assert.AreEqual(door, geometry.Arrivals.ContainsKey(dir), $"seed {seed} room {room.Index} {dir}");
                        Assert.AreEqual(door, geometry.Exits.ContainsKey(dir), $"seed {seed} room {room.Index} {dir}");
                    }
                }
            }
        }

        // 문이 없는 쪽은 벽·천장으로 막혀 있고, 문이 있는 쪽은 구멍이 나 있다.
        [Test]
        public void WallsAndHolesMatchDoors()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    var chunk = RoomShaper.Build(seed, room).Chunk;
                    int top = ChunkData.Height - 1;
                    if ((room.Doors & DoorDir.Left) == 0) Assert.IsTrue(chunk.IsSolid(0, top - 1), $"seed {seed} room {room.Index} 왼쪽 벽");
                    if ((room.Doors & DoorDir.Right) == 0) Assert.IsTrue(chunk.IsSolid(ChunkData.Width - 1, top - 1), $"seed {seed} room {room.Index} 오른쪽 벽");
                    if (!room.IsVertical) continue;

                    for (int x = RoomShaper.HoleX0; x <= RoomShaper.HoleX1; x++)
                    {
                        bool up = (room.Doors & DoorDir.Up) != 0;
                        bool down = (room.Doors & DoorDir.Down) != 0;
                        Assert.AreEqual(!up, chunk.IsSolid(x, top), $"seed {seed} room {room.Index} 천장 구멍 x={x}");
                        Assert.AreEqual(!down, chunk.IsSolid(x, 0), $"seed {seed} room {room.Index} 바닥 구멍 x={x}");
                    }
                }
            }
        }

        // 검증기: 한쪽 발판은 위에서 뚫고 내려갈 수 없다. 벽에서 벽까지 꽉 찬 발판 위에 서면 바닥으로 못 내려간다.
        [Test]
        public void ValidatorDoesNotDropThroughPlatforms()
        {
            var chunk = new ChunkData();
            for (int x = 0; x < ChunkData.Width; x++) chunk.FillColumn(x, 5);
            chunk.FillColumn(0, ChunkData.Height);
            chunk.FillColumn(ChunkData.Width - 1, ChunkData.Height);
            chunk.SetRow(1, ChunkData.Width - 2, 9, StageTile.Platform);

            var onPlatform = new System.Collections.Generic.List<(int x, int y)> { (5, 10) };
            var floor = new System.Collections.Generic.List<(int x, int y)> { (5, 5) };
            Assert.IsFalse(StageValidator.ValidateRoom(chunk, onPlatform, floor, out _), "발판 위에서 바닥으로 내려가면 안 됨");
            Assert.IsTrue(StageValidator.ValidateRoom(chunk, floor, floor, out string reason), reason);
        }

        // 세로 방 적은 바닥이나 선반 위에 서고, 구멍 위에 서지 않는다.
        [Test]
        public void ShaftEnemiesStandOnFloor()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var chunk = RoomShaper.Build(seed, room).Chunk;
                    foreach (var feature in chunk.Features)
                    {
                        if (feature.Kind != FeatureKind.Enemy) continue;
                        var below = chunk.Get(feature.X, feature.Y - 1);
                        Assert.IsTrue(below == StageTile.Solid || below == StageTile.Platform, $"seed {seed} room {room.Index} 적 ({feature.X},{feature.Y})");
                        Assert.IsFalse(chunk.IsSolid(feature.X, feature.Y) || chunk.IsSolid(feature.X, feature.Y + 1));
                        Assert.IsFalse(feature.X >= RoomShaper.HoleX0 - 1 && feature.X <= RoomShaper.HoleX1 + 1);
                    }
                }
            }
        }

        [Test]
        public void MidBossRoomHasTwinKnightsOnArenaFloor()
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                int midBossRooms = 0;
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    var chunk = RoomShaper.Build(seed, room).Chunk;
                    var knights = new System.Collections.Generic.List<ChunkFeature>();
                    ChunkFeature? arena = null;
                    foreach (var f in chunk.Features)
                    {
                        if (f.Kind == FeatureKind.Arena) arena = f;
                        if (f.Kind == FeatureKind.Enemy && (f.Enemy == StageEnemy.SlimeKnightFire || f.Enemy == StageEnemy.SlimeKnightWater))
                            knights.Add(f);
                    }

                    if (room.Kind != RoomKind.MidBoss)
                    {
                        Assert.AreEqual(0, knights.Count, $"seed {seed} room {room.Index}: 중간보스 방이 아닌데 기사가 있음");
                        continue;
                    }

                    midBossRooms++;
                    Assert.IsTrue(arena.HasValue, $"seed {seed}: 중간보스 방에 전투 구역이 없음");
                    int deferred = 0;
                    foreach (var f in chunk.Features) if (f.Kind == FeatureKind.Enemy && f.Deferred) deferred++;
                    Assert.AreEqual(2, deferred, $"seed {seed}: 중간보스 방의 구역 적은 기사 둘뿐이어야 함");
                    Assert.AreEqual(2, knights.Count);
                    Assert.AreNotEqual(knights[0].Enemy, knights[1].Enemy, "불 기사 하나, 물 기사 하나");
                    Assert.AreNotEqual(knights[0].Id, knights[1].Id);
                    foreach (var k in knights)
                    {
                        Assert.IsTrue(k.Deferred);
                        Assert.IsTrue(k.X > arena.Value.X && k.X < arena.Value.X + arena.Value.Width, $"seed {seed}: 기사가 구역 밖 ({k.X})");
                        Assert.IsTrue(chunk.IsSolid(k.X, k.Y - 1) && !chunk.IsSolid(k.X, k.Y), $"seed {seed}: 기사가 바닥 위가 아님 ({k.X},{k.Y})");
                    }
                }

                Assert.AreEqual(1, midBossRooms, $"seed {seed}: 중간보스 방은 하나");
            }
        }

        // 세로 방 발판 모양 세 가지가 모두 나오고, 고른 모양 그대로 검증을 통과한다 (지그재그로 되돌아가는 일 없음).
        [Test]
        public void EveryShaftStyleAppearsAndPassesWithoutFallback()
        {
            var seen = new System.Collections.Generic.HashSet<RoomShaper.ShaftStyle>();
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var style = RoomShaper.PickShaftStyle(seed, room);
                    seen.Add(style);
                    var geometry = RoomShaper.BuildShaft(seed, room, style);
                    Assert.IsTrue(geometry.Valid, $"seed {seed} room {room.Index} {style}: {geometry.Reason}");
                }
            }

            Assert.AreEqual(3, seen.Count, "지그재그·양 갈래·선반이 모두 나와야 함");
        }
    
        // 세로 방 함정: 이동 방 바닥에만, 도착·출발 칸과 구멍 옆·맨 아래 발판 밑에서 떨어져 있고, 함정이 있어도 검증을 통과한다.
        [Test]
        public void ShaftTrapsStayOffArrivalsAndStairs()
        {
            int spikes = 0, jets = 0, leftSpikes = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var geometry = RoomShaper.Build(seed, room);
                    Assert.IsTrue(geometry.Valid, $"seed {seed} room {room.Index}: {geometry.Reason}");
                    var chunk = geometry.Chunk;
                    foreach (var f in chunk.Features)
                    {
                        if (f.Kind != FeatureKind.Spikes && f.Kind != FeatureKind.FlameJet) continue;
                        Assert.AreEqual(RoomKind.Travel, room.Kind, $"seed {seed} room {room.Index} 이동 방에만 함정");
                        Assert.AreEqual(RoomShaper.ShaftGround, f.Y, "함정은 바닥에만");
                        if (f.Kind == FeatureKind.Spikes) spikes++; else jets++;
                        if (f.Kind == FeatureKind.Spikes && f.X < RoomShaper.HoleX0) leftSpikes++;
                        for (int x = f.X; x < f.X + f.Width; x++)
                        {
                            Assert.IsTrue(x >= 4 && x <= ChunkData.Width - 5, $"seed {seed} room {room.Index} 좌우 문 앞 {x}");
                            Assert.IsFalse(x >= RoomShaper.HoleX0 - 3 && x <= RoomShaper.HoleX1 + 3, $"seed {seed} room {room.Index} 구멍 옆 {x}");
                            foreach (var spot in geometry.Arrivals.Values)
                                Assert.IsFalse(spot.Item2 == f.Y && System.Math.Abs(spot.Item1 - x) <= 2, $"seed {seed} room {room.Index} 도착 칸 옆 {x}");
                            foreach (var spot in geometry.Exits.Values)
                                Assert.IsFalse(spot.Item2 == f.Y && System.Math.Abs(spot.Item1 - x) <= 2, $"seed {seed} room {room.Index} 출발 칸 옆 {x}");
                            foreach (var other in chunk.Features)
                                if ((other.Kind == FeatureKind.Enemy || other.Kind == FeatureKind.Checkpoint) && other.Y == f.Y)
                                    Assert.Greater(System.Math.Abs(other.X - x), 2, $"seed {seed} room {room.Index} {other.Kind} 옆 {x}");
                            for (int y = f.Y + 1; y <= f.Y + 3; y++)
                                Assert.AreNotEqual(StageTile.Platform, chunk.Get(x, y), $"seed {seed} room {room.Index} 맨 아래 발판 밑 {x}");
                        }
                    }
                }
            }

            Assert.Greater(leftSpikes, 0, "가시가 왼쪽(구멍 왼쪽)에도 나와야 함");
            Assert.Greater(spikes, 0, "가시가 한 번은 나와야 함");
            Assert.Greater(jets, 0, "불기둥이 한 번은 나와야 함");
        }
}
}
