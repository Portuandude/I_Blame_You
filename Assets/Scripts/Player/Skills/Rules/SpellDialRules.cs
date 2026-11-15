using System;

namespace IBlameYou.Player
{
    // P2 마법 고르기 2단계 원형 다이얼 (2026-11-15 사람 결정: "속성 다이얼 - 마법 다이얼 순서로 펼쳐지게").
    // 순수 C#(EditMode 테스트 대상). 화면 좌표는 Unity Input.mousePosition과 같이 위가 +y인 픽셀.
    //
    // 흐름: 우클릭을 누르면 커서 자리에 속성 다이얼(불·물·바람·땅 4칸, 위·오른쪽·아래·왼쪽)이 열린다.
    //   커서를 칸 쪽으로 옮기거나 휠을 굴려 속성을 고른다 (처음에는 지금 고른 속성 = 휠·숫자줄과 같은 상태).
    //   배운 마법이 둘 이상인 속성은 그 칸에 DwellSeconds 머무르거나 커서를 FlickRadius 밖으로 밀면
    //   그 칸 자리에서 마법 다이얼이 열린다. 마법이 하나뿐이면 마법 다이얼 없이 그 마법이다.
    //   우클릭을 떼면 고른 마법의 영창이 열린다. 좌클릭은 취소.
    // 커서·휠 중 나중에 바뀐 쪽이 이긴다: 커서가 가리키는 칸이 "바뀔 때만" 고른 칸을 바꾸므로, 휠로 바꾼 뒤 커서를 가만히 두면 휠 선택이 남는다.
    public static class SpellDialRules
    {
        public const int ElementCount = 4;     // 불·물·바람·땅 (무속성은 평타라 다이얼에 없음)
        public const float DeadZone = 22f;     // 가운데 이 반지름 안이면 커서로 칸을 가리키지 않는다
        public const float Radius = 64f;       // 칸 중심까지의 거리 (그리기에도 씀)
        public const float FlickRadius = 100f; // 이보다 멀리 밀면 머무르지 않아도 바로 마법 다이얼
        public const float DwellSeconds = 0.3f;

        // 칸 i의 방향: 위에서 시작해 시계 방향으로 같은 간격. 가운데에서 칸 중심까지의 오프셋.
        public static void SlotOffset(int slot, int count, float radius, out float x, out float y)
        {
            double angle = Math.PI / 2 - slot * 2 * Math.PI / Math.Max(1, count);
            x = (float)(Math.Cos(angle) * radius);
            y = (float)(Math.Sin(angle) * radius);
        }

        // 가운데에서의 오프셋이 가리키는 칸. 데드존 안이면 -1.
        public static int SlotFromOffset(float dx, float dy, int count)
        {
            if (count <= 0 || dx * dx + dy * dy < DeadZone * DeadZone) return -1;
            double angle = Math.Atan2(dy, dx);                    // 오른쪽 0, 반시계 +
            double fromTop = Math.PI / 2 - angle;                  // 위 0, 시계 +
            double step = 2 * Math.PI / count;
            int slot = (int)Math.Floor((fromTop + step / 2) / step);
            return ((slot % count) + count) % count;
        }

        // 휠 한 칸: 위(+) = 다음 칸, 아래(-) = 이전 칸. 한 프레임에 한 칸만.
        public static int Step(int current, int count, float wheel)
        {
            if (count <= 0) return 0;
            if (wheel == 0f) return current;
            return (current + (wheel > 0f ? 1 : count - 1)) % count;
        }
    }

    public enum SpellDialStage { Closed, Element, Spell }

    public sealed class SpellDialState
    {
        public SpellDialStage Stage { get; private set; } = SpellDialStage.Closed;
        public bool IsOpen => Stage != SpellDialStage.Closed;
        public float CenterX { get; private set; }  // 지금 펼쳐진 다이얼의 가운데 (마법 다이얼이면 고른 속성 칸 자리)
        public float CenterY { get; private set; }
        public float ElementCenterX { get; private set; } // 속성 다이얼의 가운데 (마법 다이얼이 열려도 그대로)
        public float ElementCenterY { get; private set; }
        public int ElementSlot { get; private set; }
        public int SpellSlot { get; private set; }

