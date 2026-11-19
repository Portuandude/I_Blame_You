using IBlameYou.Net;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 친구가 판 도중 끊기면 같은 호스트에 저절로 다시 붙기 (Net/Core/ReconnectPlan).
    public class NetReconnectTests
    {
        [Test]
        public void WithoutAcceptedHost_LossDoesNotRetry()
        {
            var plan = new ReconnectPlan();
            Assert.IsFalse(plan.Lost(0f), "처음 참가에 실패한 주소로는 다시 하지 않는다");
            Assert.IsFalse(plan.InProgress);
            Assert.IsFalse(plan.Due(100f));
        }

        [Test]
        public void LossSchedulesAttemptsWithGrowingDelay()
        {
            var plan = new ReconnectPlan();
            plan.Remember("10.0.0.2", 27777);
            Assert.IsFalse(plan.InProgress, "연결돼 있는 동안은 다시 붙는 중이 아니다");

            Assert.IsTrue(plan.Lost(10f));
            Assert.IsTrue(plan.InProgress);
            Assert.AreEqual(1, plan.Attempt);
            Assert.IsFalse(plan.Due(10f + ReconnectPlan.Delays[0] - 0.01f));
            Assert.IsTrue(plan.Due(10f + ReconnectPlan.Delays[0]));
            Assert.IsFalse(plan.Due(50f), "한 번 시작한 시도는 다시 Due가 아니다");
            Assert.AreEqual("10.0.0.2", plan.Address);
            Assert.AreEqual(27777, plan.Port);

            Assert.IsTrue(plan.Lost(20f), "시도가 실패하면 다음 시도");
            Assert.AreEqual(2, plan.Attempt);
            Assert.AreEqual(ReconnectPlan.Delays[1], plan.SecondsLeft(20f), 0.001f);
        }

        [Test]
        public void TotalWaitOutlastsHostDeadPeerTimeout()
        {
            float total = 0f;
            foreach (float d in ReconnectPlan.Delays) total += d;
            Assert.Greater(total, PingTracker.Timeout + 5f, "호스트가 죽은 연결을 비운 뒤에도 시도가 남아야 한다");
        }

        [Test]
        public void GivesUpAfterMaxAttempts_AndForgets()
        {
            var plan = new ReconnectPlan();
            plan.Remember("host", 1);
            float now = 0f;
            for (int i = 0; i < ReconnectPlan.MaxAttempts; i++)
            {
                Assert.IsTrue(plan.Lost(now));
                now += 10f;
                Assert.IsTrue(plan.Due(now));
            }
            Assert.IsFalse(plan.Lost(now), "다 쓰면 그만");
            Assert.IsFalse(plan.HasTarget);
            Assert.IsFalse(plan.InProgress);
        }

        [Test]
        public void SuccessResetsAttempts()
        {
            var plan = new ReconnectPlan();
            plan.Remember("host", 1);
            plan.Lost(0f);
            plan.Lost(5f);
            plan.Remember("host", 1);
            Assert.AreEqual(0, plan.Attempt);
            Assert.IsFalse(plan.Waiting);
            Assert.IsTrue(plan.Lost(30f));
            Assert.AreEqual(1, plan.Attempt, "다시 붙은 뒤 또 끊기면 처음부터 센다");
        }

        [Test]
        public void ForgetStopsWaiting()
        {
            var plan = new ReconnectPlan();
            plan.Remember("host", 1);
            plan.Lost(0f);
            plan.Forget();
            Assert.IsFalse(plan.Due(100f));
            Assert.IsFalse(plan.InProgress);
        }
    }
}
