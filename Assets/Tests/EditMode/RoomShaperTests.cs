using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 방 지형(RoomShaper): 어느 문으로 들어와도 모든 문으로 나갈 수 있고, 문 자리가 실제로 뚫려 있는지.
    public class RoomShaperTests
    {
        private const float TargetSeconds = 15f * 60f; // 스테이지 1 목표 15분 (2026-11-10 사람 결정)
        private const int SeedCount = 60; // 15분이라 방이 반으로 줄어 시드를 두 배로
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

        // NPC 마을 방: 적·함정·전투 구역·봉인문 등 기능이 하나도 없고, 건물 두 채가 서는 자리(방 가로 30%·70% ±2칸)가
        // 들어오는 높이(EntryHeight)와 같은 평지 (ChunkStreamer.LoadRoomExtras가 그 높이에 건물을 세운다).
        [Test]
        public void VillageRoomIsFlatAndEmpty()
        {
            int villages = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                var layout = RoomLayout.Generate(seed, TargetSeconds);
                foreach (var room in layout.Rooms)
                {
                    if (room.Kind != RoomKind.Village) continue;
                    villages++;
                    var geometry = RoomShaper.Build(seed, room);
                    var chunk = geometry.Chunk;
                    Assert.IsTrue(geometry.Valid, $"seed {seed}: {geometry.Reason}");
                    Assert.AreEqual(0, chunk.Features.Count, $"seed {seed}: 마을에 기능 {chunk.Features.Count}개");
                    foreach (float at in new[] { 0.3f, 0.7f })
                    {
                        int center = (int)(chunk.Cols * at);
                        for (int x = center - 2; x <= center + 2; x++)
                            Assert.AreEqual(chunk.EntryHeight, chunk.SurfaceAt(x), $"seed {seed} x {x}: 건물 자리가 평지가 아님");
                    }
                }
            }
            Assert.AreEqual(SeedCount, villages);
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
                    if ((room.Doors & DoorDir.Right) == 0) Assert.IsTrue(chunk.IsSolid(chunk.Cols - 1, top - 1), $"seed {seed} room {room.Index} 오른쪽 벽");
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

        // 넓은 방: 방 폭 = 구간 수 × 32, 모든 방이 검증 통과, 전투·보스 구역(과 중간보스 기사)은 들어오는 문 반대쪽 끝 구간,
        // 오른쪽 문 도착 칸은 방 오른쪽 끝 열.
        [Test]
        public void WideRoomsMatchColsAndKeepArenaAtFarEnd()
        {
            int wide = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    var geometry = RoomShaper.Build(seed, room);
                    string where = $"seed {seed} room {room.Index} {room.Kind}";
                    Assert.IsTrue(geometry.Valid, $"{where}: {geometry.Reason}");
                    var chunk = geometry.Chunk;
                    Assert.AreEqual(room.Cols, chunk.Cols, where);
                    if (room.Leads.Count > 0) wide++;
                    DoorDir right = DoorDir.Right;
                    if (geometry.Arrivals.ContainsKey(right)) Assert.AreEqual(chunk.Cols - 1, geometry.Arrivals[right].Item1, where);
                    if (room.Leads.Count == 0) continue;

                    bool mainRight = RoomShaper.MainOnRight(room);
                    int mainX0 = mainRight ? chunk.Cols - ChunkData.Width : 0;
                    foreach (var f in chunk.Features)
                    {
                        bool arenaThing = f.Kind == FeatureKind.Arena || f.Kind == FeatureKind.Exit || (f.Kind == FeatureKind.Enemy && f.Deferred);
                        if (!arenaThing) continue;
                        Assert.That(f.X, Is.InRange(mainX0, mainX0 + ChunkData.Width - 1), $"{where}: {f.Kind} 주 구간 밖 {f.X}");
                    }
                }
            }
            Assert.Greater(wide, SeedCount * 3, "넓은 방이 판마다 여럿");
        }

        // 넓은 방 이어 붙이기: 기능 X가 구간 시작만큼 밀리고 적 번호가 겹치지 않는다.
        [Test]
        public void StitchShiftsFeaturesAndRenumbersEnemies()
        {
            var a = new ChunkData { Index = 3, EntryHeight = 5, ExitHeight = 6 };
            a.AddEnemy(StageEnemy.Slime, 10, 5);
            var b = new ChunkData { Index = 3, EntryHeight = 6, ExitHeight = 7, Kind = ChunkKind.Arena };
            b.AddEnemy(StageEnemy.Goblin, 4, 6);
            b.AddFeature(FeatureKind.Arena, 2, 6, 28);
            b.Tiles[1, 0] = StageTile.Solid;
            var wide = ChunkData.Stitch(new List<ChunkData> { a, b }, b);
            Assert.AreEqual(64, wide.Cols);
            Assert.AreEqual(ChunkKind.Arena, wide.Kind);
            Assert.AreEqual(5, wide.EntryHeight);
            Assert.AreEqual(7, wide.ExitHeight);
            Assert.AreEqual(StageTile.Solid, wide.Get(33, 0));
            var ids = new HashSet<int>();
            foreach (var f in wide.Features)
            {
                if (f.Kind == FeatureKind.Enemy) Assert.IsTrue(ids.Add(f.Id), "적 번호 겹침");
                if (f.Kind == FeatureKind.Arena) Assert.AreEqual(34, f.X);
                if (f.Kind == FeatureKind.Enemy && f.Enemy == StageEnemy.Goblin) Assert.AreEqual(36, f.X);
            }
            Assert.AreEqual(2, ids.Count);
            Assert.AreEqual(StageTile.Empty, wide.Get(64, 0), "범위 밖은 빈칸");
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

                    Assert.AreEqual(room.Kind == RoomKind.MidBoss, RoomShaper.IsMidBossArena(chunk),
                        $"seed {seed} room {room.Index}: 중간보스 보상 판정(IsMidBossArena)이 방 종류와 다름");
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

            Assert.AreEqual(6, seen.Count, "지그재그·양 갈래·선반·무너지는 계단·깜빡 계단·버섯 탑이 모두 나와야 함");
        }

        // 선반 세로 방: 이동 방이고 난이도 문턱 이상이면 높은 선반 위에 동전 무더기 하나 (발판 위 빈 칸). 다른 모양에는 없다.
        [Test]
        public void ShelfShaftsPutCoinPileOnHighShelf()
        {
            int piles = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var style = RoomShaper.PickShaftStyle(seed, room);
                    var chunk = RoomShaper.BuildShaft(seed, room, style).Chunk;
                    var found = chunk.Features.FindAll(f => f.Kind == FeatureKind.CoinPile && f.Y != RoomShaper.WindLedgeRow + 1
                        && f.Y != RoomShaper.ShaftGround + RoomShaper.MushroomShelfRise
                        && style != RoomShaper.ShaftStyle.MushroomTower); // 바람 지름길·버섯 선반·버섯 탑 동전은 따로
                    bool expected = style == RoomShaper.ShaftStyle.Shelves && room.Kind == RoomKind.Travel
                        && room.Difficulty >= RoomShaper.ShelfCoinMinDifficulty;
                    string tag = $"seed {seed} room {room.Index} {style}";
                    Assert.AreEqual(expected ? 1 : 0, found.Count, tag);
                    foreach (var c in found)
                    {
                        Assert.AreEqual(RoomShaper.ShelfRowHigh + 1, c.Y, tag);
                        Assert.AreEqual(StageTile.Platform, chunk.Get(c.X, c.Y - 1), tag + ": 선반 위");
                        Assert.IsFalse(chunk.IsSolid(c.X, c.Y), tag);
                        piles++;
                    }
                }
            }

            Assert.Greater(piles, 0, "선반 동전이 한 번도 안 나옴");
        }

        // 바람 지름길: 위 문이 있는 이동 방(계단 모양)에서 문이 없는 벽 쪽에만. 환풍구는 바닥 위, 기둥 안은 바닥부터 꼭대기까지 비어 있고,
        // 기둥 꼭대기가 선반 서는 칸보다 높고, 선반 위에 동전, 선반은 계단 판에서 5칸 떨어져 있고, 환풍구 위에 함정이 없다.
        [Test]
        public void WindShortcutsLiftToCoinLedgeAwayFromDoors()
        {
            int vents = 0;
            int g = RoomShaper.ShaftGround;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var style = RoomShaper.PickShaftStyle(seed, room);
                    var geometry = RoomShaper.Build(seed, room);
                    var chunk = geometry.Chunk;
                    var found = chunk.Features.FindAll(f => f.Kind == FeatureKind.WindVent);
                    bool expected = RoomShaper.WantsWindShortcut(room) && style != RoomShaper.ShaftStyle.TwinStairs && style != RoomShaper.ShaftStyle.Shelves
                        && style != RoomShaper.ShaftStyle.MushroomTower;
                    string tag = $"seed {seed} room {room.Index} {style}";
                    Assert.AreEqual(expected ? 1 : 0, found.Count, tag);
                    foreach (var v in found)
                    {
                        bool left = v.X < ChunkData.Width / 2;
                        Assert.AreEqual(DoorDir.None, room.Doors & (left ? DoorDir.Left : DoorDir.Right), tag + ": 문 쪽 벽");
                        Assert.AreEqual(RoomShaper.WindVentWidth, v.Width, tag);
                        Assert.AreEqual(g, v.Y, tag);
                        int top = v.Y + v.Direction;
                        int stand = RoomShaper.WindLedgeRow + 1;
                        Assert.Greater(top, stand + 1, tag + ": 기둥 꼭대기가 선반보다 높아야");
                        Assert.Less(top, ChunkData.Height - 1, tag + ": 천장 아래");
                        for (int x = v.X; x < v.X + v.Width; x++)
                        {
                            Assert.IsTrue(chunk.IsSolid(x, g - 1), tag + $": 환풍구 바닥 ({x})");
                            for (int y = g; y < top; y++) Assert.AreEqual(StageTile.Empty, chunk.Get(x, y), tag + $": 기둥 안 ({x},{y})");
                        }

                        int ledgeX0 = left ? v.X - RoomShaper.WindLedgeWidth : v.X + v.Width;
                        for (int x = ledgeX0; x < ledgeX0 + RoomShaper.WindLedgeWidth; x++)
                        {
                            Assert.AreEqual(StageTile.Platform, chunk.Get(x, RoomShaper.WindLedgeRow), tag + $": 선반 ({x})");
                            Assert.IsFalse(chunk.IsSolid(x, stand) || chunk.IsSolid(x, stand + 1), tag + $": 선반 위 빈 칸 ({x})");
                        }
                        Assert.IsTrue(chunk.Features.Exists(f => f.Kind == FeatureKind.CoinPile && f.Y == stand
                            && f.X >= ledgeX0 && f.X < ledgeX0 + RoomShaper.WindLedgeWidth), tag + ": 선반 위 동전");

                        // 계단 판(타일·무너지는·깜빡 발판)과 가로로 5칸 이상 떨어짐 (걷기 점프로 닿지 않게)
                        int near = left ? ledgeX0 + RoomShaper.WindLedgeWidth - 1 : ledgeX0;
                        for (int y = g + 1; y < ChunkData.Height - 1; y++)
                            for (int x = 0; x < ChunkData.Width; x++)
                            {
                                if (chunk.Get(x, y) != StageTile.Platform || (y == RoomShaper.WindLedgeRow && x >= ledgeX0 && x < ledgeX0 + RoomShaper.WindLedgeWidth)) continue;
                                Assert.Greater(System.Math.Abs(x - near), 5, tag + $": 계단 판 ({x},{y})이 선반과 가까움");
                            }
                        foreach (var f in chunk.Features)
                        {
                            if (f.Kind == FeatureKind.Crumble || f.Kind == FeatureKind.BlinkPlatform)
                                Assert.Greater(System.Math.Min(System.Math.Abs(f.X - near), System.Math.Abs(f.X + f.Width - 1 - near)), 5, tag + ": 흔들 발판이 선반과 가까움");
                            if (f.Kind == FeatureKind.Spikes || f.Kind == FeatureKind.FlameJet)
                                Assert.IsTrue(f.X + System.Math.Max(1, f.Width) - 1 < v.X || f.X > v.X + v.Width - 1, tag + ": 환풍구 위 함정");
                        }
                        vents++;
                    }
                }
            }

            Assert.Greater(vents, 5, "바람 지름길이 거의 안 나옴");
        }

        // 무너지는/깜빡 계단: 난이도 기준 이상에서만 나오고, 맨 위 판·맨 아래 판은 타일 발판 그대로,
        // 그 사이 판은 타일 없이 무너지는/깜빡 발판 기능으로 바뀐다. 깜빡 계단 박자는 아래 판부터 0, 1, 2….
        [Test]
        public void UnsteadyStairsKeepTopAndBottomPlanksFixed()
        {
            int crumbleRooms = 0, blinkRooms = 0;
            int g = RoomShaper.ShaftGround;
            int lowest = RoomShaper.LowestStairRow(g);
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var style = RoomShaper.PickShaftStyle(seed, room);
                    if (style != RoomShaper.ShaftStyle.CrumbleStairs && style != RoomShaper.ShaftStyle.BlinkStairs) continue;
                    bool blink = style == RoomShaper.ShaftStyle.BlinkStairs;
                    string where = $"seed {seed} room {room.Index} {style}";
                    Assert.GreaterOrEqual(room.Difficulty, blink ? RoomShaper.BlinkStairsMinDifficulty : RoomShaper.CrumbleStairsMinDifficulty, where);
                    if (blink) blinkRooms++; else crumbleRooms++;

                    var chunk = RoomShaper.Build(seed, room).Chunk;
                    Assert.IsTrue(HasTilePlank(chunk, RoomShaper.TopPlatformRow), where + " 맨 위 판");
                    Assert.IsTrue(HasTilePlank(chunk, lowest), where + " 맨 아래 판");
                    var kind = blink ? FeatureKind.BlinkPlatform : FeatureKind.Crumble;
                    int expectedBeat = 0, planks = 0;
                    for (int row = lowest + 2; row < RoomShaper.TopPlatformRow; row += 2)
                    {
                        Assert.IsFalse(HasTilePlank(chunk, row), $"{where} {row}행은 타일이 아니어야 함");
                        int found = 0;
                        foreach (var f in chunk.Features)
                        {
                            if (f.Kind != kind || f.Y != row) continue;
                            found++;
                            if (blink) Assert.AreEqual(expectedBeat, f.Direction, $"{where} {row}행 박자");
                        }

                        Assert.AreEqual(1, found, $"{where} {row}행 발판 기능");
                        expectedBeat++;
                        planks++;
                    }

                    Assert.Greater(planks, 3, where);
                }
            }

            Assert.Greater(crumbleRooms, 0, "무너지는 계단이 한 번은 나와야 함");
            Assert.Greater(blinkRooms, 0, "깜빡 계단이 한 번은 나와야 함");
        }

        private static bool HasTilePlank(ChunkData chunk, int row)
        {
            for (int x = 0; x < ChunkData.Width; x++) if (chunk.Tiles[x, row] == StageTile.Platform) return true;
            return false;
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
                                if (other.Kind == FeatureKind.Enemy && other.Y == f.Y)
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

        // 순간이동 지름길 선반: 위 문 없는 이동 세로 방에만, 바닥보다 5칸 위(점프로는 안 닿고 점프 + 순간이동 4칸으로 닿음),
        // 걷기·점프만으로는 갈 수 없는 칸이며(필수 경로 아님) 선반 위에 회복 구슬이 있다.
        [Test]
        public void TeleportShelvesAreOptionalAndNeedTeleport()
        {
            int shelves = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    var geometry = RoomShaper.Build(seed, room);
                    foreach (var f in geometry.Chunk.Features)
                    {
                        if (f.Kind != FeatureKind.Reward) continue;
                        shelves++;
                        string where = $"seed {seed} room {room.Index}";
                        Assert.AreEqual(RoomKind.Travel, room.Kind, where);
                        Assert.AreEqual(DoorDir.None, room.Doors & DoorDir.Up, where);
                        Assert.AreEqual(StageTile.Platform, geometry.Chunk.Tiles[f.X, f.Y - 1], where);
                        Assert.AreEqual(RoomShaper.ShaftGround + RoomShaper.TeleportShelfRise, f.Y, where);
                        Assert.Greater(RoomShaper.TeleportShelfRise, 2, "점프만으로 닿으면 지름길이 아니다");
                        Assert.LessOrEqual(RoomShaper.TeleportShelfRise, 6, "점프 2.5 + 순간이동 4 안이어야 한다");

                        var arrivals = new List<(int x, int y)>(geometry.Arrivals.Values);
                        string reason;
                        Assert.IsFalse(StageValidator.ValidateRoom(geometry.Chunk, arrivals, new List<(int x, int y)> { (f.X, f.Y) }, out reason),
                            $"{where}: 선반이 걷기·점프로 닿는 필수 경로에 있다");
                        StringAssert.Contains("가는 길이 없음", reason, $"{where}: 선반 칸에 설 수는 있어야 한다");
                    }
                }
            }

            Assert.Greater(shelves, 0, "지름길 선반이 한 번도 안 나옴");
        }
    
        // 버섯 굴 선반: 위 문 없는 이동 세로 방에서 순간이동 선반이 없을 때만 (둘 중 하나). 버섯은 바닥 위, 그 위 바닥 + 6에
        // 버섯 열 ±2 발판 선반과 동전, 버섯 열과 선반 사이(몸이 오르는 길)는 비어 있고, 버섯 근처 바닥에 함정이 없으며,
        // 선반 칸은 걷기·점프로 닿지 않는 덤 길이다.
        [Test]
        public void MushroomShelvesFillCavesWithoutTeleportShelf()
        {
            int shelves = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var geometry = RoomShaper.Build(seed, room);
                    var chunk = geometry.Chunk;
                    string where = $"seed {seed} room {room.Index}";
                    var mushrooms = chunk.Features.FindAll(f => f.Kind == FeatureKind.BounceMushroom && f.Direction == 0); // 버섯 탑의 길 버섯은 따로
                    bool teleport = chunk.Features.Exists(f => f.Kind == FeatureKind.Reward);
                    bool expected = room.Kind == RoomKind.Travel && (room.Doors & DoorDir.Up) == 0 && !teleport
                        && room.Difficulty >= ChunkGenerator.BounceMinDifficulty;
                    Assert.AreEqual(expected ? 1 : 0, mushrooms.Count, where);
                    if (!expected) continue;
                    shelves++;

                    var m = mushrooms[0];
                    int g = RoomShaper.ShaftGround;
                    int stand = g + RoomShaper.MushroomShelfRise;
                    Assert.AreEqual(g, m.Y, where);
                    Assert.IsTrue(chunk.IsSolid(m.X, g - 1), where + ": 버섯 밑 바닥");
                    for (int x = m.X - RoomShaper.MushroomShelfHalf; x <= m.X + RoomShaper.MushroomShelfHalf; x++)
                        Assert.AreEqual(StageTile.Platform, chunk.Get(x, stand - 1), where + $": 선반 ({x})");
                    for (int y = g; y < stand - 1; y++) Assert.AreEqual(StageTile.Empty, chunk.Get(m.X, y), where + $": 버섯 위 길 ({y})");
                    for (int y = stand; y <= stand + 1; y++) Assert.IsFalse(chunk.IsSolid(m.X, y), where + ": 선반 위 몸 자리");
                    Assert.Greater(BounceMushroom_LaunchReach, stand - g, "튕김 높이가 선반에 닿아야 한다");

                    var coin = chunk.Features.Find(f => f.Kind == FeatureKind.CoinPile && f.Y == stand);
                    Assert.IsNotNull(coin, where + ": 선반 위 동전");
                    Assert.LessOrEqual(System.Math.Abs(coin.X - m.X), RoomShaper.MushroomShelfHalf, where);
                    Assert.AreNotEqual(m.X, coin.X, where + ": 버섯 바로 위는 내려앉을 자리");

                    Assert.IsFalse(chunk.Features.Exists(f => f.Kind == FeatureKind.Enemy && f.Y == g
                        && System.Math.Abs(f.X - m.X) <= RoomShaper.MushroomShelfHalf), where + ": 버섯 근처에 선 적");
                    foreach (var f in chunk.Features)
                        if ((f.Kind == FeatureKind.Spikes || f.Kind == FeatureKind.FlameJet) && f.Y == g)
                            Assert.IsTrue(f.X + System.Math.Max(1, f.Width) - 1 < m.X - 2 || f.X > m.X + 2, where + $": 버섯 옆 {f.Kind} {f.X}");

                    var arrivals = new List<(int x, int y)>(geometry.Arrivals.Values);
                    string reason;
                    Assert.IsFalse(StageValidator.ValidateRoom(chunk, arrivals, new List<(int x, int y)> { (m.X, stand) }, out reason),
                        $"{where}: 선반이 걷기·점프로 닿는 필수 경로에 있다");
                }
            }

            Assert.Greater(shelves, 0, "버섯 굴 선반이 한 번도 안 나옴");
        }

        // 버섯 탑: 지그재그 대신 고른 이동 방에만, 길 버섯 둘(Direction 1)이 각각 TowerStep칸 위 선반 아래에 있고,
        // 버섯 열 위가 선반까지 비어 있으며, 길 버섯 없이는 위 문에 못 오른다(버섯이 진짜 길이다). 함정·적은 버섯 칸에 없다.
        [Test]
        public void MushroomTowersClimbOnlyByMushrooms()
        {
            int towers = 0, sniperRooms = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!room.IsVertical) continue;
                    var style = RoomShaper.PickShaftStyle(seed, room);
                    var geometry = RoomShaper.BuildShaft(seed, room, style);
                    var chunk = geometry.Chunk;
                    string where = $"seed {seed} room {room.Index} {style}";
                    var path = chunk.Features.FindAll(f => f.Kind == FeatureKind.BounceMushroom && f.Direction == 1);
                    if (style != RoomShaper.ShaftStyle.MushroomTower)
                    {
                        Assert.AreEqual(0, path.Count, where);
                        continue;
                    }

                    towers++;
                    Assert.AreEqual(RoomKind.Travel, room.Kind, where);
                    Assert.GreaterOrEqual(room.Difficulty, RoomShaper.MushroomTowerMinDifficulty, where);
                    Assert.IsTrue(geometry.Valid, where + ": " + geometry.Reason);
                    Assert.AreEqual(2, path.Count, where);
                    Assert.Less(RoomShaper.TowerStep, BounceMushroom_LaunchReach, "튕김이 선반 위로 넉넉히 올라야 한다");
                    foreach (var m in path)
                    {
                        Assert.IsTrue(chunk.Get(m.X, m.Y - 1) != StageTile.Empty, where + ": 버섯 밑 발판");
                        int stand = m.Y + RoomShaper.TowerStep;
                        bool crumbleShelf = chunk.Features.Exists(f => f.Kind == FeatureKind.Crumble && f.Y == stand - 1 && m.X >= f.X && m.X < f.X + f.Width);
                        Assert.IsTrue(chunk.Get(m.X, stand - 1) == StageTile.Platform || crumbleShelf, where + ": 버섯 위 선반");
                        for (int y = m.Y; y < stand - 1; y++) Assert.IsFalse(chunk.IsSolid(m.X, y), where + $": 버섯 위 길 ({y})");
                        foreach (var f in chunk.Features)
                        {
                            if (f.Y != m.Y) continue;
                            if (f.Kind == FeatureKind.Enemy) Assert.Greater(System.Math.Abs(f.X - m.X), 2, where + ": 버섯 옆의 적");
                            if (f.Kind == FeatureKind.Spikes || f.Kind == FeatureKind.FlameJet)
                                Assert.IsTrue(f.X + System.Math.Max(1, f.Width) - 1 < m.X - 2 || f.X > m.X + 2, where + $": 버섯 옆 {f.Kind}");
                        }
                    }

                    // P2 벽 쪽 끝 동전 (난이도 문턱 이상)
                    int stand2 = RoomShaper.ShaftGround + 2 * RoomShaper.TowerStep;
                    var coins = chunk.Features.FindAll(f => f.Kind == FeatureKind.CoinPile);
                    Assert.AreEqual(room.Difficulty >= RoomShaper.ShelfCoinMinDifficulty ? 1 : 0, coins.Count, where);
                    foreach (var c in coins)
                    {
                        Assert.AreEqual(stand2, c.Y, where);
                        bool onCrumble = chunk.Features.Exists(f => f.Kind == FeatureKind.Crumble && f.Y == c.Y - 1 && c.X >= f.X && c.X < f.X + f.Width);
                        Assert.IsTrue(chunk.Get(c.X, c.Y - 1) == StageTile.Platform || onCrumble, where + ": P2 위 동전");
                    }

                    // 무너지는 탑: P2만 무너지는 발판 (타일은 비고, 같은 열·폭), 아니면 무너지는 발판 없음
                    var crumbles = chunk.Features.FindAll(f => f.Kind == FeatureKind.Crumble);
                    int p2a = System.Math.Min(RoomShaper.TowerCol(RoomShaper.MushroomTowerOnLeft(room), RoomShaper.TowerShelf2X0),
                        RoomShaper.TowerCol(RoomShaper.MushroomTowerOnLeft(room), RoomShaper.TowerShelf2X1));
                    if (RoomShaper.TowerCrumbles(room))
                    {
                        Assert.AreEqual(1, crumbles.Count, where);
                        Assert.AreEqual(stand2 - 1, crumbles[0].Y, where);
                        Assert.AreEqual(p2a, crumbles[0].X, where);
                        Assert.AreEqual(RoomShaper.TowerShelf2X1 - RoomShaper.TowerShelf2X0 + 1, crumbles[0].Width, where);
                        for (int x = p2a; x < p2a + crumbles[0].Width; x++)
                            Assert.AreEqual(StageTile.Empty, chunk.Get(x, stand2 - 1), where + ": 무너지는 P2 자리 타일");
                    }
                    else Assert.AreEqual(0, crumbles.Count, where);

                    // 저격 선반: 문턱 이상이면 탑 반대쪽 P1 높이 발판 위 궁수 하나, 오르는 버섯 열과 떨어져 있다
                    int stand1 = RoomShaper.ShaftGround + RoomShaper.TowerStep;
                    var snipers = chunk.Features.FindAll(f => f.Kind == FeatureKind.Enemy && f.Y == stand1);
                    if (RoomShaper.TowerHasSniper(room))
                    {
                        sniperRooms++;
                        Assert.AreEqual(1, snipers.Count, where);
                        Assert.AreEqual(StageEnemy.GoblinArcher, snipers[0].Enemy, where);
                        Assert.AreEqual(StageTile.Platform, chunk.Get(snipers[0].X, stand1 - 1), where + ": 궁수 발판");
                        foreach (var m in path) Assert.Greater(System.Math.Abs(snipers[0].X - m.X), 10, where + ": 궁수와 버섯 거리");
                    }
                    else Assert.AreEqual(0, snipers.Count, where);

                    // P2 → 맨 위 판 사이 빈 칸은 좌우 모두 1칸
                    int p2Edge = RoomShaper.MushroomTowerOnLeft(room) ? RoomShaper.TowerShelf2X1 : RoomShaper.TowerCol(false, RoomShaper.TowerShelf2X1);
                    int topEdge = RoomShaper.MushroomTowerOnLeft(room) ? RoomShaper.HoleX0 - 1 : RoomShaper.HoleX1 + 1;
                    Assert.AreEqual(2, System.Math.Abs(topEdge - p2Edge), where + ": 마지막 뛰기 간격");

                    // 길 버섯을 덤 버섯으로 바꾸면 바닥에서 위 문으로 못 간다
                    var copy = new ChunkData { Index = chunk.Index, Kind = chunk.Kind, Difficulty = chunk.Difficulty };
                    for (int x = 0; x < ChunkData.Width; x++)
                        for (int y = 0; y < ChunkData.Height; y++) copy.Tiles[x, y] = chunk.Tiles[x, y];
                    var top = geometry.Exits[DoorDir.Up];
                    string reason;
                    Assert.IsFalse(StageValidator.ValidateRoom(copy, new List<(int x, int y)> { geometry.Floor.Value },
                        new List<(int x, int y)> { top }, out reason), where + ": 버섯 없이도 오를 수 있다");
                }
            }

            Assert.Greater(towers, 0, "버섯 탑이 한 번도 안 나옴");
            Assert.Greater(sniperRooms, 0, "저격 선반이 한 번도 안 나옴");
        }

        // BounceMushroom.LaunchHeight(7.5) + 갓 높이(0.95). 테스트 어셈블리가 MonoBehaviour 쪽을 참조하지 않아 값을 옮겨 둔다
        private const float BounceMushroom_LaunchReach = 7.5f + 0.95f;

        // 협동 봉인문: 진행도 기준을 넘은 보물 곁방에만 하나, 문·도착 칸과 상자 사이를 바닥부터 천장 아래까지 비운 열에.
        // 첫 속성(A 방어막)과 둘째 속성(B 마법)은 서로 다른 투사체 속성이고, 봉인문 열을 메우면 상자 칸에 못 간다(우회 불가).
        [Test]
        public void PairGatesGuardLateTreasureRooms()
        {
            int sideGates = 0, shaftGates = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    var geometry = RoomShaper.Build(seed, room);
                    var chunk = geometry.Chunk;
                    var gates = chunk.Features.FindAll(f => f.Kind == FeatureKind.ElementGate && f.ShieldFirst);
                    string where = $"seed {seed} room {room.Index} ({room.Kind}, {room.Doors}, {room.Difficulty:0.00})";
                    bool expected = room.Kind == RoomKind.Treasure && room.Difficulty >= RoomShaper.PairGateMinDifficulty;
                    Assert.AreEqual(expected ? 1 : 0, gates.Count, where);
                    Assert.AreEqual(expected, RoomShaper.HasPairGate(room), where);
                    if (!expected) continue;

                    var gate = gates[0];
                    Assert.AreEqual(1, chunk.Features.FindAll(f => f.Kind == FeatureKind.ElementGate).Count, $"{where}: 봉인문은 방마다 하나 (열린 기록이 방 번호 기준)");
                    Assert.IsTrue(gate.HasSecondElement, where);
                    Assert.That(gate.ElementIndex, Is.InRange(0, ChunkGenerator.GateElementCount - 1), where);
                    Assert.That(gate.SecondElementIndex, Is.InRange(0, ChunkGenerator.GateElementCount - 1), where);
                    Assert.AreNotEqual(gate.ElementIndex, gate.SecondElementIndex, where);
                    Assert.AreEqual(RoomShaper.PairGateX(room), gate.X, where);

                    (int x, int y) chest;
                    if (room.IsVertical)
                    {
                        shaftGates++;
                        chest = (RoomShaper.ShaftPairChestX, RoomShaper.ShaftGround);
                        Assert.Less(gate.X, RoomShaper.HoleX0 - 3, $"{where}: 위·아래 문과 도착 칸은 봉인문 오른쪽");
                    }
                    else
                    {
                        sideGates++;
                        chest = (ChunkData.Width / 2, chunk.EntryHeight);
                        Assert.That(room.Doors, Is.EqualTo(DoorDir.Right).Or.EqualTo(DoorDir.Left), $"{where}: 좌우 곁방은 문 하나");
                        Assert.That(gate.X, Is.InRange(2, ChunkData.Width - 3), $"{where}: 문 칸과 겹침");
                    }
                    if (room.Doors == DoorDir.Left) Assert.Greater(chest.x, gate.X, $"{where}: 상자는 봉인문 안쪽 (왼쪽 문)");
                    else Assert.Less(chest.x, gate.X, $"{where}: 상자는 봉인문 안쪽");
                    Assert.AreEqual(chest.y, gate.Y, where);

                    for (int y = gate.Y; y < ChunkData.Height - 1; y++)
                        Assert.AreEqual(StageTile.Empty, chunk.Tiles[gate.X, y], $"{where}: 봉인문 열 ({gate.X},{y})");
                    foreach (int x in new[] { gate.X - 1, gate.X + 1, chest.x })
                        Assert.IsTrue(chunk.IsSolid(x, gate.Y - 1) && !chunk.IsSolid(x, gate.Y) && !chunk.IsSolid(x, gate.Y + 1), $"{where}: ({x}) 설 자리");

                    var arrivals = new List<(int x, int y)>(geometry.Arrivals.Values);
                    string reason;
                    Assert.IsTrue(StageValidator.ValidateRoom(chunk, arrivals, new List<(int x, int y)> { chest }, out reason), $"{where}: 상자 칸에 못 감 ({reason})");
                    // 열을 메우면 상자 칸으로 못 간다 = 봉인문을 돌아갈 길이 없다.
                    for (int y = gate.Y; y < ChunkData.Height; y++) chunk.Tiles[gate.X, y] = StageTile.Solid;
                    Assert.IsFalse(StageValidator.ValidateRoom(chunk, arrivals, new List<(int x, int y)> { chest }, out _), $"{where}: 봉인문을 우회할 수 있음");
                }
            }

            Assert.Greater(shaftGates, 0, "세로 곁방 협동 봉인문이 한 번도 안 나옴");
            Assert.Greater(sideGates + shaftGates, 5, "협동 봉인문이 너무 드묾");
        }

        // 봉인문 벽은 방 안 좌표로 잰 빈 열만 막는다: 바닥 칸부터 첫 막힌 칸 아래까지, 지형 칸과 겹치지 않는다.
        // (2026-11-12 플레이테스트 버그: 높이를 월드 y로 재서 아래 줄 방의 문이 위 방 지형까지 뚫고 올라갔다)
        [Test]
        public void GatesNeverOverlapTerrain()
        {
            int gates = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    var chunk = RoomShaper.Build(seed, room).Chunk;
                    foreach (var gate in chunk.Features.FindAll(f => f.Kind == FeatureKind.ElementGate))
                    {
                        gates++;
                        string where = $"seed {seed} room {room.Index} ({room.Kind}, Y {room.Y}) gate ({gate.X},{gate.Y})";
                        int height = chunk.GateHeight(gate.X, gate.Y);
                        Assert.GreaterOrEqual(height, 4, $"{where}: 문양 두 개가 들어갈 높이");
                        Assert.IsTrue(chunk.IsSolid(gate.X, gate.Y - 1), $"{where}: 바닥 위에 선다");
                        for (int y = gate.Y; y < gate.Y + height && y < ChunkData.Height; y++)
                            Assert.IsFalse(chunk.IsSolid(gate.X, y), $"{where}: ({gate.X},{y}) 지형과 겹침");
                        if (gate.Y + height < ChunkData.Height)
                            Assert.IsTrue(chunk.IsSolid(gate.X, gate.Y + height), $"{where}: 천장까지 막아야 넘지 못함");
                        else
                            Assert.AreEqual(ChunkData.Height + ChunkData.GateOverTop, gate.Y + height, where);
                    }
                }
            }
            Assert.Greater(gates, 5, "봉인문이 너무 드묾");
        }
    }
}
