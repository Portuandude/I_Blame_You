using System;
using System.Collections.Generic;
using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 순간이동 규칙(TeleportRules): 네 방향 고정, 옆 막힘, 위 발판 뚫기·천장 멈춤, 아래 발판 뚫기와 낙사 방지.
    // 한 열(column)만 본 단순한 세계: 판(top, bottom, 발판 여부) 목록과 키 1.8의 몸.
    public class TeleportRulesTests
    {
        private const float BodyHeight = 1.8f;
        private const float Distance = 4f;
        private const float KillY = -3f;

        private class Slab
        {
            public float Top, Bottom;
            public bool OneWay;  // 발판 역할: 아래 순간이동이 뚫는다 (solidHit에 안 잡힘)
            public bool Unsafe;  // 움직이는·스위치·무너지는 발판: 뚫고, 그 위에는 내려서지 않는다
            public Slab(float top, float bottom, bool oneWay, bool unsafeGround = false) { Top = top; Bottom = bottom; OneWay = oneWay; Unsafe = unsafeGround; }
        }

        private static Slab Ground(float top) => new Slab(top, top - 5f, false);
        private static Slab Platform(float top) => new Slab(top, top - 0.25f, true);
        // TeleportSkill.IsPlatform: UnsafeGround가 붙은 발판도 effector 없이 뚫는다 (2026-11-12 플레이테스트: 움직이는 발판에 막힘)
        private static Slab MovingPlatform(float top) => new Slab(top, top - 0.4f, true, true);

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
                bool bestUnsafe = false;
                foreach (var s in slabs)
                {
                    if (s.Top > feet + 1e-4f) continue;
                    float d = Math.Max(feet - s.Top, 0f);
                    if (d < best) { best = d; bestUnsafe = s.Unsafe; }
                }
                if (bestUnsafe) return float.PositiveInfinity; // 처음 닿는 곳이 불안한 발판이면 안전하지 않다 (FeetCast)
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

        // 위: 발이 feetY인 몸을 올린다. solidHit는 머리 위 땅(발판 제외)의 아랫면까지 (TeleportSkill.HeadCast).
        private static float PlanUp(float feetY, params Slab[] slabs)
        {
            float head = feetY + BodyHeight;
            float solidHit = float.PositiveInfinity;
            foreach (var s in slabs)
                if (!s.OneWay && s.Bottom >= head - 1e-4f) solidHit = Math.Min(solidHit, s.Bottom - head);

            Func<float, bool> overlaps = t =>
            {
                float lo = feetY + t, hi = lo + BodyHeight;
                foreach (var s in slabs)
                    if (lo < s.Top - 1e-4f && hi > s.Bottom + 1e-4f) return true;
                return false;
            };
            return TeleportRules.PlanUp(Distance, solidHit, overlaps);
        }

        [Test]
        public void UpPassesThroughPlatformAboveAndLandsOnIt()
        {
            // 발판(윗면 3) 바로 아래 바닥(0)에 서서 위로: 발판을 뚫고 위로 4칸 (발 4 > 발판 윗면 3)
            float t = PlanUp(0f, Ground(0f), Platform(3f));
            Assert.AreEqual(Distance, t, 1e-4f);
            Assert.Greater(0f + t, 3f);
        }

        [Test]
        public void UpPassesThroughMovingPlatformAbove()
        {
            Assert.AreEqual(Distance, PlanUp(0f, Ground(0f), MovingPlatform(2.6f)), 1e-4f);
        }

        [Test]
        public void UpEndingInsidePlatformBacksOffToFreeSpot()
        {
            // 발판(4.5~4.25)이 도착 자리(발 4~머리 5.8)와 겹친다: 겹치지 않는 가장 먼 자리 = 머리가 발판 아랫면 밑 (발 2.25 이하)
            float t = PlanUp(0f, Ground(0f), Platform(4.5f));
            Assert.Greater(t, TeleportRules.MinTravel - 1e-4f);
            Assert.LessOrEqual(t + BodyHeight, 4.25f + 1e-4f);
        }

        [Test]
        public void UpStopsUnderSolidCeiling()
        {
            // 천장(땅) 아랫면 3.8: 머리(1.8)에서 2칸 위 → 2 - Skin
            var ceiling = new Slab(8.8f, 3.8f, false);
            Assert.AreEqual(2f - TeleportRules.Skin, PlanUp(0f, Ground(0f), ceiling), 1e-4f);
            // 천장에 머리가 붙어 있으면 실패
            Assert.Less(PlanUp(0f, Ground(0f), new Slab(6.8f, 1.85f, false)), 0f);
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
        public void DownPassesThroughMovingPlatformStoodOn()
        {
            // 움직이는(스위치) 발판 위에 서서 아래로: 발판을 뚫고 3칸 아래 땅에 선다
            float t = PlanDown(3f, MovingPlatform(3f), Ground(0f));
            Assert.AreEqual(3f - TeleportRules.Skin, t, 1e-4f);
        }

        [Test]
        public void DownPassesThroughMovingPlatformBelowAndNeverLandsOnIt()
        {
            // 한쪽 발판(6) 아래 움직이는 발판(4.5), 그 아래 땅(1.5): 4칸을 다 가면 발판 아래 공중(땅 0.5칸 위)에 선다.
            // 발판 위(1.5칸만 내려감)에는 서지 않는다
            float t = PlanDown(6f, Platform(6f), MovingPlatform(4.5f), Ground(1.5f));
            Assert.AreEqual(Distance, t, 1e-4f);
            // 땅이 없으면(낙사) 불안한 발판 위에 서지 않고 거부한다
            Assert.Less(PlanDown(6f, Platform(6f), MovingPlatform(4.5f)), 0f);
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
