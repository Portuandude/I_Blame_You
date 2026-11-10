using System;
using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 미니맵 던그리드 방식 표시 규칙(MinimapRules): 안 가 본 방은 칸 없이 통로 실선만.
    public class MinimapRulesTests
    {
        [Test]
        public void OnlyVisitedOrCurrentRoomsAreDrawn()
        {
            Assert.IsTrue(MinimapRules.ShowRoom(true, false));
            Assert.IsTrue(MinimapRules.ShowRoom(false, true));
            Assert.IsFalse(MinimapRules.ShowRoom(false, false));
            Assert.IsFalse(MinimapRules.ShowMarks(false, false));
            Assert.IsTrue(MinimapRules.ShowMarks(true, false));
        }

        [Test]
        public void LinkKindDependsOnWhichSideWasVisited()
        {
            Assert.AreEqual(MapLink.Full, MinimapRules.Link(true, true));
            Assert.AreEqual(MapLink.Stub, MinimapRules.Link(true, false));
            Assert.AreEqual(MapLink.Stub, MinimapRules.Link(false, true));
            Assert.AreEqual(MapLink.None, MinimapRules.Link(false, false));
        }

        // 여러 시드·여러 방문 상태에서: 각 문은 정확히 한 번(보이는 쪽이 하나라도 있을 때), Stub은 늘 보이는 방에서 시작.
        [Test]
        public void EveryDoorTouchingAShownRoomIsDrawnOnceFromTheShownSide()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var layout = RoomLayout.Generate(seed, 15f * 60f);
                var rng = new Random(seed);
                var shown = new HashSet<int>();
                foreach (var room in layout.Rooms) if (rng.NextDouble() < 0.5) shown.Add(room.Index);
                shown.Add(0);

                var lines = MinimapRules.Links(layout, shown.Contains);
                var drawn = new HashSet<(int, int)>();
                foreach (var line in lines)
                {
                    Assert.IsTrue(shown.Contains(line.From.Index), $"seed {seed}: 안 보이는 방에서 실선이 시작함");
                    Assert.AreNotEqual(DoorDir.None, line.From.Doors & line.Dir);
                    var other = layout.Neighbor(line.From, line.Dir);
                    Assert.IsNotNull(other);
                    var expected = shown.Contains(other.Index) ? MapLink.Full : MapLink.Stub;
                    Assert.AreEqual(expected, line.Kind, $"seed {seed}");
                    var key = (Math.Min(line.From.Index, other.Index), Math.Max(line.From.Index, other.Index));
                    Assert.IsTrue(drawn.Add(key), $"seed {seed}: 같은 통로를 두 번 그림");
                }

                // 보이는 방에 붙은 문은 빠짐없이, 둘 다 안 보이는 문은 하나도 없이
                foreach (var room in layout.Rooms)
                    foreach (var dir in new[] { DoorDir.Right, DoorDir.Up })
                    {
                        if ((room.Doors & dir) == 0) continue;
                        var other = layout.Neighbor(room, dir);
                        var key = (Math.Min(room.Index, other.Index), Math.Max(room.Index, other.Index));
                        bool any = shown.Contains(room.Index) || shown.Contains(other.Index);
                        Assert.AreEqual(any, drawn.Contains(key), $"seed {seed}: 방 {room.Index}의 {dir} 문");
                    }
            }
        }

        [Test]
        public void NothingVisitedButStartShowsOnlyStartStubs()
        {
            var layout = RoomLayout.Generate(3, 15f * 60f);
            var lines = MinimapRules.Links(layout, i => i == 0);
            foreach (var line in lines)
            {
                Assert.AreEqual(0, line.From.Index);
                Assert.AreEqual(MapLink.Stub, line.Kind);
            }
            Assert.Greater(lines.Count, 0);
        }
    }
}
