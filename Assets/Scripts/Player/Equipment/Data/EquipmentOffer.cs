using System;

namespace IBlameYou.Player
{
    // 유물 상자와 함께 나오는 장비 하나 (Docs/EquipmentDesign.md 7절). 시드·청크 번호로 정해서 방을 다시 불러와도 같다.
    public static class EquipmentOffer
    {
        public const int MidBossGradeBonus = 1; // 중간보스 상자는 수식어 한 등급 위 (전설은 그대로)

        public static EquipmentItem Roll(int seed, int chunkIndex, int gradeBonus = 0)
        {
            var rng = new Random(unchecked(seed * 7919 + chunkIndex * 104729 + 17)); // 유물 후보 난수열과 따로
            var all = EquipmentBook.All;
            var def = all[rng.Next(all.Count)];
            return new EquipmentItem(def.Id, EquipmentModifier.Roll(rng, gradeBonus));
        }
    }
}