        private int[] spellCounts = new int[SpellDialRules.ElementCount];
        private int[] remembered = new int[SpellDialRules.ElementCount];
        private int lastCursorSlot = -1;
        private bool chosen;          // 커서나 휠로 속성을 직접 골랐는지 (가만히 누르고만 있으면 마법 다이얼을 열지 않는다)
        private float highlightSince;

        // spellCounts[e] = 속성 e의 배운 마법 수, rememberedSlots[e] = 그 속성에서 마지막으로 고른 마법 칸.
        public void Open(float mouseX, float mouseY, int element, int[] spellCounts, int[] rememberedSlots, float time)
        {
            Stage = SpellDialStage.Element;
            CenterX = ElementCenterX = mouseX;
            CenterY = ElementCenterY = mouseY;
            this.spellCounts = spellCounts;
            remembered = rememberedSlots;
            ElementSlot = Clamp(element, SpellDialRules.ElementCount);
            SpellSlot = Clamp(remembered[ElementSlot], Count(ElementSlot));
            lastCursorSlot = -1;
            chosen = false;
            highlightSince = time;
        }

        public void Update(float mouseX, float mouseY, float wheel, float time)
        {
            if (Stage == SpellDialStage.Closed) return;
            float dx = mouseX - CenterX, dy = mouseY - CenterY;

            if (Stage == SpellDialStage.Element)
            {
                int cursor = SpellDialRules.SlotFromOffset(dx, dy, SpellDialRules.ElementCount);
                int before = ElementSlot;
                if (cursor >= 0 && cursor != lastCursorSlot) ElementSlot = cursor;
                lastCursorSlot = cursor;
                if (wheel != 0f) ElementSlot = SpellDialRules.Step(ElementSlot, SpellDialRules.ElementCount, wheel);
                if (cursor >= 0 || wheel != 0f) chosen = true;
                if (ElementSlot != before)
                {
                    highlightSince = time;
                    SpellSlot = Clamp(remembered[ElementSlot], Count(ElementSlot));
                }

                bool flick = cursor == ElementSlot && dx * dx + dy * dy >= SpellDialRules.FlickRadius * SpellDialRules.FlickRadius;
                bool dwell = chosen && time - highlightSince >= SpellDialRules.DwellSeconds;
                if (Count(ElementSlot) > 1 && (flick || dwell)) OpenSpellDial();
                return;
            }

            // 마법 다이얼에서 커서를 속성 다이얼 가운데로 되돌리면 속성 다이얼로 돌아간다 (속성을 다시 고르기)
            float ex = mouseX - ElementCenterX, ey = mouseY - ElementCenterY;
            if (ex * ex + ey * ey < SpellDialRules.DeadZone * SpellDialRules.DeadZone)
            {
                Stage = SpellDialStage.Element;
                CenterX = ElementCenterX;
                CenterY = ElementCenterY;
                lastCursorSlot = -1;
                chosen = false;
                highlightSince = time;
                return;
            }

            int count = Count(ElementSlot);
            int spellCursor = SpellDialRules.SlotFromOffset(dx, dy, count);
            if (spellCursor >= 0 && spellCursor != lastCursorSlot) SpellSlot = spellCursor;
            lastCursorSlot = spellCursor;
            if (wheel != 0f) SpellSlot = SpellDialRules.Step(SpellSlot, count, wheel);
        }

        // 우클릭을 뗐을 때: 고른 속성·마법을 돌려주고 닫는다. 그 속성에 마법이 없으면 false.
        public bool Release(out int element, out int spellSlot)
        {
            element = ElementSlot;
            spellSlot = SpellSlot;
            bool ok = Stage != SpellDialStage.Closed && Count(ElementSlot) > 0;
            Stage = SpellDialStage.Closed;
            return ok;
        }

        public void Cancel() => Stage = SpellDialStage.Closed;

        private void OpenSpellDial()
        {
            SpellDialRules.SlotOffset(ElementSlot, SpellDialRules.ElementCount, SpellDialRules.Radius, out float ox, out float oy);
            CenterX = ElementCenterX + ox;
            CenterY = ElementCenterY + oy;
            Stage = SpellDialStage.Spell;
            lastCursorSlot = -1;
        }

        public int Count(int element) => element >= 0 && element < spellCounts.Length ? spellCounts[element] : 0;

        private static int Clamp(int value, int count) => count <= 0 ? 0 : Math.Max(0, Math.Min(count - 1, value));
    }
}
