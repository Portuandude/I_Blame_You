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
    }
}
