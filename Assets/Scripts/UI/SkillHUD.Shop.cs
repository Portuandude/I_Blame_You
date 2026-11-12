using System.Collections.Generic;
using IBlameYou.Core;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 마을 장비 상점 창 (Docs/EquipmentDesign.md 5·6절, 규칙은 Player/Equipment/Data/EquipmentShop.cs).
    // 위: 진열 4개 (1~4 또는 클릭으로 사기, 그 부위에 낀 장비는 판매가만큼 값을 깎고 팔린다). 아래: 낀 장비 3칸 (팔기·수식어 바꾸기, 클릭).
    // 여닫기·게임 정지는 마을 창(SkillHUD.Village.cs)과 같다. 진열은 스테이지 시드 + 마을 방 번호로 정해 판 안에서 다시 들어와도 같다.
    public partial class SkillHUD
    {
        private readonly Dictionary<long, EquipmentShop> shops = new Dictionary<long, EquipmentShop>();
        private static readonly KeyCode[] ShopNumberKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
        private static readonly KeyCode[] ShopNumpadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4 };

        private EquipmentShop CurrentShop
        {
            get
            {
                long key = ((long)StageEvents.StageSeed << 32) | (uint)StageEvents.VillageRoom;
                if (!shops.TryGetValue(key, out var shop))
                {
                    shop = new EquipmentShop(StageEvents.StageSeed, StageEvents.VillageRoom);
                    shops[key] = shop;
                }
                return shop;
            }
        }

        private void UpdateShopKeys()
        {
            var shop = CurrentShop;
            for (int i = 0; i < ShopNumberKeys.Length; i++)
                if (Input.GetKeyDown(ShopNumberKeys[i]) || Input.GetKeyDown(ShopNumpadKeys[i]))
                {
                    if (i < shop.Stock.Count) BuyEquipment(shop, i);
                    return;
                }
        }

        private void BuyEquipment(EquipmentShop shop, int index)
        {
            var equip = PlayerEquipment.For(health.gameObject);
            var item = shop.Stock[index];
            var current = equip.Get(item.Slot);
            int gold = SpendableGold;
            if (!shop.CanBuy(index, current, gold))
            {
                villageMessage = "<color=#ff9a6b>골드가 모자라요</color>";
                GameAudio.PlaySfx("Switch_Tick", 0.5f);
                return;
            }

            int cost = EquipmentShop.NetCost(item, current);
            if (!RunStats.TrySpendGold(cost, RunGold.InFlight)) return;
            shop.Buy(index, current, gold, out _);
            var old = equip.Equip(item); // 옛 장비는 값에서 이미 깎았다 (판매)
            villageMessage = old != null
                ? $"<color=#7dff8a>{item.Name} 장착 · {old.Name} 판매 (-{cost} G)</color>"
                : $"<color=#7dff8a>{item.Name} 장착 (-{cost} G)</color>";
            StageEvents.PostNotice($"장비: {item.Name}");
            GameAudio.PlaySfx("Coin", 0.7f);
            GameAudio.PlaySfx("RelicOpen", 0.5f);
        }

        private void SellEquipment(EquipSlot slot)
        {
            var equip = PlayerEquipment.For(health.gameObject);
            var item = equip.Get(slot);
            if (item == null) return;
            equip.Unequip(slot);
            RunStats.AddGold(item.SellPrice);
            villageMessage = $"<color=#ffd84d>{item.Name} 판매 (+{item.SellPrice} G)</color>";
            GameAudio.PlaySfx("Coin", 0.8f);
        }

        private void RerollEquipment(EquipmentShop shop, EquipSlot slot)
        {
            var equip = PlayerEquipment.For(health.gameObject);
            var item = equip.Get(slot);
            if (item == null) return;
            if (!EquipmentShop.CanReroll(item, SpendableGold))
            {
                villageMessage = "<color=#ff9a6b>골드가 모자라요</color>";
                GameAudio.PlaySfx("Switch_Tick", 0.5f);
                return;
            }

            int before = item.Grade;
            if (!RunStats.TrySpendGold(item.RerollCost, RunGold.InFlight)) return;
            int cost = shop.Reroll(item, int.MaxValue); // 골드는 위에서 뺐다
            equip.Refresh(slot);
            string turn = item.Grade > before ? "좋아졌다!" : item.Grade < before ? "나빠졌다…" : "그대로";
            string color = item.Grade > before ? "#7dff8a" : item.Grade < before ? "#ff6b6b" : "#fff4dc";
            villageMessage = $"<color={color}>수식어 변경 (-{cost} G): {item.Name} — {turn}</color>";
            GameAudio.PlaySfx(item.Grade > before ? "RelicNew" : "Switch_On", 0.6f);
        }

        private void DrawShopWindow()
        {
            var shop = CurrentShop;
            var equip = PlayerEquipment.For(health.gameObject);
            const float rowHeight = 50f;
            float width = Mathf.Min(680f, Screen.width - 32f);
            float height = 92f + EquipmentShop.StockSize * rowHeight + 34f + 3 * rowHeight + 74f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(16f, (Screen.height - height) / 2f), width, height);

            DrawVillageBackdrop();
            var previousMatrix = BeginCardPop(rect, villageOpenedAt, 0);
            DrawCard(rect, "공용");
            var smallRich = new GUIStyle(small) { richText = true };
            var labelRich = new GUIStyle(label) { richText = true };
            var buttonRich = new GUIStyle(cardButton) { richText = true };

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "장비 상점", big);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 46f, rect.width - 40f, 22f),
                $"골드 <color=#ffd84d>{SpendableGold}</color> · 1~4 사기 (같은 부위 장비는 팔고 그만큼 깎음) · 아래에서 팔기·수식어 바꾸기 · Esc/W 닫기",
                new GUIStyle(smallRich) { alignment = TextAnchor.MiddleCenter });

            int gold = SpendableGold;
            float y = rect.y + 76f;
            for (int i = 0; i < EquipmentShop.StockSize; i++)
            {
                var row = new Rect(rect.x + 20f, y, rect.width - 40f, rowHeight - 6f);
                GUI.Box(row, GUIContent.none, panel);
                if (i < shop.Stock.Count)
                {
                    var item = shop.Stock[i];
                    var current = equip.Get(item.Slot);
                    int cost = EquipmentShop.NetCost(item, current);
                    DrawShopItem(row, $"{i + 1}. ", item, labelRich, smallRich);
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = shop.CanBuy(i, current, gold);
                    string price = current != null ? $"{cost} G <size=11>(-{current.SellPrice})</size>" : $"{cost} G";
                    if (GUI.Button(new Rect(row.xMax - 130f, row.y + 6f, 120f, row.height - 12f), price, buttonRich)) BuyEquipment(shop, i);
                    GUI.enabled = wasEnabled;
                }
                else
                {
                    GUI.Label(new Rect(row.x + 12f, row.y + 10f, row.width - 24f, 22f), "<color=#8a8a8a>팔렸어요</color>", labelRich);
                }
                y += rowHeight;
            }

            GUI.Label(new Rect(rect.x + 20f, y + 4f, rect.width - 40f, 24f), "지금 장비 (A·B 공용)", smallRich);
            y += 30f;
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                var row = new Rect(rect.x + 20f, y, rect.width - 40f, rowHeight - 6f);
                GUI.Box(row, GUIContent.none, panel);
                var item = equip.Get(slot);
                if (item == null)
                {
                    GUI.Label(new Rect(row.x + 12f, row.y + 10f, row.width - 24f, 22f), $"<color=#8a8a8a>{SlotName(slot)}: 없음</color>", labelRich);
                }
                else
                {
                    DrawShopItem(row, "", item, labelRich, smallRich);
                    if (GUI.Button(new Rect(row.xMax - 250f, row.y + 6f, 110f, row.height - 12f), $"팔기 +{item.SellPrice}", buttonRich))
                        SellEquipment(slot);
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = EquipmentShop.CanReroll(item, gold);
                    if (GUI.Button(new Rect(row.xMax - 130f, row.y + 6f, 120f, row.height - 12f), $"바꾸기 {item.RerollCost} G", buttonRich))
                        RerollEquipment(shop, slot);
                    GUI.enabled = wasEnabled;
                }
                y += rowHeight;
            }

            if (!string.IsNullOrEmpty(villageMessage))
                GUI.Label(new Rect(rect.x + 20f, y + 2f, rect.width - 40f, 22f), villageMessage, new GUIStyle(smallRich) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(rect.center.x - 70f, rect.yMax - 44f, 140f, 32f), "닫기", cardButton)) CloseVillage();
            GUI.matrix = previousMatrix;
        }

        // 아이콘 + 이름(등급 색) + 능력치 한 줄
        private void DrawShopItem(Rect row, string prefix, EquipmentItem item, GUIStyle labelRich, GUIStyle smallRich)
        {
            var icon = EquipmentIcon(item.Id);
            if (icon != null) GUI.DrawTexture(new Rect(row.x + 8f, row.y + 6f, 32f, 32f), icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(row.x + 46f, row.y + 2f, row.width - 320f, 22f),
                $"{prefix}<b><color={GradeColor(item.Grade)}>{item.Name}</color></b>", labelRich);
            var stats = new System.Text.StringBuilder();
            foreach (var s in item.Stats())
            {
                if (stats.Length > 0) stats.Append(" · ");
                stats.Append(EquipmentBook.Describe(s));
            }
            GUI.Label(new Rect(row.x + 46f, row.y + 22f, row.width - 310f, 20f), stats.ToString(), smallRich);
        }

        private static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Robe: return "로브";
                case EquipSlot.Staff: return "지팡이";
                default: return "신발";
            }
        }
    }
}
