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

        // 세로 방 적은 구멍 위에 서지 않는다.
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
                        Assert.IsTrue(chunk.IsSolid(feature.X, feature.Y - 1), $"seed {seed} room {room.Index} 적 ({feature.X},{feature.Y})");
                        Assert.IsFalse(feature.X >= RoomShaper.HoleX0 - 1 && feature.X <= RoomShaper.HoleX1 + 1);
                    }
                }
            }
        }
    }
}
