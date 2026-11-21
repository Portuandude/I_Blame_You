using System;
using System.Collections.Generic;
using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests.EditMode
{
    // P2 마법진 그리기 규칙 (MagicCircleRules, 2026-11-16 사람 결정)
    public class MagicCircleRulesTests
    {
        private static List<CirclePoint> Shift(IList<CirclePoint> path, float dx, float dy)
        {
            var list = new List<CirclePoint>();
            foreach (var p in path) list.Add(new CirclePoint(p.X + dx, p.Y + dy));
            return list;
        }

        [Test]
        public void TierIsCostRankInElement()
        {
            var fire = new[] { 20f, 25f, 35f };
            Assert.AreEqual(0, MagicCircleRules.TierFor(20f, fire));
            Assert.AreEqual(1, MagicCircleRules.TierFor(25f, fire));
            Assert.AreEqual(2, MagicCircleRules.TierFor(35f, fire));
            Assert.AreEqual(1, MagicCircleRules.TierFor(35f, fire, tailwind: true), "순풍이면 한 단계 쉬움");
            Assert.AreEqual(0, MagicCircleRules.TierFor(20f, fire, tailwind: true));
            Assert.AreEqual(2, MagicCircleRules.TierFor(99f, new[] { 1f, 2f, 3f, 4f }), "셋째 이후는 모두 대마법");
            Assert.AreEqual(0, MagicCircleRules.TierFor(10f, null));
        }

        [Test]
        public void HarderTiersAreNarrowerAndLonger()
        {
            for (int tier = 1; tier < MagicCircleRules.TierCount; tier++)
            {
                Assert.Less(MagicCircleRules.ToleranceOf(tier), MagicCircleRules.ToleranceOf(tier - 1));
                Assert.Greater(MagicCircleRules.TimeLimitOf(tier), MagicCircleRules.TimeLimitOf(tier - 1));
            }
            Assert.AreEqual(MagicCircleRules.ToleranceOf(2) * 1.2f, MagicCircleRules.ToleranceOf(2, 0.2f), 1e-5, "마법진 여유는 허용 폭 배율");
            Assert.AreEqual("견습", MagicCircleRules.TierName(0));
            Assert.AreEqual("대마법", MagicCircleRules.TierName(7));
        }

        // 단계마다 한 획이고, 위 단계일수록 길다. 2단계는 바깥 원에서, 3단계는 바깥 원 위 도형 꼭짓점에서 시작해 안쪽 원을 지난다.
        [Test]
        public void PathsAreOneStrokeAndGrowWithTier()
        {
            for (int element = 0; element < 4; element++)
            {
                var rune = MagicCircleRules.Path(element, 0);
                var ringed = MagicCircleRules.Path(element, 1);
                var grand = MagicCircleRules.Path(element, 2);
                Assert.Greater(rune.Count, 4, $"속성 {element}");
                Assert.Greater(MagicCircleRules.Length(ringed), MagicCircleRules.Length(rune));
                Assert.Greater(MagicCircleRules.Length(grand), MagicCircleRules.Length(ringed));

                float r1 = (float)Math.Sqrt(ringed[0].X * ringed[0].X + ringed[0].Y * ringed[0].Y);
                float r2 = (float)Math.Sqrt(grand[0].X * grand[0].X + grand[0].Y * grand[0].Y);
                Assert.AreEqual(MagicCircleRules.OuterRing, r1, 1e-3);
                Assert.AreEqual(MagicCircleRules.OuterRing, r2, 1e-3);
                bool touchesInner = grand.Exists(p => Math.Abs(Math.Sqrt(p.X * p.X + p.Y * p.Y) - MagicCircleRules.InnerRing) < 1e-3);
                Assert.IsTrue(touchesInner, $"속성 {element}: 안쪽 원");
                foreach (var p in grand) Assert.LessOrEqual(Math.Sqrt(p.X * p.X + p.Y * p.Y), 0.9, "판 밖으로 나가지 않는다");

                // 한 획: 이웃한 점 사이가 크게 뛰지 않는다 (바큇살도 이어진 선)
                for (int i = 1; i < grand.Count; i++)
                {
                    float dx = grand[i].X - grand[i - 1].X, dy = grand[i].Y - grand[i - 1].Y;
                    Assert.Less(Math.Sqrt(dx * dx + dy * dy), 1.8);
                }
            }
        }

        [Test]
        public void RunesDifferPerElement()
        {
            var lengths = new HashSet<int>();
            for (int element = 0; element < 4; element++) lengths.Add((int)(MagicCircleRules.Length(MagicCircleRules.RuneBox(element)) * 10));
            Assert.AreEqual(4, lengths.Count);
        }

        [Test]
        public void ExactTraceIsHundredAndSmallWobbleStaysHigh()
        {
            for (int element = 0; element < 4; element++)
                for (int tier = 0; tier < MagicCircleRules.TierCount; tier++)
                {
                    var path = MagicCircleRules.Path(element, tier);
                    float tol = MagicCircleRules.ToleranceOf(tier);
                    Assert.AreEqual(100, MagicCircleRules.Score(path, path, tol).Percent, $"{element}/{tier}");
                    var wobble = MagicCircleRules.Score(path, Shift(path, tol * 0.5f, -tol * 0.4f), tol);
                    Assert.GreaterOrEqual(wobble.Percent, 90, $"{element}/{tier}: 허용 폭 안 흔들림");
                    Assert.IsFalse(wobble.Failed);
                }
        }

        [Test]
        public void HalfDrawnOrFarOffFails()
        {
            var path = MagicCircleRules.Path(0, 1);
            float tol = MagicCircleRules.ToleranceOf(1);
            var half = path.GetRange(0, path.Count / 3);
            Assert.IsTrue(MagicCircleRules.Score(path, half, tol).Failed, "앞 1/3만 그리면 실패");
            Assert.IsTrue(MagicCircleRules.Score(path, Shift(path, 0.3f, 0.3f), tol).Failed, "통째로 엇나가면 실패");
            Assert.AreEqual(0, MagicCircleRules.Score(path, new List<CirclePoint>(), tol).Percent);
            Assert.AreEqual(0, MagicCircleRules.Score(path, null, tol).Percent);
        }

        // 판 전체를 지그재그로 칠하면 덮은 정도는 높아도 샌 정도 때문에 실패
        [Test]
        public void ScribbleOverEverythingFails()
        {
            var scribble = new List<CirclePoint>();
            for (int row = 0; row <= 20; row++)
            {
                float y = -0.9f + row * 0.09f;
                scribble.Add(new CirclePoint(row % 2 == 0 ? -0.9f : 0.9f, y));
                scribble.Add(new CirclePoint(row % 2 == 0 ? 0.9f : -0.9f, y));
            }
            for (int element = 0; element < 4; element++)
            {
                var path = MagicCircleRules.Path(element, 0);
                var result = MagicCircleRules.Score(path, scribble, MagicCircleRules.ToleranceOf(0));
                Assert.IsTrue(result.Failed, $"속성 {element}: {result.Percent}% (덮음 {result.Coverage:0.00}, 샘 {result.Precision:0.00})");
            }
        }

        [Test]
        public void DamageScalesWithPercent()
        {
            Assert.AreEqual(0f, MagicCircleRules.DamageMultiplier(50));
            Assert.AreEqual(0f, MagicCircleRules.DamageMultiplier(0));
            Assert.AreEqual(51f / 75f, MagicCircleRules.DamageMultiplier(51), 1e-5);
            Assert.AreEqual(1f, MagicCircleRules.DamageMultiplier(75), 1e-5);
            Assert.AreEqual(4f / 3f, MagicCircleRules.DamageMultiplier(100), 1e-5);
            Assert.AreEqual("실패", MagicCircleRules.Verdict(50));
            Assert.AreEqual("약하게 발동", MagicCircleRules.Verdict(60));
            Assert.AreEqual("발동!", MagicCircleRules.Verdict(80));
            Assert.AreEqual("완벽!", MagicCircleRules.Verdict(90));
        }

        [Test]
        public void PointAtWalksThePath()
        {
            var line = new List<CirclePoint> { new CirclePoint(0f, 0f), new CirclePoint(1f, 0f), new CirclePoint(1f, 1f) };
            Assert.AreEqual(2f, MagicCircleRules.Length(line), 1e-5);
            var mid = MagicCircleRules.PointAt(line, 1.5f);
            Assert.AreEqual(1f, mid.X, 1e-5);
            Assert.AreEqual(0.5f, mid.Y, 1e-5);
            Assert.AreEqual(1f, MagicCircleRules.PointAt(line, 9f).Y, 1e-5);
        }
    }
}
