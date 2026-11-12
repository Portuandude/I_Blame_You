using System;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // StagePlanner.PickTravel: 가중치 뽑기가 부동소수 반올림으로 끝까지 남았을 때 쓰는 대체값도
    // 직전 종류와 겹치지 않고, 가중치 0인 종류(아직 나올 진행도가 아닌 것)를 고르지 않아야 한다.
    public class StagePlannerPickTests
    {
        // NextDouble이 늘 1에 가장 가까운 값을 돌려주는 난수: 뽑기가 마지막 후보까지 가거나 대체 경로로 빠지게 한다.
        private class MaxRandom : Random
        {
            private readonly double value;

            public MaxRandom(double value)
            {
                this.value = value;
            }

            public override double NextDouble() => value;
        }

        private static readonly ChunkKind[] TravelKinds =
        {
            ChunkKind.Start, ChunkKind.Plains, ChunkKind.Hills, ChunkKind.SpikeField, ChunkKind.Pits, ChunkKind.Cave,
            ChunkKind.Climb, ChunkKind.CrumbleBridge, ChunkKind.MovingBridge, ChunkKind.ElementGate, ChunkKind.Pillars, ChunkKind.TwinPath
        };

        // 1.0은 NextDouble 범위([0, 1)) 밖이지만, 합이 반올림으로 조금 커지거나 작아진 경우와 같은 상황(roll이 끝까지 남음)을 만든다.
        [TestCase(0.9999999999)]
        [TestCase(1.0)]
        [TestCase(1.0000001)]
        public void FallbackNeverRepeatsLastKind(double roll)
        {
            foreach (var last in TravelKinds)
            {
                for (int step = 0; step <= 20; step++)
                {
                    float progress = step / 20f;
                    foreach (bool allowGate in new[] { true, false })
                    {
                        var kind = StagePlanner.PickTravel(new MaxRandom(roll), progress, last, allowGate);
                        Assert.AreNotEqual(last, kind, $"progress {progress} last {last} gate {allowGate}");
                        AssertAllowed(kind, progress, allowGate);
                    }
                }
            }
        }

        [Test]
        public void RandomPicksRespectProgressRules()
        {
            var rng = new Random(42);
            for (int i = 0; i < 5000; i++)
            {
                float progress = (float)rng.NextDouble();
                var last = TravelKinds[rng.Next(TravelKinds.Length)];
                bool allowGate = rng.Next(2) == 0;
                var kind = StagePlanner.PickTravel(rng, progress, last, allowGate);
                Assert.AreNotEqual(last, kind);
                AssertAllowed(kind, progress, allowGate);
            }
        }

        // StagePlanner.PickTravel의 가중치 표에서 0이 되는 조건과 같아야 한다.
        private static void AssertAllowed(ChunkKind kind, float progress, bool allowGate)
        {
            switch (kind)
            {
                case ChunkKind.Cave: Assert.GreaterOrEqual(progress, 0.15f, "동굴이 너무 이르다"); break;
                case ChunkKind.Climb: Assert.GreaterOrEqual(progress, 0.2f, "절벽이 너무 이르다"); break;
                case ChunkKind.CrumbleBridge: Assert.GreaterOrEqual(progress, 0.3f, "무너지는 다리가 너무 이르다"); break;
                case ChunkKind.MovingBridge: Assert.GreaterOrEqual(progress, 0.2f, "움직이는 발판이 너무 이르다"); break;
                case ChunkKind.Pillars: Assert.GreaterOrEqual(progress, StagePlanner.PillarsMinProgress, "기둥 숲이 너무 이르다"); break;
                case ChunkKind.TwinPath: Assert.GreaterOrEqual(progress, StagePlanner.TwinPathMinProgress, "두 갈래 길이 너무 이르다"); break;
                case ChunkKind.ElementGate:
                    Assert.IsTrue(allowGate, "봉인문 개수 초과");
                    Assert.GreaterOrEqual(progress, StagePlanner.GateMinProgress);
                    Assert.LessOrEqual(progress, StagePlanner.GateMaxProgress);
                    break;
                case ChunkKind.Plains:
                case ChunkKind.Hills:
                case ChunkKind.SpikeField:
                case ChunkKind.Pits:
                    break;
                default:
                    Assert.Fail($"이동 구간이 아닌 종류 {kind}");
                    break;
            }
        }
    }
}
