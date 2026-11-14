using System.Linq;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 기능 테스트 모드 맵 (FeatureTestStage): 적 없이 협동 봉인문·이중 스위치 발판을 바로 시험할 수 있어야 한다.
    public class FeatureTestStageTests
    {
        [Test]
        public void ChunksFollowOrderAndPassValidator()
        {
            var stage = FeatureTestStage.Build();
            Assert.AreEqual(FeatureTestStage.Order.Length, stage.Chunks.Count);
            Assert.AreEqual(FeatureTestStage.Order.Length, FeatureTestStage.Names.Length);
            for (int i = 0; i < stage.Chunks.Count; i++)
            {
                Assert.AreEqual(FeatureTestStage.Order[i], stage.Chunks[i].Kind, $"chunk {i}");
                if (i == stage.Chunks.Count - 1) continue; // 끝 방은 오른쪽을 벽으로 막아 검증기의 "오른쪽 끝에 닿기"를 일부러 어긴다
                Assert.IsTrue(StageValidator.Validate(stage.Chunks[i], out string reason), $"chunk {i}: {reason}");
            }
        }

        [Test]
        public void HasNoEnemiesAndOnePlayerStart()
        {
            var features = FeatureTestStage.Build().Chunks.SelectMany(c => c.Features).ToList();
            Assert.IsFalse(features.Any(f => f.Kind == FeatureKind.Enemy || f.Kind == FeatureKind.Arena));
            Assert.AreEqual(1, features.Count(f => f.Kind == FeatureKind.PlayerStart));
        }

        [Test]
        public void GateIsCoopWithTwoDifferentElements()
        {
            var gates = FeatureTestStage.Build().Chunks.SelectMany(c => c.Features).Where(f => f.Kind == FeatureKind.ElementGate).ToList();
            Assert.AreEqual(1, gates.Count);
            Assert.IsTrue(gates[0].ShieldFirst);
            Assert.IsTrue(gates[0].HasSecondElement);
            Assert.AreNotEqual(gates[0].ElementIndex, gates[0].SecondElementIndex);
        }

        [Test]
        public void SwitchBridgeHasTwoSwitchesAndDrivenPlatforms()
        {
            var bridge = FeatureTestStage.Build().Chunks.Single(c => c.Kind == ChunkKind.SwitchBridge);
            Assert.AreEqual(2, bridge.Features.Count(f => f.Kind == FeatureKind.PlatformSwitch));
            Assert.AreEqual(2, bridge.Features.Count(f => f.Kind == FeatureKind.MovingPlatform && f.SwitchDriven));
        }

        // 통통 버섯 방: 버섯 하나 + 그 위 선반 동전 (2026-11-14)
        [Test]
        public void BounceAreaHasMushroomAndLedgeCoins()
        {
            var chunk = FeatureTestStage.Build().Chunks[FeatureTestStage.BounceArea];
            var mushroom = chunk.Features.Single(f => f.Kind == FeatureKind.BounceMushroom);
            int stand = mushroom.Y + ChunkGenerator.BounceLedgeRise;
            Assert.AreEqual(StageTile.Platform, chunk.Get(mushroom.X, stand - 1));
            Assert.IsTrue(chunk.Features.Any(f => f.Kind == FeatureKind.CoinPile && f.Y == stand));
            Assert.IsTrue(StageValidator.Validate(chunk, out string reason), reason);
        }

        // 바람 구멍: 같은 방 오른쪽에 환풍구 하나, 기둥 안은 비어 있고 꼭대기가 옆 선반보다 높고, 선반 위 동전 (2026-11-14)
        [Test]
        public void BounceAreaHasWindVentBesideLedge()
        {
            var chunk = FeatureTestStage.Build().Chunks[FeatureTestStage.BounceArea];
            var vent = chunk.Features.Single(f => f.Kind == FeatureKind.WindVent);
            int stand = vent.Y + ChunkGenerator.BounceLedgeRise;
            Assert.AreEqual(FeatureTestStage.WindVentX, vent.X);
            Assert.Greater(vent.Y + vent.Direction, stand + 1);
            for (int x = vent.X; x < vent.X + vent.Width; x++)
                for (int y = vent.Y; y < vent.Y + vent.Direction; y++) Assert.AreEqual(StageTile.Empty, chunk.Get(x, y), $"기둥 안 ({x},{y})");
            Assert.AreEqual(StageTile.Platform, chunk.Get(vent.X - 1, stand - 1));
            Assert.IsTrue(chunk.Features.Any(f => f.Kind == FeatureKind.CoinPile && f.Y == stand && f.X < vent.X));
            Assert.IsTrue(StageValidator.Validate(chunk, out string reason), reason);
        }

        [Test]
        public void EndsWithWalls()
        {
            var stage = FeatureTestStage.Build();
            var last = stage.Chunks[stage.Chunks.Count - 1];
            Assert.IsTrue(stage.Chunks[0].IsSolid(0, ChunkData.Height - 1), "왼쪽 벽");
            Assert.IsTrue(last.IsSolid(ChunkData.Width - 1, ChunkData.Height - 1), "오른쪽 벽");
        }
    
        // 버섯 탑 방: 길 버섯 둘(바닥·P1 위) + P1 발판 + 무너지는 P2 + P2 동전 + 저격 선반, 적 없음 (2026-11-14)
        [Test]
        public void TowerAreaHasMushroomTower()
        {
            var chunk = FeatureTestStage.Build().Chunks[FeatureTestStage.TowerArea];
            var mushrooms = chunk.Features.Where(f => f.Kind == FeatureKind.BounceMushroom).OrderBy(f => f.Y).ToList();
            Assert.AreEqual(2, mushrooms.Count);
            Assert.IsTrue(mushrooms.All(m => m.Direction == 1), "길 버섯");
            int g = mushrooms[0].Y;
            int stand1 = g + RoomShaper.TowerStep;
            Assert.AreEqual(RoomShaper.TowerMushroom1X, mushrooms[0].X);
            Assert.AreEqual(stand1, mushrooms[1].Y);
            Assert.AreEqual(StageTile.Platform, chunk.Get(RoomShaper.TowerMushroom2X, stand1 - 1), "P1");
            Assert.AreEqual(StageTile.Platform, chunk.Get(RoomShaper.TowerSniperX0, stand1 - 1), "저격 선반");
            var crumble = chunk.Features.Single(f => f.Kind == FeatureKind.Crumble);
            Assert.AreEqual(stand1 + RoomShaper.TowerStep - 1, crumble.Y);
            Assert.IsTrue(crumble.X <= RoomShaper.TowerMushroom2X && RoomShaper.TowerMushroom2X < crumble.X + crumble.Width, "P2가 M2 위");
            Assert.IsTrue(chunk.Features.Any(f => f.Kind == FeatureKind.CoinPile && f.Y == crumble.Y + 1));
            Assert.IsFalse(chunk.Features.Any(f => f.Kind == FeatureKind.Enemy));
            for (int y = g; y < ChunkData.Height; y++) chunk.Tiles[ChunkData.Width - 1, y] = StageTile.Empty; // 끝 벽을 걷어 내고 길 검사
            Assert.IsTrue(StageValidator.Validate(chunk, out string reason), reason);
            Assert.AreEqual("버섯 탑 방", FeatureTestStage.Names[FeatureTestStage.TowerArea]);
        }
    }
}
