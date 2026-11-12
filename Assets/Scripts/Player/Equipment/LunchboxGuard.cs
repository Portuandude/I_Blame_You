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
            var guard = player.GetComponent<LunchboxGuard>();
            if (guard == null) guard = player.AddComponent<LunchboxGuard>();
            guard.reduction = Mathf.Clamp01(reduction);
            guard.clearedAtStart = RunStats.ArenasCleared;
        }

        public float Modify(float damage, DamageKind kind, Element element) =>
            Active ? damage * (1f - reduction) : damage;
    }
}
