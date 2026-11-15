using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // P2 마법 고르기 2단계 다이얼 (SpellDialRules / SpellDialState): 칸 방향, 휠, 커서·휠 우선순위, 마법 다이얼 열기·돌아가기, 떼기.
    public class SpellDialRulesTests
    {
        private const float CX = 500f, CY = 300f;

        private static SpellDialState Open(int element, int[] counts, int[] remembered = null, float time = 0f)
        {
            var dial = new SpellDialState();
            dial.Open(CX, CY, element, counts, remembered ?? new int[4], time);
            return dial;
        }

        [Test]
        public void SlotFromOffset_FourDirections_TopRightBottomLeft()
        {
            Assert.AreEqual(0, SpellDialRules.SlotFromOffset(0f, 60f, 4));   // 위 = 불
            Assert.AreEqual(1, SpellDialRules.SlotFromOffset(60f, 0f, 4));   // 오른쪽 = 물
            Assert.AreEqual(2, SpellDialRules.SlotFromOffset(0f, -60f, 4));  // 아래 = 바람
            Assert.AreEqual(3, SpellDialRules.SlotFromOffset(-60f, 0f, 4));  // 왼쪽 = 땅
            Assert.AreEqual(0, SpellDialRules.SlotFromOffset(-30f, 60f, 4)); // 위쪽 왼편도 위
            Assert.AreEqual(-1, SpellDialRules.SlotFromOffset(5f, 5f, 4));   // 데드존
        }

        [Test]
        public void SlotOffset_RoundTripsThroughSlotFromOffset()
        {
            for (int count = 1; count <= 6; count++)
                for (int slot = 0; slot < count; slot++)
                {
                    SpellDialRules.SlotOffset(slot, count, SpellDialRules.Radius, out float x, out float y);
                    Assert.AreEqual(slot, SpellDialRules.SlotFromOffset(x, y, count), $"count {count} slot {slot}");
                }
        }

        [Test]
        public void Step_WheelWraps()
        {
            Assert.AreEqual(1, SpellDialRules.Step(0, 4, 1f));
            Assert.AreEqual(3, SpellDialRules.Step(0, 4, -1f));
            Assert.AreEqual(0, SpellDialRules.Step(3, 4, 0.1f));
            Assert.AreEqual(2, SpellDialRules.Step(2, 4, 0f));
        }

        [Test]
        public void QuickTap_ReleasesCurrentElementAndRememberedSlot()
        {
            var dial = Open(2, new[] { 1, 1, 3, 1 }, new[] { 0, 0, 2, 0 });
            dial.Update(CX, CY, 0f, 0.05f);
            Assert.IsTrue(dial.Release(out int e, out int s));
            Assert.AreEqual(2, e);
            Assert.AreEqual(2, s);
            Assert.IsFalse(dial.IsOpen);
        }

        [Test]
        public void Cursor_PicksElement_SingleSpellNeverOpensSpellDial()
        {
            var dial = Open(0, new[] { 1, 1, 1, 1 });
            dial.Update(CX + 60f, CY, 0f, 0.1f);
            dial.Update(CX + 200f, CY, 0f, 2f); // 멀리 밀고 오래 있어도 마법이 하나면 1단계 그대로
            Assert.AreEqual(SpellDialStage.Element, dial.Stage);
            Assert.IsTrue(dial.Release(out int e, out int s));
            Assert.AreEqual(1, e);
            Assert.AreEqual(0, s);
        }

        [Test]
        public void Wheel_WinsWhileCursorStaysOnSameSlot()
        {
            var dial = Open(0, new[] { 1, 1, 1, 1 });
            dial.Update(CX + 60f, CY, 0f, 0.1f); // 커서로 물
            dial.Update(CX + 60f, CY, 1f, 0.2f); // 휠 위로 → 바람
            dial.Update(CX + 61f, CY, 0f, 0.3f); // 커서가 같은 칸 안에서 조금 움직여도 휠 선택 유지
            Assert.AreEqual(2, dial.ElementSlot);
            dial.Update(CX, CY + 60f, 0f, 0.4f); // 커서가 다른 칸으로 가면 커서가 이긴다
            Assert.AreEqual(0, dial.ElementSlot);
        }

        [Test]
        public void Dwell_OpensSpellDial_AtElementSlot()
        {
            var dial = Open(0, new[] { 1, 3, 1, 1 });
            dial.Update(CX + 60f, CY, 0f, 0.1f);
            Assert.AreEqual(SpellDialStage.Element, dial.Stage);
            dial.Update(CX + 60f, CY, 0f, 0.1f + SpellDialRules.DwellSeconds + 0.01f);
            Assert.AreEqual(SpellDialStage.Spell, dial.Stage);
            Assert.AreEqual(CX + SpellDialRules.Radius, dial.CenterX, 0.01f);
            Assert.AreEqual(CY, dial.CenterY, 0.01f);

            // 마법 다이얼 가운데에서 오른쪽 아래 → 3칸 중 둘째(오른쪽 아래)
            SpellDialRules.SlotOffset(1, 3, SpellDialRules.Radius, out float ox, out float oy);
            dial.Update(dial.CenterX + ox, dial.CenterY + oy, 0f, 1f);
            Assert.IsTrue(dial.Release(out int e, out int s));
            Assert.AreEqual(1, e);
            Assert.AreEqual(1, s);
        }

        [Test]
        public void HoldingStillOnDefault_DoesNotOpenSpellDial()
        {
            var dial = Open(1, new[] { 1, 3, 1, 1 });
            dial.Update(CX, CY, 0f, 5f);
            Assert.AreEqual(SpellDialStage.Element, dial.Stage);
        }

        [Test]
        public void Flick_OpensSpellDialImmediately()
        {
            var dial = Open(0, new[] { 2, 1, 1, 1 });
            dial.Update(CX, CY + SpellDialRules.FlickRadius + 5f, 0f, 0.02f);
            Assert.AreEqual(SpellDialStage.Spell, dial.Stage);
            Assert.AreEqual(0, dial.ElementSlot);
        }

        [Test]
        public void SpellDial_WheelCycles_AndCenterReturnGoesBack()
        {
            var dial = Open(3, new[] { 1, 1, 1, 2 });
            dial.Update(CX - 120f, CY, 0f, 0.02f); // 왼쪽(땅)으로 휙
            Assert.AreEqual(SpellDialStage.Spell, dial.Stage);
            int before = dial.SpellSlot;
            dial.Update(dial.CenterX, dial.CenterY, 1f, 0.1f);
            Assert.AreEqual((before + 1) % 2, dial.SpellSlot);

            dial.Update(CX, CY, 0f, 0.2f); // 속성 다이얼 가운데로 되돌리면 1단계
            Assert.AreEqual(SpellDialStage.Element, dial.Stage);
            Assert.AreEqual(CX, dial.CenterX);
        }

        [Test]
        public void Cancel_And_EmptyElement_ReleaseFalse()
        {
            var dial = Open(0, new[] { 1, 0, 1, 1 });
            dial.Cancel();
            Assert.IsFalse(dial.IsOpen);
            Assert.IsFalse(dial.Release(out _, out _));

            dial = Open(0, new[] { 1, 0, 1, 1 });
            dial.Update(CX + 60f, CY, 0f, 0.1f);
            Assert.IsFalse(dial.Release(out _, out _));
        }
    }
}
