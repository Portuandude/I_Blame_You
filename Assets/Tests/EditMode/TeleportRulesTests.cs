using System;
using System.Collections.Generic;
using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 순간이동 규칙(TeleportRules): 네 방향 고정, 위·옆 막힘, 아래 발판 뚫기와 낙사 방지.
    // 한 열(column)만 본 단순한 세계: 판(top, bottom, 발판 여부) 목록과 키 1.8의 몸.
    public class TeleportRulesTests
    {
        private const float BodyHeight = 1.8f;
        private const float Distance = 4f;
        private const float KillY = -3f;

        private class Slab
        {
            public float Top, Bottom;
            public bool OneWay;
            public Slab(float top, float bottom, bool oneWay) { Top = top; Bottom = bottom; OneWay = oneWay; }
        }

        private static Slab Ground(float top) => new Slab(top, top - 5f, false);
        private static Slab Platform(float top) => new Slab(top, top - 0.25f, true);

        private static float PlanDown(float feetY, params Slab[] slabs)
        {
            float solidHit = float.PositiveInfinity;
            foreach (var s in slabs)
                if (!s.OneWay && s.Top <= feetY + 1e-4f) solidHit = Math.Min(solidHit, feetY - s.Top);

            Func<float, bool> overlaps = t =>
            {
                float lo = feetY - t, hi = lo + BodyHeight;
                foreach (var s in slabs)
                    if (lo < s.Top - 1e-4f && hi > s.Bottom + 1e-4f) return true;
                return false;
            };
            Func<float, float> drop = t =>
            {
                float feet = feetY - t;
                float best = float.PositiveInfinity;
                foreach (var s in slabs)
                    if (s.Top <= feet + 1e-4f) best = Math.Min(best, Math.Max(feet - s.Top, 0f));
                return best <= TeleportRules.MaxSafeDrop ? best : float.PositiveInfinity;
            };

            return TeleportRules.PlanDown(Distance, solidHit, feetY, KillY, overlaps, drop);
        }

        [Test]
        public void CardinalPicksLargestAxisOnly()
        {
            int dx, dy;
            TeleportRules.Cardinal(0f, 0f, -1, out dx, out dy);
            Assert.AreEqual(-1, dx); Assert.AreEqual(0, dy);
            TeleportRules.Cardinal(0.707f, 0.707f, 1, out dx, out dy);
            Assert.AreEqual(0, dx); Assert.AreEqual(1, dy);
            TeleportRules.Cardinal(0.6f, -0.8f, 1, out dx, out dy);
            Assert.AreEqual(0, dx); Assert.AreEqual(-1, dy);
            TeleportRules.Cardinal(-1f, 0.2f, 1, out dx, out dy);
            Assert.AreEqual(-1, dx); Assert.AreEqual(0, dy);
        }

        [Test]
        public void SideAndUpStopBeforeBlockingSurface()
        {
            Assert.AreEqual(Distance, TeleportRules.Blocked(Distance, float.PositiveInfinity), 1e-4f);
            Assert.AreEqual(2f - TeleportRules.Skin, TeleportRules.Blocked(Distance, 2f), 1e-4f);
            Assert.AreEqual(0f, TeleportRules.Blocked(Distance, 0.01f), 1e-4f);
        }

        [Test]
        public void DownPassesThroughPlatformAndLandsOnGround()
        {
            float t = PlanDown(3f, Platform(3f), Ground(0f));
            Assert.AreEqual(3f - TeleportRules.Skin, t, 1e-4f);
        }

        [Test]
        public void DownStopsOnLowerPlatformInsteadOfFallingFar()
        {
            // 위 발판(10) → 아래 발판(7), 땅은 한참 아래(0): 4칸을 다 가면 발 밑 6칸이 비어 있으므로 아래 발판 위에 선다
            float t = PlanDown(10f, Platform(10f), Platform(7f), Ground(0f));
            Assert.AreEqual(3f, t, 1e-4f);
        }

        [Test]
        public void DownIntoBottomlessDropIsRefused()
        {
            Assert.Less(PlanDown(10f, Platform(10f), Ground(0f)), 0f);
            Assert.Less(PlanDown(5f, Platform(5f)), 0f);
        }

        [Test]
        public void DownOntoGroundBelowKillHeightIsRefused()
        {
            // 디딜 곳은 있지만 낙사 높이(-3) 아래라 떨어지는 순간 복귀(체력 손실)가 일어난다
            Assert.Less(PlanDown(1f, Platform(1f), Ground(-3.5f)), 0f);
        }

        [Test]
        public void DownOnSolidGroundDoesNothing()
        {
            Assert.Less(PlanDown(0f, Ground(0f)), 0f);
        }
    }
}
