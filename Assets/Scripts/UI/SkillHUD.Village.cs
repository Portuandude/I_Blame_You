using System;
using IBlameYou.Core;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 마을 창 (Docs/EquipmentDesign.md 9~10절): 마을 건물(Stage/VillageBuilding) 앞에서 W를 톡 / ↑를 누르면 열린다.
    // 식당: 음식 5가지를 골드로 산다 (한 방문에 종류별 한 번, Data/VillageFood.cs). 1~5 또는 클릭으로 사고, Esc·W·↑·닫기로 닫는다.
    // 유물 창·장비 카드와 같은 방식으로 게임을 멈추고 조작을 잠근다. 장비 상점은 다음 작업 (지금은 "곧 문을 열어요").
    public partial class SkillHUD
    {
        private bool villageOpen;
        private VillageService villageService;
        private int villageOpenedFrame;
        private float villageOpenedAt;
        private int villageUnlockFrame = -1;
        private string villageMessage; // 창 아래 한 줄 (샀다 / 골드 부족 등)
        private readonly RestaurantVisit restaurantVisit = new RestaurantVisit();

        private static readonly KeyCode[] VillageNumberKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
        private static readonly KeyCode[] VillageNumpadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5 };

        private void InitializeVillage()
        {
            StageEvents.VillageInteractRequested = OpenVillage;
        }

        private void ShutdownVillage()
        {
            if (StageEvents.VillageInteractRequested == (Func<VillageService, bool>)OpenVillage) StageEvents.VillageInteractRequested = null;
        }

        private int SpendableGold => Mathf.Max(0, RunStats.Gold - RunGold.InFlight);

        private bool OpenVillage(VillageService service)
        {
            if (service != VillageService.Restaurant) return false; // 장비 상점 창은 아직 (VillageBuilding이 "곧 문을 열어요")
            if (villageOpen || villageUnlockFrame >= 0 || !CanOpenRelicWindow()) return true; // 다른 창·영창 중: 열지 않고 알림도 띄우지 않는다
            villageOpen = true;
            villageService = service;
            villageOpenedFrame = Time.frameCount;
            villageOpenedAt = Time.unscaledTime;
            villageMessage = null;
            restaurantVisit.BeginVisit(StageEvents.VillageVisit);
            control.Locked = true;
            Time.timeScale = 0f;
            GameAudio.PlaySfx("RelicOpen", 0.4f);
            return true;
        }

        // keepLockThisFrame: 키로 닫은 경우 그 키(Esc·W)가 일시정지·순간이동으로 새지 않게 잠금은 다음 프레임에 푼다.
        private void CloseVillage(bool keepLockThisFrame = false)
        {
            if (!villageOpen) return;
            villageOpen = false;
            Time.timeScale = 1f;
            if (keepLockThisFrame) villageUnlockFrame = Time.frameCount + 1;
            else if (!deathHandled && !stageCleared) control.Locked = false;
        }

        // 일시정지 입력보다 먼저 부른다 (열려 있으면 Esc는 이 창 몫). 창이 이번 프레임 입력을 썼으면 true.
        private bool UpdateVillageInput()
        {
            if (villageUnlockFrame >= 0 && Time.frameCount >= villageUnlockFrame)
            {
                villageUnlockFrame = -1;
                if (!deathHandled && !stageCleared && !paused && !traitWindowOpen && !relicWindowOpen && !equipCardOpen) control.Locked = false;
            }

            if (!villageOpen) return villageUnlockFrame >= 0;
            if (Time.frameCount <= villageOpenedFrame) return true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                CloseVillage(true);
                return true;
            }

            for (int i = 0; i < VillageFood.Count && i < VillageNumberKeys.Length; i++)
                if (Input.GetKeyDown(VillageNumberKeys[i]) || Input.GetKeyDown(VillageNumpadKeys[i]))
                    BuyFood(VillageFood.All[i]);
            return true;
        }

        private bool HealthFull => health.Current >= health.Max - 0.01f;
        private bool ManaFull => mana.Current >= mana.Max - 0.01f;

        private void BuyFood(FoodInfo food)
        {
            var check = restaurantVisit.Check(food.Id, SpendableGold, HealthFull, ManaFull);
            if (check != FoodBuyCheck.Ok)
            {
                villageMessage = $"<color=#ff9a6b>{BuyCheckText(check)}</color>";
                GameAudio.PlaySfx("Switch_Tick", 0.5f); // 안 됨: 짧은 똑딱
                return;
            }

            if (!RunStats.TrySpendGold(food.Price, RunGold.InFlight)) return;
            restaurantVisit.TryBuy(food.Id, food.Price, false, false); // 위에서 이미 확인함: 기록만
            ApplyFood(food);
            villageMessage = $"<color=#7dff8a>{food.Name}: {food.Describe()}</color>";
            GameAudio.PlaySfx("Coin", 0.7f);
            GameAudio.PlaySfx("Pickup", 0.6f);
        }

        private void ApplyFood(FoodInfo food)
        {
            var at = health.transform.position;
            switch (food.Effect)
            {
                case FoodEffect.HealRatio:
                {
                    float before = health.Current;
                    health.Heal(health.Max * food.Value);
                    DamageNumbers.ShowHeal(at, health.Current - before);
                    break;
                }
                case FoodEffect.ManaFull:
                    mana.Restore(mana.Max);
                    break;
                case FoodEffect.MaxHealth:
                    health.AddMax(food.Value, true);
                    DamageNumbers.ShowHeal(at, food.Value, "최대 체력 ");
                    break;
                case FoodEffect.ManaRegen:
                    mana.RegenMultiplier += food.Value;
                    break;
                case FoodEffect.DamageCut:
                    LunchboxGuard.Grant(health.gameObject, food.Value);
                    break;
            }
            StageEvents.PostNotice($"{food.Name} 냠!");
        }

        private static string BuyCheckText(FoodBuyCheck check)
        {
            switch (check)
            {
                case FoodBuyCheck.AlreadyBought: return "이번 방문에는 이미 먹었어요";
                case FoodBuyCheck.NotEnoughGold: return "골드가 모자라요";
                case FoodBuyCheck.NoEffect: return "지금은 먹어도 소용없어요 (이미 가득)";
                default: return "";
            }
        }

        private void DrawVillageWindow()
        {
            if (!villageOpen) return;

            int count = VillageFood.Count;
            const float rowHeight = 46f;
            float width = Mathf.Min(560f, Screen.width - 32f);
            float height = 112f + count * rowHeight + 70f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(16f, (Screen.height - height) / 2f), width, height);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            var previousMatrix = BeginCardPop(rect, villageOpenedAt, 0);
            DrawCard(rect, "공용");

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "식당", big);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 46f, rect.width - 40f, 22f),
                $"골드 <color=#ffd84d>{SpendableGold}</color> · 한 번 들를 때 종류별 한 그릇 · 1~5 사기 · Esc/W 닫기",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true });

            bool healthFull = HealthFull, manaFull = ManaFull;
            float y = rect.y + 80f;
            for (int i = 0; i < count; i++)
            {
                var food = VillageFood.All[i];
                var check = restaurantVisit.Check(food.Id, SpendableGold, healthFull, manaFull);
                var row = new Rect(rect.x + 20f, y, rect.width - 40f, rowHeight - 6f);
                GUI.Box(row, GUIContent.none, panel);
                string name = check == FoodBuyCheck.Ok ? $"<b>{food.Name}</b>" : $"<color=#8a8a8a><b>{food.Name}</b></color>";
                string desc = check == FoodBuyCheck.AlreadyBought ? "<color=#8a8a8a>다 먹었어요</color>" : food.Describe();
                GUI.Label(new Rect(row.x + 10f, row.y + 2f, row.width - 150f, 20f), $"{i + 1}. {name}", new GUIStyle(label) { richText = true });
                GUI.Label(new Rect(row.x + 28f, row.y + 20f, row.width - 160f, 18f), desc, new GUIStyle(small) { richText = true });

                var buy = new Rect(row.xMax - 120f, row.y + 5f, 110f, row.height - 10f);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = check == FoodBuyCheck.Ok;
                string price = check == FoodBuyCheck.NotEnoughGold ? $"<color=#ff6b6b>{food.Price} G</color>" : $"{food.Price} G";
                if (GUI.Button(buy, price, new GUIStyle(cardButton) { richText = true })) BuyFood(food);
                GUI.enabled = wasEnabled;
                y += rowHeight;
            }

            if (!string.IsNullOrEmpty(villageMessage))
                GUI.Label(new Rect(rect.x + 20f, y + 2f, rect.width - 40f, 22f), villageMessage,
                    new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true });
            if (GUI.Button(new Rect(rect.center.x - 70f, rect.yMax - 44f, 140f, 32f), "닫기", cardButton)) CloseVillage();
            GUI.matrix = previousMatrix;
        }
    }
}
