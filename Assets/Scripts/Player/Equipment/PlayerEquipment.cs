using System;
using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 이번 판에 낀 장비 (A/B 공용 한 벌: 로브·지팡이·신발 칸마다 하나, Docs/EquipmentDesign.md 2절).
    // 유물은 얻을 때 한 번 더하고 끝이지만 장비는 바꿔 낄 수 있어서, 칸마다 "지금 더해 준 값"을 기억했다가
    // 바꿀 때 옛 값을 빼고 새 값을 더한다 (유물·특성이 더한 값과 섞이지 않는다). 판이 끝나면 플레이어와 함께 사라진다.
    public class PlayerEquipment : MonoBehaviour
    {
        private readonly Dictionary<EquipSlot, EquipmentItem> equipped = new Dictionary<EquipSlot, EquipmentItem>();
        private readonly Dictionary<EquipSlot, List<StatValue>> applied = new Dictionary<EquipSlot, List<StatValue>>();

        // 칸이 바뀌면 (칸, 새 장비 또는 null)
        public event Action<EquipSlot, EquipmentItem> Changed;

        public EquipmentItem Get(EquipSlot slot) => equipped.TryGetValue(slot, out var item) ? item : null;

        public IEnumerable<EquipmentItem> All
        {
            get
            {
                foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
                    if (equipped.TryGetValue(slot, out var item)) yield return item;
            }
        }

        // 장비를 그 칸에 낀다. 반환: 빠진 옛 장비 (없으면 null).
        public EquipmentItem Equip(EquipmentItem item)
        {
            if (item == null) return null;
            var old = Remove(item.Slot, false);
            equipped[item.Slot] = item;
            var stats = item.Stats();
            Apply(stats, 1f);
            applied[item.Slot] = stats;
            Changed?.Invoke(item.Slot, item);
            return old;
        }

        // 칸을 비운다. 반환: 빠진 장비 (없으면 null).
        public EquipmentItem Unequip(EquipSlot slot) => Remove(slot, true);

        // 낀 장비의 수식어가 바뀌었을 때 (상점 리롤) 능력치를 다시 맞춘다.
        public void Refresh(EquipSlot slot)
        {
            if (!equipped.TryGetValue(slot, out var item)) return;
            if (applied.TryGetValue(slot, out var old)) Apply(old, -1f);
            var stats = item.Stats();
            Apply(stats, 1f);
            applied[slot] = stats;
            Changed?.Invoke(slot, item);
        }

        private EquipmentItem Remove(EquipSlot slot, bool notify)
        {
            if (!equipped.TryGetValue(slot, out var old)) return null;
            if (applied.TryGetValue(slot, out var stats)) Apply(stats, -1f);
            equipped.Remove(slot);
            applied.Remove(slot);
            if (notify) Changed?.Invoke(slot, null);
            return old;
        }

        private void Apply(List<StatValue> stats, float sign)
        {
            var combat = GetComponent<CombatStats>();
            var health = GetComponent<HealthSystem>();
            var mana = GetComponent<ManaSystem>();
            var stamina = GetComponent<StaminaSystem>();
            foreach (var s in stats)
            {
                float v = s.Value * sign;
                int element = EquipmentBook.ElementIndex(s.Stat);
                switch (s.Stat)
                {
                    case EquipStat.Defense:
                        if (combat != null) combat.Stats.defense += v;
                        break;
                    case EquipStat.MaxHealth:
                        if (health != null) health.AddMax(v, false); // 끼고 빼기로 회복되지 않게 현재 체력은 그대로 (최대치로만 자름)
                        break;
                    case EquipStat.PhysicalDamage:
                        if (combat != null) combat.Stats.physicalDamage += v;
                        break;
                    case EquipStat.CritChance:
                        if (combat != null) combat.Stats.critChance += v; // 빼기가 정확하도록 자르지 않는다 (확률 1 넘으면 늘 치명타)
                        break;
                    case EquipStat.MaxMana:
                        if (mana != null) mana.AddMax(v, false);
                        break;
                    case EquipStat.MoveSpeed:
                        if (combat != null) combat.MoveSpeedMultiplier += v;
                        break;
                    case EquipStat.MaxStamina:
                        if (stamina != null) stamina.AddMax(v, false);
                        break;
                    default:
                        if (combat == null || element < 0) break;
                        if (s.Stat <= EquipStat.EarthDamage) combat.Stats.elementDamage[element] += v;
                        else combat.Stats.elementDefense[element] += v;
                        break;
                }
            }
        }

        // 플레이어 프리팹이 이 컴포넌트가 생기기 전에 구워졌어도 동작하도록, 없으면 붙여서 돌려준다.
        public static PlayerEquipment For(GameObject player)
        {
            if (player == null) return null;
            var equipment = player.GetComponent<PlayerEquipment>();
            return equipment != null ? equipment : player.AddComponent<PlayerEquipment>();
        }
    }
}
