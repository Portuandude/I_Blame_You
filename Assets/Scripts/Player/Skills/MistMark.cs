using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 물안개 안에 있는 적 표시. 여러 안개가 겹쳐도 한 번만 적용한다 (count).
    public class MistMark : MonoBehaviour, IDamageModifier
    {
        private int count;
        private CombatStats stats;

        public void Enter()
        {
            if (count++ > 0) return;
            if (stats == null) stats = GetComponent<CombatStats>();
            if (stats != null) stats.DamageMultiplier *= MistZone.DealtMultiplier;
        }

        public void Exit()
        {
            if (count <= 0) return;
            if (--count > 0) return;
            if (stats != null) stats.DamageMultiplier /= MistZone.DealtMultiplier;
        }

        public float Modify(float damage, DamageKind kind, Element element) => count > 0 ? damage * MistZone.TakenMultiplier : damage;
    }
}
