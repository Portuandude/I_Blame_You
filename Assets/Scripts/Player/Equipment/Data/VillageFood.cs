using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 마을 식당 음식 (Docs/EquipmentDesign.md 10절). 골드로 체력·마나 관련 런 도움을 산다.
    // 한 번 방문(마을 방에 들어올 때마다 새 방문)에 종류별 한 번만 산다 (연속 회복 남용 방지).
    // UnityEngine 없이 돌아서 가격·효과 범위·방문 규칙을 EditMode 테스트로 고정한다. 실제 적용은 UI/SkillHUD.Village.cs.
    public enum FoodId
    {
        WarmSoup,    // 따끈한 수프: 체력 즉시 회복 (최대 체력 비율)
        StarTea,     // 별빛 차: 마나 즉시 가득
        MushroomStew, // 버섯 스튜: 최대 체력 + (이번 판)
        HoneyDumpling, // 꿀 경단: 마나 재생 + (이번 판)
        Lunchbox     // 든든한 도시락: 다음 전투 구역을 돌파할 때까지 받는 피해 -
    }

    public enum FoodEffect
    {
        HealRatio,      // 최대 체력 × Value 회복
        ManaFull,       // 마나 가득
        MaxHealth,      // 최대 체력 + Value (현재 체력도 같이)
        ManaRegen,      // 마나 재생 배율 + Value
        DamageCut       // 받는 피해 × (1 - Value), 다음 전투 구역 돌파까지
    }

    public sealed class FoodInfo
    {
        public readonly FoodId Id;
        public readonly string Name;
        public readonly FoodEffect Effect;
        public readonly float Value;
        public readonly int Price;

        public FoodInfo(FoodId id, string name, FoodEffect effect, float value, int price)
        {
            Id = id;
            Name = name;
            Effect = effect;
            Value = value;
            Price = price;
        }

        // 메뉴판 한 줄 설명
        public string Describe()
        {
            switch (Effect)
            {
                case FoodEffect.HealRatio: return $"체력 {Math.Round(Value * 100f)}% 회복";
                case FoodEffect.ManaFull: return "마나 가득";
                case FoodEffect.MaxHealth: return $"최대 체력 +{Math.Round(Value)} (이번 판)";
                case FoodEffect.ManaRegen: return $"마나 재생 +{Math.Round(Value * 100f)}% (이번 판)";
                case FoodEffect.DamageCut: return $"다음 전투 구역 돌파까지 받는 피해 -{Math.Round(Value * 100f)}%";
                default: return "";
            }
        }
    }

    public static class VillageFood
    {
        private static readonly FoodInfo[] Menu =
        {
            new FoodInfo(FoodId.WarmSoup, "따끈한 수프", FoodEffect.HealRatio, 0.3f, 20),
            new FoodInfo(FoodId.StarTea, "별빛 차", FoodEffect.ManaFull, 1f, 15),
            new FoodInfo(FoodId.MushroomStew, "버섯 스튜", FoodEffect.MaxHealth, 15f, 45),
            new FoodInfo(FoodId.HoneyDumpling, "꿀 경단", FoodEffect.ManaRegen, 0.15f, 40),
            new FoodInfo(FoodId.Lunchbox, "든든한 도시락", FoodEffect.DamageCut, 0.2f, 30),
        };

        public static IReadOnlyList<FoodInfo> All => Menu;
        public static int Count => Menu.Length;

        public static FoodInfo Get(FoodId id)
        {
            foreach (var food in Menu) if (food.Id == id) return food;
            throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    // 살 수 있는지 (UI 버튼 회색·안내 문구)
    public enum FoodBuyCheck
    {
        Ok,
        AlreadyBought,  // 이번 방문에 이미 삼
        NotEnoughGold,
        NoEffect        // 지금 먹어도 소용없음 (체력·마나가 이미 가득)
    }

    // 한 판 동안의 식당 기록: 방문 번호와 이번 방문에 산 음식. 마을 방에 새로 들어오면 BeginVisit.
    public sealed class RestaurantVisit
    {
        private readonly HashSet<FoodId> bought = new HashSet<FoodId>();

        public int Visit { get; private set; } = -1;

        // visit이 지난 방문과 다르면 산 기록을 비운다 (같은 방문 번호면 그대로 = 창을 닫았다 열어도 다시 못 삼).
        public void BeginVisit(int visit)
        {
            if (visit == Visit) return;
            Visit = visit;
            bought.Clear();
        }

        public bool Bought(FoodId id) => bought.Contains(id);

        // gold: 쓸 수 있는 골드. healthFull/manaFull: 회복 음식이 소용없는지.
        public FoodBuyCheck Check(FoodId id, int gold, bool healthFull, bool manaFull)
        {
            if (bought.Contains(id)) return FoodBuyCheck.AlreadyBought;
            var food = VillageFood.Get(id);
            if (food.Effect == FoodEffect.HealRatio && healthFull) return FoodBuyCheck.NoEffect;
            if (food.Effect == FoodEffect.ManaFull && manaFull) return FoodBuyCheck.NoEffect;
            if (gold < food.Price) return FoodBuyCheck.NotEnoughGold;
            return FoodBuyCheck.Ok;
        }

        // 샀다고 기록한다. Check가 Ok가 아니면 false (아무것도 바꾸지 않음).
        public bool TryBuy(FoodId id, int gold, bool healthFull, bool manaFull)
        {
            if (Check(id, gold, healthFull, manaFull) != FoodBuyCheck.Ok) return false;
            bought.Add(id);
            return true;
        }
    }
}
