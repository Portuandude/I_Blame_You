using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 화면 설정 규칙: 16:9 해상도 목록·모니터 크기 거르기·레터박스 영역.
    public class DisplayRulesTests
    {
        [Test]
        public void AllResolutionsAre16By9()
        {
            foreach (var r in DisplayRules.Resolutions)
                Assert.AreEqual(DisplayRules.Aspect, (float)r.width / r.height, 0.01f, $"{r.width}x{r.height}");
        }

        [Test]
        public void AvailableFiltersByMonitor()
        {
            var list = DisplayRules.Available(1920, 1080);
            Assert.AreEqual(4, list.Count); // 1280, 1366, 1600, 1920
            Assert.AreEqual(1, DisplayRules.Available(800, 600).Count, "작은 모니터라도 하나는 남긴다");
        }

        [Test]
        public void SanitizePicksLargestFittingWhenMissingOrTooBig()
        {
            Assert.AreEqual(3, DisplayRules.Sanitize(-1, 1920, 1080));
            Assert.AreEqual(3, DisplayRules.Sanitize(6, 1920, 1080));
            Assert.AreEqual(1, DisplayRules.Sanitize(1, 1920, 1080));
            Assert.AreEqual(DisplayRules.Borderless, DisplayRules.SanitizeMode(7));
            Assert.AreEqual(DisplayRules.Windowed, DisplayRules.SanitizeMode(0));
        }

        [Test]
        public void LetterboxKeeps16By9()
        {
            Assert.AreEqual((0f, 0f, 1f, 1f), DisplayRules.Letterbox(1920, 1080));
            var wide = DisplayRules.Letterbox(2560, 1080); // 21:9 → 좌우 띠
            Assert.AreEqual(0f, wide.y, 1e-4f);
            Assert.AreEqual(DisplayRules.Aspect, wide.width * 2560f / (wide.height * 1080f), 0.01f);
            Assert.AreEqual(1f, wide.x * 2f + wide.width, 1e-4f);
            var tall = DisplayRules.Letterbox(1600, 1200); // 4:3 → 위아래 띠
            Assert.AreEqual(0f, tall.x, 1e-4f);
            Assert.AreEqual(DisplayRules.Aspect, tall.width * 1600f / (tall.height * 1200f), 0.01f);
        }
    }
}
