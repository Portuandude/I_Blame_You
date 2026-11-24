using System;

namespace IBlameYou.Systems
{
    // 저장 슬롯 1~99 (2026-11-24 사람 결정). 슬롯 1은 예전 저장 키 그대로라 지금까지의 저장이 슬롯 1에 보인다.
    // 슬롯 2+는 키 앞에 "SlotNN."를 붙인다. 슬롯마다 따로인 것: 런 저장(쉼터)·숨은 능력치. 설정·기록·도시(영혼석·탑·마법)는 공용.
    public static class SaveSlotRule
    {
        public const int Min = 1;
        public const int Max = 99;

        public static int Clamp(int slot) => Math.Max(Min, Math.Min(Max, slot));

        // 한 칸 옮기기, 끝에서 감아 돈다 (99 → 1, 1 → 99)
        public static int Step(int slot, int delta)
        {
            int count = Max - Min + 1;
            int i = ((Clamp(slot) - Min + delta) % count + count) % count;
            return Min + i;
        }

        public static string Key(string baseKey, int slot)
        {
            slot = Clamp(slot);
            return slot == Min ? baseKey : $"Slot{slot:00}.{baseKey}";
        }

        public static string Label(int slot) => $"슬롯 {Clamp(slot):00}";
    }
}
