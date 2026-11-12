using System;
using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 방 격자형 배치(RoomLayout) 규칙. Docs/LevelDesign.md의 "생성 규칙"을 고정한다.
    public class RoomLayoutTests
    {
        private const float TargetSeconds = 15f * 60f; // 스테이지 1 목표 15분 (2026-11-10 사람 결정)
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
        public void GridIsFourByThreeWithNineToTwelveRooms()
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
                Assert.AreEqual(1, start.Y, $"seed {layout.Seed}");
                Assert.That(start.X, Is.InRange(1, 2), $"seed {layout.Seed}");
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

        // 지금 한 화면 방 9~12개로는 15분에 못 미친다 (2026-11-12 측정: 약 11분). 큰 방 작업(백로그)에서 15~30분으로 올린다.
        // 여기서는 너무 짧아지지 않는지만 본다.
        [Test]
        public void EstimatedTimeIsReported()
        {
            float min = float.MaxValue, max = 0f;
            foreach (var layout in Layouts())
            {
                float seconds = RoomLayout.EstimatedSeconds(layout);
                min = Math.Min(min, seconds);
                max = Math.Max(max, seconds);
            }
            Console.WriteLine($"방 격자 예상 시간: {min / 60f:0.0}~{max / 60f:0.0}분");
            Assert.GreaterOrEqual(min, 9f * 60f);
            Assert.LessOrEqual(max, RoomLayout.MaxSeconds);
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
                Assert.LessOrEqual(Math.Abs(progress - RoomLayout.MidBossProgress), 1f / (main.Count - 1) + 1e-4f, $"seed {layout.Seed}: {progress}");
            }
        }

        [Test]
        public void OnlyTravelRestAndRewardRoomsUseVerticalDoors()
        {
            foreach (var layout in Layouts())
            {
                foreach (var room in layout.Rooms)
                {
                    if (!room.IsVertical) continue;
                    Assert.IsTrue(RoomLayout.CanBeVertical(room.Kind) || room.Kind == RoomKind.Treasure || room.Kind == RoomKind.Spring,
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

        [Test]
        public void HasFightsAndOneSwitchBridge()
        {
            foreach (var layout in Layouts())
            {
                int fights = layout.Rooms.FindAll(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Elite).Count;
                Assert.GreaterOrEqual(fights, RoomLayout.MinFights, $"seed {layout.Seed}");
                Assert.AreEqual(1, layout.Rooms.FindAll(r => r.Chunk == ChunkKind.SwitchBridge).Count, $"seed {layout.Seed}");
                foreach (var room in layout.Rooms)
                    if (room.Kind == RoomKind.Combat || room.Kind == RoomKind.Elite)
                        Assert.GreaterOrEqual(room.Difficulty, RoomLayout.MinFightDifficulty - 1e-4f, $"seed {layout.Seed}");
            }
        }

        [Test]
        public void FightsNeverComeBackToBackAndMidBossIsFollowedByRest()
        {
            foreach (var layout in Layouts())
            {
                var main = new List<RoomData>(layout.MainPathRooms());
                for (int i = 1; i < main.Count; i++)
                    Assert.IsFalse(main[i - 1].LocksDoors && main[i].LocksDoors,
                        $"seed {layout.Seed}: 문 잠기는 방이 연달아 ({i - 1} {main[i - 1].Kind} → {i} {main[i].Kind})");
                Assert.AreEqual(RoomKind.Rest, main[layout.MidBossPathIndex + 1].Kind, $"seed {layout.Seed}: 중간보스 뒤는 휴식 방");
            }
        }
    }
}
