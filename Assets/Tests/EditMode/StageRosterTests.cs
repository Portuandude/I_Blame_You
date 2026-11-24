using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 스테이지별 적 구성 (Chunks/StageRoster): 스테이지 1은 그대로, 스테이지 2 숲은 슬라임 없음·코볼트 비율·보스 짝.
    public class StageRosterTests
    {
        private const float TargetSeconds = 20f * 60f;

        private static List<ChunkData> Build(int seed, int stage)
        {
            var chunks = new List<ChunkData>();
            foreach (var room in RoomLayout.Generate(seed, TargetSeconds, stage).Rooms)
                chunks.Add(RoomShaper.Build(seed, room).Chunk);
            return chunks;
        }

        [Test]
        public void StageOneIsUnchangedByDefault()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var a = new List<ChunkData>();
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms) a.Add(RoomShaper.Build(seed, room).Chunk);
                var b = Build(seed, 1);
                Assert.AreEqual(a.Count, b.Count);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(1, b[i].Stage);
                    Assert.AreEqual(a[i].Features.Count, b[i].Features.Count);
                    for (int f = 0; f < a[i].Features.Count; f++)
                        Assert.AreEqual(a[i].Features[f].Enemy, b[i].Features[f].Enemy);
                }
            }
        }

        [Test]
        public void ForestHasSameTerrainButNoSlimes()
        {
            int kobolds = 0, enemies = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var one = Build(seed, 1);
                var two = Build(seed, 2);
                Assert.AreEqual(one.Count, two.Count);
                for (int i = 0; i < one.Count; i++)
                {
                    Assert.AreEqual(2, two[i].Stage);
                    CollectionAssert.AreEqual(one[i].Tiles, two[i].Tiles, "지형은 스테이지와 상관없이 같다");
                    foreach (var f in two[i].Features)
                    {
                        if (f.Kind != FeatureKind.Enemy) continue;
                        Assert.AreNotEqual(StageEnemy.Slime, f.Enemy);
                        Assert.AreNotEqual(StageEnemy.MotherSlime, f.Enemy);
                        Assert.AreNotEqual(StageEnemy.SlimeKing, f.Enemy);
                        enemies++;
                        if (f.Enemy == StageEnemy.Kobold || f.Enemy == StageEnemy.KoboldNetter) kobolds++;
                    }
                }
            }
            Assert.Greater(kobolds, enemies / 5);
        }

        [Test]
        public void ForestBossIsChiefWithTwoWargs()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var layout = RoomLayout.Generate(seed, TargetSeconds, 2);
                var boss = layout.Find(RoomKind.Boss);
                var chunk = RoomShaper.Build(seed, boss).Chunk;
                int chiefs = 0, wargs = 0, x0 = 0, x1 = chunk.Cols - 1;
                foreach (var f in chunk.Features)
                    if (f.Kind == FeatureKind.Arena) { x0 = f.X; x1 = f.X + f.Width - 1; }
                var ids = new HashSet<int>();
                foreach (var f in chunk.Features)
                {
                    if (f.Kind != FeatureKind.Enemy) continue;
                    Assert.IsTrue(ids.Add(f.Id), "적 번호는 겹치지 않는다");
                    if (f.Enemy == StageEnemy.KoboldChief) chiefs++;
                    if (f.Enemy == StageEnemy.Warg)
                    {
                        wargs++;
                        Assert.IsTrue(f.Deferred);
                        Assert.That(f.X, Is.InRange(x0, x1));
                        Assert.AreEqual(StageTile.Empty, chunk.Get(f.X, f.Y), "와르그는 빈 칸에 선다");
                    }
                }
                Assert.AreEqual(1, chiefs);
                Assert.AreEqual(2, wargs);
            }
        }

        [Test]
        public void KoboldShareRisesWithDepthAndNetterOnlyDeep()
        {
            Assert.Less(StageRoster.KoboldChance(0.1f), StageRoster.KoboldChance(0.9f));
            for (int id = 0; id < 2000; id++)
            {
                double roll = StageRoster.Roll(7, id);
                Assert.That(roll, Is.InRange(0.0, 1.0));
                Assert.AreNotEqual(StageEnemy.KoboldNetter,
                    StageRoster.Adapt(StageEnemy.GoblinArcher, 2, StageRoster.KoboldNetterMinDifficulty - 0.01f, roll));
                Assert.AreEqual(StageEnemy.GoblinArcher, StageRoster.Adapt(StageEnemy.GoblinArcher, 1, 0.9f, roll));
                Assert.AreEqual(StageEnemy.Slime, StageRoster.Adapt(StageEnemy.Slime, 1, 0.9f, roll));
            }

            int low = 0, high = 0;
            for (int id = 0; id < 4000; id++)
            {
                double roll = StageRoster.Roll(11, id);
                if (StageRoster.Adapt(StageEnemy.Goblin, 2, 0.1f, roll) == StageEnemy.Kobold) low++;
                if (StageRoster.Adapt(StageEnemy.Goblin, 2, 0.9f, roll) == StageEnemy.Kobold) high++;
            }
            Assert.Greater(high, low);
            Assert.AreEqual(StageRoster.KoboldChance(0.9f), high / 4000.0, 0.04);
        }

        [Test]
        public void StageIsClamped()
        {
            Assert.AreEqual(1, RoomLayout.Generate(3, TargetSeconds, 0).Stage);
            Assert.AreEqual(StageRoster.LastStage, RoomLayout.Generate(3, TargetSeconds, 99).Stage);
        }
    }
}
