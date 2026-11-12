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
