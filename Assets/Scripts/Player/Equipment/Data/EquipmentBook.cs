using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 장비 데이터·계산 (순수 C#, Docs/EquipmentDesign.md 3~6절). 적용(PlayerEquipment)·UI는 따로.
    // 장비는 A/B 공용 한 벌: 로브(방어)·지팡이(공격)·신발(이동) 부위마다 하나씩, 판이 끝나면 사라진다.
    public enum EquipSlot
    {
        Robe,
        Staff,
        Shoes
    }

    public enum EquipmentId
    {
        ApprenticeRobe,
        MossRobe,
        MistRobe,
        FlameRobe,
        WoodStaff,
        EmberStaff,
        DropletStaff,
        WhirlStaff,
        ClothShoes,
        FeatherShoes,
        MudBoots,
        BoltShoes
    }

    // 속성 능력치는 Element 순서(불, 물, 바람, 땅)로 붙여 둔다: FireDamage + 속성 번호.
    public enum EquipStat
    {
        Defense,          // StatBlock.defense
        MaxHealth,        // HealthSystem.AddMax
        PhysicalDamage,   // StatBlock.physicalDamage
        CritChance,       // StatBlock.critChance (비율, 0.03 = 3%)
        MaxMana,          // ManaSystem.AddMax
        MoveSpeed,        // CombatStats.MoveSpeedMultiplier에 더함 (비율)
        MaxStamina,       // StaminaSystem.AddMax
        FireDamage,
        WaterDamage,
        WindDamage,
        EarthDamage,
        FireDefense,
        WaterDefense,
        WindDefense,
        EarthDefense
    }

    public struct StatValue
    {
        public EquipStat Stat;
        public float Value;

        public StatValue(EquipStat stat, float value)
        {
            Stat = stat;
            Value = value;
        }
    }

    public sealed class EquipmentDef
    {
        public EquipmentId Id;
        public EquipSlot Slot;
        public string Name;
        public int BasePrice;
        public StatValue[] Stats;
    }

    public static class EquipmentBook
    {
        private static readonly EquipmentDef[] Defs =
        {
            Def(EquipmentId.ApprenticeRobe, EquipSlot.Robe, "견습생 로브", 30, S(EquipStat.Defense, 4)),
            Def(EquipmentId.MossRobe, EquipSlot.Robe, "이끼 로브", 45, S(EquipStat.Defense, 3), S(EquipStat.MaxHealth, 10), S(EquipStat.EarthDefense, 10)),
            Def(EquipmentId.MistRobe, EquipSlot.Robe, "물안개 로브", 50, S(EquipStat.Defense, 3), S(EquipStat.WaterDefense, 12), S(EquipStat.FireDefense, 6)),
            Def(EquipmentId.FlameRobe, EquipSlot.Robe, "불꽃 술 로브", 55, S(EquipStat.Defense, 5), S(EquipStat.FireDefense, 12)),
            Def(EquipmentId.WoodStaff, EquipSlot.Staff, "나무 지팡이", 30, S(EquipStat.PhysicalDamage, 3)),
            Def(EquipmentId.EmberStaff, EquipSlot.Staff, "불씨 지팡이", 50, S(EquipStat.PhysicalDamage, 1), S(EquipStat.FireDamage, 5)),
            Def(EquipmentId.DropletStaff, EquipSlot.Staff, "물방울 지팡이", 50, S(EquipStat.MaxMana, 10), S(EquipStat.WaterDamage, 5)),
            Def(EquipmentId.WhirlStaff, EquipSlot.Staff, "회오리 지팡이", 60, S(EquipStat.CritChance, 0.03f), S(EquipStat.WindDamage, 4), S(EquipStat.EarthDamage, 2)),
            Def(EquipmentId.ClothShoes, EquipSlot.Shoes, "헝겊 신발", 30, S(EquipStat.MoveSpeed, 0.04f)),
            Def(EquipmentId.FeatherShoes, EquipSlot.Shoes, "깃털 신발", 45, S(EquipStat.MoveSpeed, 0.03f), S(EquipStat.MaxStamina, 10), S(EquipStat.WindDefense, 8)),
            Def(EquipmentId.MudBoots, EquipSlot.Shoes, "진흙 장화", 45, S(EquipStat.MaxStamina, 20), S(EquipStat.EarthDefense, 8), S(EquipStat.WaterDefense, 4)),
            Def(EquipmentId.BoltShoes, EquipSlot.Shoes, "번개 신발", 60, S(EquipStat.MoveSpeed, 0.05f), S(EquipStat.CritChance, 0.02f))
        };

        public static IReadOnlyList<EquipmentDef> All => Defs;

        public static EquipmentDef Get(EquipmentId id) => Defs[(int)id];

        public static List<EquipmentDef> OfSlot(EquipSlot slot)
        {
            var list = new List<EquipmentDef>();
            foreach (var def in Defs) if (def.Slot == slot) list.Add(def);
            return list;
        }

        // 비율 능력치 (소수 그대로 곱함, 표시는 %). 나머지는 정수로 반올림한다.
        public static bool IsPercent(EquipStat stat) =>
            stat == EquipStat.CritChance || stat == EquipStat.MoveSpeed;

        // 속성 능력치면 Element 번호(불 0, 물 1, 바람 2, 땅 3), 아니면 -1.
        public static int ElementIndex(EquipStat stat)
        {
            if (stat >= EquipStat.FireDamage && stat <= EquipStat.EarthDamage) return stat - EquipStat.FireDamage;
            if (stat >= EquipStat.FireDefense && stat <= EquipStat.EarthDefense) return stat - EquipStat.FireDefense;
            return -1;
        }

        public static string StatName(EquipStat stat)
        {
            switch (stat)
            {
                case EquipStat.Defense: return "방어력";
                case EquipStat.MaxHealth: return "최대 체력";
                case EquipStat.PhysicalDamage: return "물리 피해";
                case EquipStat.CritChance: return "치명타 확률";
                case EquipStat.MaxMana: return "최대 마나";
                case EquipStat.MoveSpeed: return "이동속도";
                case EquipStat.MaxStamina: return "기력";
                case EquipStat.FireDamage: return "불 피해";
                case EquipStat.WaterDamage: return "물 피해";
                case EquipStat.WindDamage: return "바람 피해";
                case EquipStat.EarthDamage: return "땅 피해";
                case EquipStat.FireDefense: return "불 방어";
                case EquipStat.WaterDefense: return "물 방어";
                case EquipStat.WindDefense: return "바람 방어";
                case EquipStat.EarthDefense: return "땅 방어";
                default: return stat.ToString();
            }
        }

        // "방어력 +4", "이동속도 +5%"
        public static string Describe(StatValue value)
        {
            if (IsPercent(value.Stat))
            {
                double pct = Math.Round(value.Value * 100.0, 1, MidpointRounding.AwayFromZero);
                return $"{StatName(value.Stat)} {(pct >= 0 ? "+" : "")}{pct:0.#}%";
            }

            return $"{StatName(value.Stat)} {(value.Value >= 0 ? "+" : "")}{value.Value:0}";
        }

        private static EquipmentDef Def(EquipmentId id, EquipSlot slot, string name, int price, params StatValue[] stats) =>
            new EquipmentDef { Id = id, Slot = slot, Name = name, BasePrice = price, Stats = stats };

        private static StatValue S(EquipStat stat, float value) => new StatValue(stat, value);
    }
}
