using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 동전 소리가 겹치면 겹친 수에 비례해 작게 (2026-11-24 사람 결정 D4).
    public class CoinSoundRuleTests
    {
        [Test]
        public void VolumeShrinksWithOverlapButNotToSilence()
        {
            Assert.AreEqual(0.6f, CoinSoundRule.Volume(0.6f, 1), 1e-6f);
            Assert.AreEqual(0.6f, CoinSoundRule.Volume(0.6f, 0), 1e-6f, "0 이하는 하나로 친다");
            Assert.AreEqual(0.3f, CoinSoundRule.Volume(0.6f, 2), 1e-6f);
            Assert.AreEqual(0.15f, CoinSoundRule.Volume(0.6f, 4), 1e-6f);
            Assert.AreEqual(0.6f * CoinSoundRule.MinRatio, CoinSoundRule.Volume(0.6f, 100), 1e-6f, "아주 많아도 최소 크기는 남긴다");
            for (int n = 1; n < 20; n++)
                Assert.LessOrEqual(CoinSoundRule.Volume(1f, n + 1), CoinSoundRule.Volume(1f, n));
        }
    }
}
