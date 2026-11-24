using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 숲 함정 놓기 (Chunks/ForestDressing): 스테이지 1엔 없음, 스테이지 2 전투 방엔 덤불·덩굴·통나무가 빈 바닥 위에, 덩굴 벽은 보물 방에만.
    public class ForestDressingTests
    {
        private const float TargetSeconds = 20f * 60f;

        private static bool IsForestTrap(FeatureKind kind) =>
            kind == FeatureKind.ThornBush || kind == FeatureKind.VineFloor || kind == FeatureKind.FallingLog || kind == FeatureKind.VineWall;

        [Test]
        public void StageOneHasNoForestTraps()
        {
            for (int seed = 0; seed < 30; seed++)
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                    foreach (var f in RoomShaper.Build(seed, room).Chunk.Features)
                        Assert.IsFalse(IsForestTrap(f.Kind));
        }

        [Test]
        public void ForestTrapsSitOnClearFloorAwayFromDoors()
        {
            int thorns = 0, vines = 0, logs = 0, walls = 0;
            for (int seed = 0; seed < 120; seed++)
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds, 2).Rooms)
                {
                    var geometry = RoomShaper.Build(seed, room);
                    var chunk = geometry.Chunk;
                    foreach (var f in chunk.Features)
                    {
                        if (!IsForestTrap(f.Kind)) continue;
                        if (f.Kind == FeatureKind.VineWall)
                        {
                            walls++;
                            Assert.AreEqual(RoomKind.Treasure, room.Kind);
                            Assert.IsTrue(ForestDressing.PairGateFits(room));
                            Assert.IsFalse(chunk.HasFeature(FeatureKind.ElementGate), "봉인문과 겹치지 않는다");
                            continue;
                        }
                        Assert.That(room.Kind == RoomKind.Combat || room.Kind == RoomKind.Elite);
                        Assert.IsFalse(room.IsVertical);
                        Assert.GreaterOrEqual(f.X, ForestDressing.EdgeMargin);
                        Assert.Less(f.X + f.Width - 1, chunk.Cols - ForestDressing.EdgeMargin);
                        if (f.Kind == FeatureKind.FallingLog)
                        {
                            logs++;
                            int floor = ForestDressing.FloorAt(chunk, f.X);
                            Assert.AreEqual(floor + ForestDressing.LogHeight, f.Y);
                            for (int y = floor; y <= f.Y; y++) Assert.AreEqual(StageTile.Empty, chunk.Get(f.X, y), "통나무 아래는 비어 있다");
                            continue;
                        }
                        if (f.Kind == FeatureKind.ThornBush) thorns++; else vines++;
                        for (int x = f.X; x < f.X + f.Width; x++)
                        {
                            Assert.AreEqual(f.Y, ForestDressing.FloorAt(chunk, x), "평평한 바닥 위");
                            Assert.AreEqual(StageTile.Empty, chunk.Get(x, f.Y));
                        }
                    }
                }
            Assert.Greater(thorns, 100);
            Assert.Greater(vines, 30);
            Assert.Greater(logs, 30);
            Assert.Greater(walls, 0);
        }

        [Test]
        public void SameSeedSameTraps()
        {
            var a = RoomLayout.Generate(5, TargetSeconds, 2);
            var b = RoomLayout.Generate(5, TargetSeconds, 2);
            for (int i = 0; i < a.Rooms.Count; i++)
            {
                var fa = RoomShaper.Build(5, a.Rooms[i]).Chunk.Features;
                var fb = RoomShaper.Build(5, b.Rooms[i]).Chunk.Features;
                Assert.AreEqual(fa.Count, fb.Count);
                for (int k = 0; k < fa.Count; k++) Assert.AreEqual(fa[k].X, fb[k].X);
            }
        }
    }
}
