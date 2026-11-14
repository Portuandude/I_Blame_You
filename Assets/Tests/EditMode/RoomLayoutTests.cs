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
        public void GridIsFiveByFourWithThirteenToSeventeenRooms()
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

        [Test]
        public void BossIsFarthestAndMainPathIsTreePath()
        {
            foreach (var layout in Layouts())
            {
                var main = new List<RoomData>(layout.MainPathRooms());
                var boss = main[main.Count - 1];
                Assert.AreEqual(RoomKind.Boss, boss.Kind, $"seed {layout.Seed}");
                Assert.AreEqual(ChunkKind.Boss, boss.Chunk);
                Assert.IsFalse(boss.IsVertical);
                Assert.AreEqual(boss.EntryDoor, boss.Doors, $"seed {layout.Seed}: 보스 방은 막다른 방");
                Assert.GreaterOrEqual(boss.Distance, RoomLayout.MinBossDistance);
                foreach (var room in layout.Rooms) Assert.LessOrEqual(room.Distance, boss.Distance, $"seed {layout.Seed}: 보스가 가장 멀다");
                Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Kind == RoomKind.Boss).Count);
                for (int i = 0; i < main.Count; i++)
                {
                    Assert.AreEqual(i, main[i].PathIndex, $"seed {layout.Seed}");
                    Assert.AreEqual(i, main[i].Distance, $"seed {layout.Seed}");
                    if (i > 0) Assert.AreEqual(RoomLayout.Opposite(main[i - 1].ExitDoor), main[i].EntryDoor, $"seed {layout.Seed}");
                }
                foreach (var room in layout.Rooms)
                {
                    if (room.MainPath) continue;
                    Assert.IsTrue(layout.Rooms[room.ParentIndex].MainPath, $"seed {layout.Seed}: 곁가지의 ParentIndex는 주 경로 방");
                }
            }
        }

        [Test]
        public void DifficultyFollowsDistance()
        {
            foreach (var layout in Layouts())
            {
                float boss = new List<RoomData>(layout.MainPathRooms()).FindLast(r => true).Distance;
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

        [Test]
        public void MidBossSitsNearFiftyFivePercentOfThePath()
        {
            foreach (var layout in Layouts())
            {
                var main = new List<RoomData>(layout.MainPathRooms());
                var mids = layout.Rooms.FindAll(r => r.Kind == RoomKind.MidBoss);
                Assert.AreEqual(1, mids.Count, $"seed {layout.Seed}");
                var mid = mids[0];
                Assert.IsTrue(mid.MainPath);
                Assert.IsFalse(mid.IsVertical);
                Assert.AreEqual(layout.MidBossPathIndex, mid.PathIndex);
                Assert.That(mid.PathIndex, Is.InRange(2, main.Count - 3), $"seed {layout.Seed}: 시작·보스와 붙지 않음");
                float progress = mid.PathIndex / (float)(main.Count - 1);
                Assert.LessOrEqual(Math.Abs(progress - RoomLayout.MidBossDepth), 1f / (main.Count - 1) + 1e-4f, $"seed {layout.Seed}: {progress}");
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
        public void RewardRoomsAreDeadEndsOffThePath()
        {
            foreach (var layout in Layouts())
            {
                int treasure = 0, rewards = 0;
                foreach (var room in layout.Rooms)
                {
                    if (room.Kind != RoomKind.Treasure && room.Kind != RoomKind.Spring) continue;
                    rewards++;
                    if (room.Kind == RoomKind.Treasure) treasure++;
                    Assert.IsFalse(room.MainPath, $"seed {layout.Seed}");
                    Assert.AreEqual(room.EntryDoor, room.Doors, $"seed {layout.Seed}: 보상 곁방은 문이 하나뿐");
                }

                Assert.That(rewards, Is.InRange(RoomLayout.MinRewardRooms, RoomLayout.MaxRewardRooms), $"seed {layout.Seed}");
                Assert.GreaterOrEqual(treasure, 1);
            }
        }

        // NPC 마을 (2026-11-12, 2026-11-14 1~2개): 판마다(목표 15·20·30분) 하나(방 SecondVillageRooms개 이상이면 둘까지), 주 경로 밖 문 하나짜리 좌우 방, 시작 방에서 2칸 이상,
        // 보스 방과 격자로 붙지 않음, 문이 잠기지 않고 한 화면 평지(휴식 청크).
        [Test]
        public void VillageIsOneDeadEndSideRoom()
        {
            foreach (float minutes in new[] { 15f, 20f, 30f })
                for (int seed = 0; seed < 300; seed++)
                {
                    var layout = RoomLayout.Generate(seed, minutes * 60f);
                    RoomData boss = layout.Rooms.Find(r => r.Kind == RoomKind.Boss);
                    var villages = layout.Rooms.FindAll(r => r.Kind == RoomKind.Village);
                    string at = $"seed {seed} {minutes}분";
                    Assert.That(villages.Count, Is.InRange(1, layout.Rooms.Count >= RoomLayout.SecondVillageRooms ? RoomLayout.MaxVillages : 1), at);
                    foreach (var village in villages)
                    {
                        Assert.IsFalse(village.MainPath, at);
                        Assert.AreEqual(village.EntryDoor, village.Doors, $"{at}: 마을은 문이 하나뿐");
                        Assert.IsFalse(village.IsVertical, at);
                        Assert.GreaterOrEqual(village.Distance, RoomLayout.VillageMinDistance, at);
                        Assert.Greater(Math.Abs(village.X - boss.X) + Math.Abs(village.Y - boss.Y), 1, $"{at}: 보스 방 옆");
                        Assert.IsFalse(village.LocksDoors, at);
                        Assert.AreEqual(ChunkKind.Rest, village.Chunk, at);
                        Assert.AreEqual(0, village.Leads.Count, at);
                    }
                }
        }

        // 방 구성 (2026-11-14 사람 결정): 일반 전투 방 5+, 퍼즐 방 2+(몹 없음, 이중 스위치 하나 포함), 이동 방 없음. 목표 15·20·30분 모두.
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
                    Assert.AreEqual(0, layout.Rooms.FindAll(r => r.Kind == RoomKind.Travel || r.Kind == RoomKind.Rest).Count, $"{at}: 이동·휴식 방 없음");
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
                        continue;
                    }
                    Assert.IsTrue(chunk.Features.Exists(f => f.Kind == FeatureKind.Arena), at);
                    Assert.GreaterOrEqual(chunk.Features.FindAll(f => f.Kind == FeatureKind.Enemy).Count, 3, at);
                    Assert.IsFalse(chunk.Features.Exists(f => f.Kind == FeatureKind.Enemy && !f.Deferred), at);
                }
        }
    }
}
