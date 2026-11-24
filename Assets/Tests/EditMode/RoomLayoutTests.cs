using System;
using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 방 격자형 배치(RoomLayout) 규칙. Docs/LevelDesign.md의 "생성 규칙"을 고정한다.
    public class RoomLayoutTests
    {
        private const float TargetSeconds = 20f * 60f; // 스테이지 목표 15~30분의 기본값 20분 (2026-11-12 사람 결정 15~30분)
        private const int SeedCount = 60;

        private static IEnumerable<RoomLayoutData> Layouts()
        {
            for (int seed = 0; seed < SeedCount; seed++) yield return RoomLayout.Generate(seed, TargetSeconds);
        }

        [Test]
        public void SameSeedGivesSameLayout()
        {
            var a = RoomLayout.Generate(7, TargetSeconds);
            var b = RoomLayout.Generate(7, TargetSeconds);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
            {
                Assert.AreEqual(a.Rooms[i].X, b.Rooms[i].X);
                Assert.AreEqual(a.Rooms[i].Y, b.Rooms[i].Y);
                Assert.AreEqual(a.Rooms[i].Kind, b.Rooms[i].Kind);
                Assert.AreEqual(a.Rooms[i].Doors, b.Rooms[i].Doors);
            }
        }

        [Test]
        public void RoomsNeverShareACell()
        {
            foreach (var layout in Layouts())
            {
                var seen = new HashSet<(int, int)>();
                foreach (var room in layout.Rooms)
                    Assert.IsTrue(seen.Add((room.X, room.Y)), $"seed {layout.Seed}: ({room.X},{room.Y}) 겹침");
            }
        }

        [Test]
        public void EveryDoorHasAMatchingDoorOnTheOtherSide()
        {
            var dirs = new[] { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down };
            foreach (var layout in Layouts())
            {
                foreach (var room in layout.Rooms)
                {
                    foreach (var dir in dirs)
                    {
                        if ((room.Doors & dir) == 0) continue;
                        var other = layout.Neighbor(room, dir);
                        Assert.IsNotNull(other, $"seed {layout.Seed} room {room.Index}: {dir} 문 너머에 방이 없음");
                        Assert.AreNotEqual(DoorDir.None, other.Doors & RoomLayout.Opposite(dir),
                            $"seed {layout.Seed} room {room.Index}: {dir} 문 맞은편에 문이 없음");
                    }
                }
            }
        }

        [Test]
        public void AllRoomsReachableFromStart()
        {
            var dirs = new[] { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down };
            foreach (var layout in Layouts())
            {
                var start = layout.Rooms[0];
                Assert.AreEqual(RoomKind.Start, start.Kind);
                var visited = new HashSet<int> { start.Index };
                var queue = new Queue<RoomData>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var room = queue.Dequeue();
                    foreach (var dir in dirs)
                    {
                        if ((room.Doors & dir) == 0) continue;
                        var next = layout.Neighbor(room, dir);
                        if (next != null && visited.Add(next.Index)) queue.Enqueue(next);
                    }
                }

                Assert.AreEqual(layout.Rooms.Count, visited.Count, $"seed {layout.Seed}: 못 가는 방이 있음");
            }
        }

        [Test]
        public void GridIsFiveByFourWithFourteenToEighteenRooms()
        {
            foreach (var layout in Layouts())
            {
                Assert.That(layout.Rooms.Count, Is.InRange(RoomLayout.MinRooms, RoomLayout.MaxRooms), $"seed {layout.Seed}");
                foreach (var room in layout.Rooms)
                {
                    Assert.That(room.X, Is.InRange(0, RoomLayout.GridWidth - 1), $"seed {layout.Seed}");
                    Assert.That(room.Y, Is.InRange(0, RoomLayout.GridHeight - 1), $"seed {layout.Seed}");
                }
                Assert.AreEqual(0, layout.MinX);
                Assert.AreEqual(RoomLayout.GridWidth - 1, layout.MaxX);
                Assert.AreEqual(0, layout.MinY);
                Assert.AreEqual(RoomLayout.GridHeight - 1, layout.MaxY);
            }
        }

        [Test]
        public void StartIsInnerMiddleCellWithSideDoorsOnly()
        {
            foreach (var layout in Layouts())
            {
                var start = layout.Rooms[0];
                Assert.AreEqual(RoomLayout.StartX, start.X, $"seed {layout.Seed}");
                Assert.That(start.Y, Is.InRange(1, 2), $"seed {layout.Seed}");
                Assert.IsFalse(start.IsVertical, $"seed {layout.Seed}: 시작 방은 좌우 방");
                Assert.AreEqual(0, start.Distance);
            }
        }

        [Test]
        public void LayoutIsATreeAndDistancesMatchParents()
        {
            var dirs = new[] { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down };
            foreach (var layout in Layouts())
            {
                int doors = 0;
                foreach (var room in layout.Rooms)
                    foreach (var dir in dirs) if ((room.Doors & dir) != 0) doors++;
                Assert.AreEqual(2 * (layout.Rooms.Count - 1), doors, $"seed {layout.Seed}: 이음 수 = 방 수 - 1 (고리 없음)");
                foreach (var room in layout.Rooms)
                {
                    if (room.Index == 0) continue;
                    var up = layout.Rooms[room.TreeParentIndex];
                    Assert.AreEqual(up.Distance + 1, room.Distance, $"seed {layout.Seed} room {room.Index}");
                    Assert.AreEqual(up, layout.Neighbor(room, room.EntryDoor), $"seed {layout.Seed} room {room.Index}: 들어오는 문 너머가 부모");
                }
            }
        }

        // 2026-11-24 사람 결정: 주 경로·곁방 개념 없음. 보스 방 = 시작에서 가장 먼 방 (문 하나, 좌우 방).
        [Test]
        public void BossIsTheFarthestRoom()
        {
            foreach (var layout in Layouts())
            {
                var boss = layout.Find(RoomKind.Boss);
                Assert.IsNotNull(boss, $"seed {layout.Seed}");
                Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Kind == RoomKind.Boss).Count);
                Assert.AreEqual(ChunkKind.Boss, boss.Chunk);
                Assert.IsFalse(boss.IsVertical);
                Assert.AreEqual(1, boss.DoorCount, $"seed {layout.Seed}: 보스 방은 문 하나");
                Assert.AreEqual(boss.EntryDoor, boss.Doors);
                Assert.GreaterOrEqual(boss.Distance, RoomLayout.MinBossDistance);
                foreach (var room in layout.Rooms) Assert.LessOrEqual(room.Distance, boss.Distance, $"seed {layout.Seed}: 보스가 가장 멀다");
                Assert.AreEqual(1f, boss.Difficulty, 1e-6f);
            }
        }

        // 방 번호는 시작 방에서 가까운 순 (시작 방 0번).
        [Test]
        public void RoomsAreIndexedByDistance()
        {
            foreach (var layout in Layouts())
            {
                Assert.AreEqual(RoomKind.Start, layout.Rooms[0].Kind);
                for (int i = 0; i < layout.Rooms.Count; i++)
                {
                    Assert.AreEqual(i, layout.Rooms[i].Index);
                    if (i > 0) Assert.LessOrEqual(layout.Rooms[i - 1].Distance, layout.Rooms[i].Distance, $"seed {layout.Seed}");
                }
            }
        }

        [Test]
        public void DifficultyFollowsDistance()
        {
            foreach (var layout in Layouts())
            {
                float boss = layout.Find(RoomKind.Boss).Distance;
                foreach (var room in layout.Rooms)
                    Assert.AreEqual(room.Distance / boss, room.Difficulty, 1e-4f, $"seed {layout.Seed} room {room.Index}");
            }
        }

        // 넓은 방(앞 구간)으로 목표 15·20·30분 모두 예상 시간이 15~30분 안 (2026-11-12 "큰 방").
        [Test]
        public void EstimatedTimeWithinFifteenToThirtyMinutes()
        {
            foreach (float minutes in new[] { 15f, 20f, 30f })
            {
                float min = float.MaxValue, max = 0f;
                for (int seed = 0; seed < 200; seed++)
                {
                    float seconds = RoomLayout.EstimatedSeconds(RoomLayout.Generate(seed, minutes * 60f));
                    min = Math.Min(min, seconds);
                    max = Math.Max(max, seconds);
                }
                Console.WriteLine($"목표 {minutes}분 → 방 격자 예상 시간: {min / 60f:0.0}~{max / 60f:0.0}분");
                Assert.GreaterOrEqual(min, RoomLayout.MinSeconds, $"목표 {minutes}분");
                Assert.LessOrEqual(max, RoomLayout.MaxSeconds, $"목표 {minutes}분");
            }
        }

        // 넓은 방 규칙: 좌우 전투·정예·중간보스·보스 방은 앞 구간 2개, 퍼즐·시작·보물·샘·마을·세로 방은 한 화면.
        // 봉인문·움직이는 발판 앞 구간은 일반·정예 전투 방에만, 방마다 하나까지 (2026-11-14 기믹은 전투 방에 섞는다).
        [Test]
        public void LeadsFollowRoomKind()
        {
            foreach (var layout in Layouts())
                foreach (var room in layout.Rooms)
                {
                    Assert.AreEqual(RoomLayout.LeadCount(room), room.Leads.Count, $"seed {layout.Seed} room {room.Index}");
                    Assert.LessOrEqual(room.Cols, RoomLayout.CellCols);
                    if (room.IsVertical || room.Kind == RoomKind.Start || room.Kind == RoomKind.Treasure || room.Kind == RoomKind.Spring
                        || room.Kind == RoomKind.Village || room.Kind == RoomKind.Puzzle)
                        Assert.AreEqual(0, room.Leads.Count, $"seed {layout.Seed} room {room.Index} {room.Kind}");
                    int puzzleLeads = room.Leads.FindAll(l => l == ChunkKind.ElementGate || l == ChunkKind.MovingBridge).Count;
                    Assert.LessOrEqual(puzzleLeads, RoomLayout.AllowsGimmickLead(room) ? 1 : 0, $"seed {layout.Seed} room {room.Index} {room.Kind}");
                }
        }

        // 중간보스 방 = 보스 다음으로 먼 좌우 방. 보스 방으로 가는 길목(보스의 조상)은 아니다 (중간보스가 보스 관문이 되지 않게).
        [Test]
        public void MidBossIsTheNextFarthestRoomOffTheBossLine()
        {
            foreach (var layout in Layouts())
            {
                var mids = layout.Rooms.FindAll(r => r.Kind == RoomKind.MidBoss);
                Assert.AreEqual(1, mids.Count, $"seed {layout.Seed}");
                var mid = mids[0];
                var boss = layout.Find(RoomKind.Boss);
                Assert.IsFalse(mid.IsVertical);
                Assert.AreEqual(ChunkKind.EliteArena, mid.Chunk);
                Assert.GreaterOrEqual(mid.Distance, RoomLayout.MinMidBossDistance);
                var bossLine = new HashSet<RoomData>();
                for (var r = boss; r.TreeParentIndex >= 0;)
                {
                    r = layout.Rooms[r.TreeParentIndex];
                    bossLine.Add(r);
                }
                Assert.IsFalse(bossLine.Contains(mid), $"seed {layout.Seed}: 중간보스가 보스로 가는 길목");
                foreach (var r in layout.Rooms)
                {
                    if (r == boss || r == mid || r.Index == 0 || bossLine.Contains(r) || r.IsVertical) continue;
                    Assert.LessOrEqual(r.Distance, mid.Distance, $"seed {layout.Seed}: 중간보스보다 먼 좌우 방 {r.Index}");
                }
            }
        }

        [Test]
        public void OnlyCombatAndRewardRoomsUseVerticalDoors()
        {
            foreach (var layout in Layouts())
            {
                foreach (var room in layout.Rooms)
                {
                    if (!room.IsVertical) continue;
                    Assert.IsTrue(room.Kind == RoomKind.Combat || room.Kind == RoomKind.Treasure || room.Kind == RoomKind.Spring,
                        $"seed {layout.Seed} room {room.Index}: {room.Kind}에 위/아래 문");
                }
            }
        }

        [Test]
        public void RewardRoomCountAndKinds()
        {
            foreach (var layout in Layouts())
            {
                int treasure = 0, rewards = 0;
                foreach (var room in layout.Rooms)
                {
                    if (room.Kind != RoomKind.Treasure && room.Kind != RoomKind.Spring) continue;
                    rewards++;
                    if (room.Kind == RoomKind.Treasure) treasure++;
                    Assert.AreEqual(ChunkKind.Rest, room.Chunk);
                }

                Assert.That(rewards, Is.InRange(RoomLayout.MinRewardRooms, RoomLayout.MaxRewardRooms), $"seed {layout.Seed}");
                Assert.GreaterOrEqual(treasure, 1);
            }
        }

        // 주 경로·막다른 방 개념이 없으니 마을·퍼즐·보상 방도 문이 둘 이상일 수 있다 (2026-11-24). 여러 판 중 한 번은 나온다.
        // 이중 스위치 발판 퍼즐은 건너가는 퍼즐이라 늘 왼쪽·오른쪽 문이 다 있다. 마을·퍼즐은 좌우 방.
        [Test]
        public void SpecialRoomsAreNotTiedToDeadEnds()
        {
            bool village2 = false, puzzle1 = false, reward2 = false, village1 = false;
            for (int seed = 0; seed < 300; seed++)
            {
                var layout = RoomLayout.Generate(seed, TargetSeconds);
                foreach (var room in layout.Rooms)
                {
                    if (room.Kind == RoomKind.Village)
                    {
                        if (room.DoorCount >= 2) village2 = true;
                        else village1 = true;
                    }
                    if (room.Kind == RoomKind.Puzzle)
                    {
                        Assert.IsFalse(room.IsVertical, $"seed {seed}");
                        if (room.Chunk == ChunkKind.SwitchBridge)
                            Assert.AreEqual(DoorDir.Left | DoorDir.Right, room.Doors, $"seed {seed}: 이중 스위치 방은 양쪽 문");
                        else if (room.DoorCount == 1) puzzle1 = true;
                    }
                    if ((room.Kind == RoomKind.Treasure || room.Kind == RoomKind.Spring) && room.DoorCount >= 2) reward2 = true;
                }
            }

            Assert.IsTrue(village2, "문 둘 이상인 마을이 한 번은 나온다");
            Assert.IsTrue(village1, "문 하나인 마을도 나온다");
            Assert.IsTrue(puzzle1, "문 하나인 추리 퍼즐 방도 나온다");
            Assert.IsTrue(reward2, "문 둘 이상인 보상 방도 나온다");
        }

        // NPC 마을 (2026-11-12, 2026-11-14 23:30 1개 고정): 판마다(목표 15·20·30분) 하나, 좌우 방, 시작 방에서 2칸 이상,
        // 보스 방과 격자로 붙지 않음, 문이 잠기지 않고 한 화면 평지(휴식 청크).
        [Test]
        public void VillageIsOneSideRoom()
        {
            foreach (float minutes in new[] { 15f, 20f, 30f })
                for (int seed = -40; seed < 300; seed++)
                {
                    var layout = RoomLayout.Generate(seed, minutes * 60f);
                    RoomData boss = layout.Rooms.Find(r => r.Kind == RoomKind.Boss);
                    var villages = layout.Rooms.FindAll(r => r.Kind == RoomKind.Village);
                    string at = $"seed {seed} {minutes}분";
                    Assert.AreEqual(RoomLayout.Villages, villages.Count, at);
                    foreach (var village in villages)
                    {
                        Assert.IsFalse(village.IsVertical, at);
                        Assert.GreaterOrEqual(village.Distance, RoomLayout.VillageMinDistance, at);
                        Assert.Greater(Math.Abs(village.X - boss.X) + Math.Abs(village.Y - boss.Y), 1, $"{at}: 보스 방 옆");
                        Assert.IsFalse(village.LocksDoors, at);
                        Assert.AreEqual(ChunkKind.Rest, village.Chunk, at);
                        Assert.AreEqual(0, village.Leads.Count, at);
                    }
                }
        }

        // 방 구성 (2026-11-14 23:30·23:37 사람 결정): 방 14~18개(시작·보스 포함) = 일반 전투 방 5+, 퍼즐 방 2+(몹 없음, 이중 스위치 하나 포함),
        // 마을 1, 중간보스 1, 보물·샘 1~2, 보스 1. 이동·휴식 방 없음. 목표 15·20·30분 모두.
        [Test]
        public void RoomMixFollowsDecision()
        {
            foreach (float minutes in new[] { 15f, 20f, 30f })
                for (int seed = 0; seed < 200; seed++)
                {
                    var layout = RoomLayout.Generate(seed, minutes * 60f);
                    string at = $"seed {seed} {minutes}분";
                    Assert.GreaterOrEqual(layout.Rooms.FindAll(r => r.Kind == RoomKind.Combat).Count, RoomLayout.MinCombatRooms, at);
                    var puzzles = layout.Rooms.FindAll(r => r.Kind == RoomKind.Puzzle);
                    Assert.GreaterOrEqual(puzzles.Count, RoomLayout.MinPuzzleRooms, at);
                    foreach (var p in puzzles) Assert.IsFalse(p.IsVertical, at);
                    Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Chunk == ChunkKind.SwitchBridge).Count, at);
                    Assert.GreaterOrEqual(layout.Rooms.FindAll(r => r.Chunk == RoomLayout.DeductionPuzzle(seed)).Count, 1, $"{at}: 추리 퍼즐 방");
                    Assert.AreEqual(0, layout.Rooms.FindAll(r => r.Kind == RoomKind.Travel || r.Kind == RoomKind.Rest).Count, $"{at}: 이동·휴식 방 없음");
                    Assert.That(layout.Rooms.FindAll(r => r.Kind == RoomKind.Treasure || r.Kind == RoomKind.Spring).Count, Is.InRange(1, 2), $"{at}: 보물·샘 방");
                    Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Kind == RoomKind.Start).Count, at);
                    Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Kind == RoomKind.Boss).Count, at);
                    Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Kind == RoomKind.MidBoss).Count, at);
                    Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Kind == RoomKind.Village).Count, at);
                    Assert.That(layout.Rooms.Count, Is.InRange(14, 18), $"{at}: 방 수 (시작·보스 포함)");
                }
        }

        // 퍼즐 방에는 몹이 없다. 세로 전투 방은 바닥 전체가 전투 구역이고 적은 모두 문이 닫힐 때 나온다.
        [Test]
        public void PuzzleRoomsHaveNoEnemiesAndShaftFightsAreArenas()
        {
            foreach (var layout in Layouts())
                foreach (var room in layout.Rooms)
                {
                    if (room.Kind != RoomKind.Puzzle && !(room.Kind == RoomKind.Combat && room.IsVertical)) continue;
                    var chunk = RoomShaper.Build(layout.Seed, room).Chunk;
                    string at = $"seed {layout.Seed} room {room.Index} {room.Kind}";
                    if (room.Kind == RoomKind.Puzzle)
                    {
                        Assert.IsFalse(chunk.Features.Exists(f => f.Kind == FeatureKind.Enemy), at);
                        if (room.Chunk == ChunkKind.LanternPuzzle || room.Chunk == ChunkKind.MirrorPuzzle)
                            Assert.AreEqual(ChunkGenerator.LanternCount, chunk.Features.FindAll(f => f.Kind == FeatureKind.Lantern).Count, at);
                        continue;
                    }
                    Assert.IsTrue(chunk.Features.Exists(f => f.Kind == FeatureKind.Arena), at);
                    Assert.GreaterOrEqual(chunk.Features.FindAll(f => f.Kind == FeatureKind.Enemy).Count, 3, at);
                    Assert.IsFalse(chunk.Features.Exists(f => f.Kind == FeatureKind.Enemy && !f.Deferred), at);
                }
        }

        // 중간보스는 후보 중 시드로 하나 (2026-11-14 23:30). 같은 시드는 같은 중간보스, 후보가 둘 이상이면 시드마다 섞여 나온다.
        [Test]
        public void MidBossIsPickedFromPoolBySeed()
        {
            var seen = new HashSet<MidBossKind>();
            for (int seed = -50; seed < 200; seed++)
            {
                var kind = RoomLayout.MidBossFor(seed);
                Assert.Contains(kind, RoomLayout.MidBossPool, $"seed {seed}");
                Assert.AreEqual(kind, RoomLayout.MidBossFor(seed));
                seen.Add(kind);
            }
            Assert.AreEqual(RoomLayout.MidBossPool.Length, seen.Count, "후보가 모두 한 번 이상 나옴");
            Assert.AreEqual(RoomLayout.MidBossFor(7), RoomLayout.Generate(7, 20f * 60f).MidBoss);
        }

        // 좌우 일반 전투 방(앞 구간 기믹이 붙는 방)이 판마다 MinSideCombatRooms개 이상 (2026-11-16 기본안).
        // 여러 시드·목표 시간 통계: 최소는 규칙대로, 평균도 남겨 둔다 (콘솔).
        [Test]
        public void EveryLayoutHasEnoughSideCombatRooms()
        {
            float[] targets = { 15f * 60f, 20f * 60f, 30f * 60f };
            foreach (float target in targets)
            {
                int total = 0, min = int.MaxValue, max = 0, runs = 0;
                for (int seed = -40; seed < 300; seed++)
                {
                    var layout = RoomLayout.Generate(seed, target);
                    int side = 0, combat = 0;
                    foreach (var room in layout.Rooms)
                    {
                        if (room.Kind != RoomKind.Combat) continue;
                        combat++;
                        if (!room.IsVertical) side++;
                    }
                    Assert.GreaterOrEqual(side, RoomLayout.MinSideCombatRooms, $"seed {seed} target {target}: 좌우 전투 방 {side}개");
                    Assert.GreaterOrEqual(combat, RoomLayout.MinCombatRooms, $"seed {seed} target {target}");
                    total += side;
                    min = System.Math.Min(min, side);
                    max = System.Math.Max(max, side);
                    runs++;
                }
                System.Console.WriteLine($"목표 {target / 60f}분: 좌우 전투 방 평균 {total / (float)runs:0.00} · 최소 {min} · 최대 {max}");
            }
        }
    }
}
