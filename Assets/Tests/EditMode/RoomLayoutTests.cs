using System;
using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 방 격자형 배치(RoomLayout) 규칙. Docs/LevelDesign.md의 "생성 규칙"을 고정한다.
    public class RoomLayoutTests
    {
        private const float TargetSeconds = 30f * 60f;
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
        public void MainPathFollowsPlannerAndEndsAtBoss()
        {
            foreach (var layout in Layouts())
            {
                var plan = StagePlanner.Plan(layout.Seed, TargetSeconds);
                var main = new List<RoomData>(layout.MainPathRooms());
                Assert.AreEqual(plan.Count + 1, main.Count, $"seed {layout.Seed}: 계획 + 중간보스 1개");
                int p = 0;
                for (int i = 0; i < main.Count; i++)
                {
                    Assert.AreEqual(i, main[i].PathIndex);
                    if (i == layout.MidBossPathIndex) continue;
                    Assert.AreEqual(plan[p].Key, main[i].Chunk, $"seed {layout.Seed} path {i}");
                    p++;
                }

                var boss = main[main.Count - 1];
                Assert.AreEqual(RoomKind.Boss, boss.Kind);
                Assert.AreEqual(DoorDir.Left, boss.EntryDoor, "보스 방은 왼쪽 문으로 들어간다");
                Assert.AreEqual(DoorDir.None, boss.ExitDoor);
                for (int i = 1; i < main.Count; i++)
                {
                    // 앞 방의 나가는 문 = 이 방의 들어오는 문의 반대
                    Assert.AreEqual(RoomLayout.Opposite(main[i - 1].ExitDoor), main[i].EntryDoor);
                }
            }
        }

        [Test]
        public void MidBossSitsAtFiftyFivePercentOfThePath()
        {
            foreach (var layout in Layouts())
            {
                var main = new List<RoomData>(layout.MainPathRooms());
                int index = layout.MidBossPathIndex;
                Assert.AreEqual(RoomKind.MidBoss, main[index].Kind);
                float progress = index / (float)(main.Count - 1);
                Assert.That(progress, Is.InRange(0.53f, 0.57f), $"seed {layout.Seed}: {progress}");
                int count = 0;
                foreach (var room in layout.Rooms) if (room.Kind == RoomKind.MidBoss) count++;
                Assert.AreEqual(1, count);
            }
        }

        [Test]
        public void OnlyTravelAndRestRoomsUseVerticalDoors()
        {
            foreach (var layout in Layouts())
            {
                foreach (var room in layout.Rooms)
                {
                    if (!room.MainPath || RoomLayout.CanBeVertical(room.Kind)) continue;
                    Assert.IsFalse(room.IsVertical, $"seed {layout.Seed} room {room.Index} ({room.Kind})에 위/아래 문");
                    Assert.AreEqual(DoorDir.None, room.Doors & DoorDir.Up);
                }
            }
        }

        // 세로 플레이 비중: 주 경로 방 중 위/아래 문이 있는 방이 평균 20~45%, 모든 스테이지에 적어도 하나.
        [Test]
        public void VerticalRoomShareIsInRange()
        {
            int vertical = 0, total = 0;
            foreach (var layout in Layouts())
            {
                int here = 0;
                foreach (var room in layout.MainPathRooms())
                {
                    total++;
                    if (room.IsVertical) here++;
                }

                Assert.Greater(here, 0, $"seed {layout.Seed}: 세로 방이 없음");
                vertical += here;
            }

            float share = vertical / (float)total;
            Assert.That(share, Is.InRange(0.2f, 0.45f), $"세로 방 비율 {share}");
        }

        [Test]
        public void StaysWithinVerticalBounds()
        {
            foreach (var layout in Layouts())
            {
                foreach (var room in layout.Rooms)
                    Assert.LessOrEqual(Math.Abs(room.Y), RoomLayout.MaxRise + 1, $"seed {layout.Seed} room {room.Index}");
            }
        }

        [Test]
        public void SideRoomsAreRewardDeadEnds()
        {
            foreach (var layout in Layouts())
            {
                int side = 0, treasure = 0;
                foreach (var room in layout.Rooms)
                {
                    if (room.MainPath) continue;
                    side++;
                    if (room.Kind == RoomKind.Treasure) treasure++;
                    Assert.That(room.Kind, Is.EqualTo(RoomKind.Treasure).Or.EqualTo(RoomKind.Spring));
                    Assert.AreEqual(room.EntryDoor, room.Doors, "곁방은 문이 하나뿐");
                    var parent = layout.Rooms[room.ParentIndex];
                    Assert.IsTrue(parent.MainPath);
                    Assert.IsTrue(RoomLayout.CanBeVertical(parent.Kind), "곁방은 이동·휴식 방에만 붙는다");
                }

                Assert.That(side, Is.InRange(RoomLayout.MinSideRooms, RoomLayout.MaxSideRooms), $"seed {layout.Seed}");
                Assert.GreaterOrEqual(treasure, 1);
            }
        }
    }
}
