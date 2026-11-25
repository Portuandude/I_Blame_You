using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 식당 "든든한 도시락" (Data/VillageFood.cs): 산 뒤 다음 전투 구역을 돌파할 때까지 받는 피해를 줄인다.
    // 돌파 횟수(RunStats.ArenasCleared)가 산 때보다 늘면 꺼진다. 다시 사면 그때부터 다시 센다. 판이 바뀌면 플레이어와 함께 없어진다.
    public class LunchboxGuard : MonoBehaviour, IDamageModifier
    {
        private float reduction;
        private int clearedAtStart = -1;

        public bool Active => clearedAtStart >= 0 && RunStats.ArenasCleared <= clearedAtStart;

        public static void Grant(GameObject player, float reduction)
        {
            if (!(reduction > 0f)) return; // Clamp01(NaN) = NaN: 피해 감소가 NaN이면 TakeDamage가 피해를 통째로 버린다
            var guard = player.GetComponent<LunchboxGuard>();
            if (guard == null) guard = player.AddComponent<LunchboxGuard>();
            guard.reduction = Mathf.Clamp01(reduction);
            guard.clearedAtStart = RunStats.ArenasCleared;
        }

        // 스테이지를 넘을 때(새 플레이어) 저장한 값 그대로 다시 붙인다 (RunSave, 버그 점검 2026-11-25: 마을에서 사고 구역을 돌파하기 전에
        // 쉼터로 넘어가면 효과가 사라지던 것). 돌파 횟수는 판 기록(RunStats.ArenasCleared)이 이어지므로 산 때 값을 그대로 쓴다.
        public static void Restore(GameObject player, float reduction, int clearedAtStart)
        {
            if (!(reduction > 0f) || clearedAtStart < 0) return;
            Grant(player, reduction);
            player.GetComponent<LunchboxGuard>().clearedAtStart = clearedAtStart;
        }

        public float SavedReduction => Active ? reduction : 0f;
        public int SavedClearedAt => Active ? clearedAtStart : -1;

        public float Modify(float damage, DamageKind kind, Element element) =>
            Active ? damage * (1f - reduction) : damage;
    }
}
