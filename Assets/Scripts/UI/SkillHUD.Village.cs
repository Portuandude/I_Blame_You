using System;
using IBlameYou.Core;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 마을 창 (Docs/EquipmentDesign.md 9~10절): 마을 건물(Stage/VillageBuilding) 앞에서 W를 톡 / ↑를 누르면 열린다.
    // 식당: 음식 5가지를 골드로 산다 (한 방문에 종류별 한 번, Data/VillageFood.cs). 1~5 또는 클릭으로 사고, Esc·W·↑·닫기로 닫는다.
    // 키보드: ↓·S로 고르기 시작(첫 줄 금색 테두리) → ↑↓·W/S로 옮기고 Enter·Space로 산다. 고르는 중엔 W·↑가 위로 옮기기라 Esc로 닫는다.
    // 장비 상점은 SkillHUD.Shop.cs. 유물 창·장비 카드와 같은 방식으로 게임을 멈추고 조작을 잠근다.
    public partial class SkillHUD
    {
        private bool villageOpen;
        private VillageService villageService;
        private int villageOpenedFrame;
        private float villageOpenedAt;
        private int villageUnlockFrame = -1;
        private string villageMessage; // 창 아래 한 줄 (샀다 / 골드 부족 등)
        private readonly RestaurantVisit restaurantVisit = new RestaurantVisit();

        // 키보드 고르기 (식당·장비 상점 공용). villageCol은 장비 상점의 지금 장비 줄에서 팔기(0)/바꾸기(1).
        // S·Space·Enter는 이동·점프 키이기도 해서 창이 다 뜨기 전(0.4초)엔 받지 않고, Enter·Space는 ↓로 줄을 보인 뒤에만 받는다.
        private const float VillageKeysDelay = 0.4f;
        private bool villageKeyboard;
        private int villageRow;
        private int villageCol;

        // 사기 반응: 산 줄이 금색으로 번쩍(VillageFlashTime), 창 왼쪽 위 골드가 톡 튐. 골드가 모자라면 골드가 빨갛게 좌우로 흔들림.
        private const float VillageFlashTime = 0.45f;
        private const float VillageShakeTime = 0.4f;
        private int villageFlashRow = -1;
        private float villageFlashAt = -9f;
        private float villageGoldPopAt = -9f;
        private float villageGoldShakeAt = -9f;

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

        // 단골 도장이 있으면 마을에서 사는 값 할인 (식당·장비 상점 구매만, 팔기·리롤은 그대로)
        private float VillagePriceRate =>
            relics != null && relics.Has(RelicId.RegularStamp) ? RelicBook.RegularStampPriceRate : 1f;

        private bool OpenVillage(VillageService service)
        {
            if (villageOpen || villageUnlockFrame >= 0 || !CanOpenRelicWindow()) return true; // 다른 창·영창 중: 열지 않고 알림도 띄우지 않는다
            villageOpen = true;
            villageService = service;
            villageOpenedFrame = Time.frameCount;
            villageOpenedAt = Time.unscaledTime;
            villageMessage = null;
            villageKeyboard = false;
            villageRow = 0;
            villageCol = 0;
            villageFlashRow = -1;
            villageGoldShakeAt = -9f;
            villageGoldPopAt = -9f;
            if (service == VillageService.Restaurant) restaurantVisit.BeginVisit(StageEvents.VillageVisit);
            control.Locked = true;
            Time.timeScale = 0f;
            GameAudio.PlaySfx("ShopBell", 0.8f, 0.03f); // 가게 문 종 딸랑 (Tools/AudioGen shop_bell)
            return true;
        }

        // keepLockThisFrame: 키로 닫은 경우 그 키(Esc·W)가 일시정지·순간이동으로 새지 않게 잠금은 다음 프레임에 푼다.
        // playSound: 사람이 닫았을 때만 "통"(PauseClose, 일시정지 창 닫기와 같은 소리). 쓰러짐·클리어로 닫힐 땐 안 낸다.
        private void CloseVillage(bool keepLockThisFrame = false, bool playSound = false)
        {
            if (!villageOpen) return;
            villageOpen = false;
            if (playSound) GameAudio.PlaySfx("PauseClose", 0.6f);
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
            if (Input.GetKeyDown(KeyCode.Escape) || (!villageKeyboard && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))))
            {
                CloseVillage(true, true);
                return true;
            }

            if (villageService == VillageService.Shop)
            {
                UpdateShopKeys(); // SkillHUD.Shop.cs
                return true;
            }

            for (int i = 0; i < VillageFood.Count && i < VillageNumberKeys.Length; i++)
                if (Input.GetKeyDown(VillageNumberKeys[i]) || Input.GetKeyDown(VillageNumpadKeys[i]))
                {
                    BuyFood(VillageFood.All[i]);
                    return true;
                }

            if (!VillageKeysReady || UpdateVillageRowKeys(VillageFood.Count)) return true;
            if (VillageConfirmKey)
            {
                GameAudio.PlaySfx("UiClick", 0.7f, 0.04f); // 마우스로 누를 때와 같은 소리
                BuyFood(VillageFood.All[Mathf.Clamp(villageRow, 0, VillageFood.Count - 1)]);
            }
            return true;
        }

        private bool VillageKeysReady => Time.unscaledTime - villageOpenedAt >= VillageKeysDelay;

        private bool VillageConfirmKey => villageKeyboard &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space));

        // ↑↓·W/S로 줄 옮기기 (끝에서 반대쪽 끝으로). 첫 ↓·S는 첫 줄을 보여 주기만 한다. 키를 썼으면 true.
        private bool UpdateVillageRowKeys(int rows)
        {
            if (rows <= 0) return false;
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            bool up = villageKeyboard && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W));
            if (!down && !up) return false;
            if (!villageKeyboard)
            {
                villageKeyboard = true;
                villageRow = 0;
            }
            else villageRow = ((villageRow + (down ? 1 : -1)) % rows + rows) % rows;
            GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
            return true;
        }

        // 창 위 안내 글의 조작 부분
        private string VillageKeysHint(string numbers, string verb = "사기") => villageKeyboard
            ? $"↑↓ 고르기 · Enter {verb} · Esc 닫기"
            : $"{numbers} 사기 · ↓ 고르기 · Esc/W 닫기";

        private bool HealthFull => health.Current >= health.Max - 0.01f;
        private bool ManaFull => mana.Current >= mana.Max - 0.01f;

        // 음식 아이콘: Tools/ArtGen/generate_food_icons.py → Resources/UI/HUD/Food/<FoodId>.png (24x24)
        private const float FoodIconSize = 28f;
        private static readonly Vector3 FoodGlintOffset = new Vector3(0f, 1.5f, 0f); // 머리 위 (유물 반짝 연출과 같은 높이)
        private static readonly Vector3 StampGlintOffset = new Vector3(0.6f, 1.3f, 0f); // 단골 도장 (음식 아이콘 오른쪽)
        private readonly System.Collections.Generic.Dictionary<FoodId, Texture2D> foodIcons =
            new System.Collections.Generic.Dictionary<FoodId, Texture2D>();

        private Texture2D FoodIcon(FoodId id)
        {
            if (!foodIcons.TryGetValue(id, out var icon))
            {
                icon = Resources.Load<Texture2D>("UI/HUD/Food/" + id);
                foodIcons[id] = icon; // 없어도(null) 기억해서 매 프레임 다시 찾지 않는다
                if (icon != null) icon.filterMode = FilterMode.Point;
            }
            return icon;
        }

        private void BuyFood(FoodInfo food)
        {
            float rate = VillagePriceRate;
            var check = restaurantVisit.Check(food.Id, SpendableGold, HealthFull, ManaFull, rate);
            if (check != FoodBuyCheck.Ok)
            {
                if (check == FoodBuyCheck.NotEnoughGold) VillageGoldShake();
                villageMessage = $"<color=#ff9a6b>{BuyCheckText(check)}</color>";
                GameAudio.PlaySfx("Switch_Tick", 0.5f); // 안 됨: 짧은 똑딱
                return;
            }

            int price = VillagePrice.Apply(food.Price, rate);
            if (!RunStats.TrySpendGold(price, RunGold.InFlight)) return;
            restaurantVisit.TryBuy(food.Id, price, false, false, rate); // 위에서 이미 확인함: 기록만
            if (price < food.Price) VfxPlayer.StampGlint(health.transform, StampGlintOffset); // 단골 도장 할인 쾅
            ApplyFood(food);
            for (int i = 0; i < VillageFood.Count; i++)
                if (VillageFood.All[i].Id == food.Id) VillageFlash(i);
            villageMessage = $"<color=#7dff8a>{food.Name}: {food.Describe()}</color>";
            GameAudio.PlaySfx("Coin", 0.7f);
            GameAudio.PlaySfx("Pickup", 0.6f);
            StageEvents.RaiseVillageNpcSay(VillageService.Restaurant, "맛있게 드세요!"); // 창을 닫으면 식당 주인이 톡
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
            VfxPlayer.FoodGlint(health.transform, FoodGlintOffset, food.Id.ToString()); // 산 음식이 머리 위로 톡
        }

        private void VillageFlash(int row)
        {
            villageFlashRow = row;
            villageFlashAt = Time.unscaledTime;
            villageGoldPopAt = Time.unscaledTime;
        }

        private void VillageGoldShake() => villageGoldShakeAt = Time.unscaledTime;

        // 방금 산 줄 위에 금색이 번쩍했다가 흐려진다
        private void DrawVillageRowFlash(Rect row, int index)
        {
            if (index != villageFlashRow || Event.current.type != EventType.Repaint) return;
            float t = (Time.unscaledTime - villageFlashAt) / VillageFlashTime;
            if (t < 0f || t >= 1f) return;
            var previous = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.35f * (1f - t) * (1f - t));
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // 창 왼쪽 위: 금화 + 쓸 수 있는 골드. 사면 톡 커지고, 모자라면 빨갛게 좌우로 흔들린다.
        private void DrawVillageGold(Rect window)
        {
            EnsureGoldArt();
            float shakeT = (Time.unscaledTime - villageGoldShakeAt) / VillageShakeTime;
            bool shaking = shakeT >= 0f && shakeT < 1f;
            float dx = shaking ? Mathf.Sin(shakeT * 40f) * 5f * (1f - shakeT) : 0f;
            float popT = (Time.unscaledTime - villageGoldPopAt) / GoldPopTime;
            float pop = popT >= 0f && popT < 1f ? 1f + 0.25f * Mathf.Sin(popT * Mathf.PI) : 1f;
            var row = new Rect(window.x + 18f + dx, window.y + 14f, 130f, 28f);
            var previousMatrix = GUI.matrix;
            if (pop > 1f) GUIUtility.ScaleAroundPivot(Vector2.one * pop, new Vector2(row.x + 12f, row.center.y));
            if (coinIcon != null) GUI.DrawTexture(new Rect(row.x, row.y + 2f, 24f, 24f), coinIcon, ScaleMode.ScaleToFit, true);
            var color = shaking ? Color.Lerp(GoldSpendRed, OutlinedLabel.Gold, shakeT * shakeT) : OutlinedLabel.Gold;
            OutlinedLabel.Draw(new Rect(row.x + 28f, row.y, row.width - 28f, row.height), SpendableGold.ToString(), goldText,
                color, OutlinedLabel.Outline, 0f, 1.5f);
            GUI.matrix = previousMatrix;
        }

        // 창 뒤 화면을 어둡게 (열 때 VillageFadeTime 동안 깔린다, 유물 창과 같은 길이)
        private const float VillageFadeTime = 0.15f;

        private void DrawVillageBackdrop()
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((Time.unscaledTime - villageOpenedAt) / VillageFadeTime));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
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
            if (villageService == VillageService.Shop)
            {
                DrawShopWindow(); // SkillHUD.Shop.cs
                return;
            }

            int count = VillageFood.Count;
            const float rowHeight = 46f;
            float width = Mathf.Min(560f, Screen.width - 32f);
            float height = 112f + count * rowHeight + 70f;
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(16f, (Screen.height - height) / 2f), width, height);

            DrawVillageBackdrop();
            var previousMatrix = BeginCardPop(rect, villageOpenedAt, 0);
            DrawCard(rect, "공용");

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "식당", big);
            DrawVillageGold(rect);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 46f, rect.width - 40f, 22f),
                $"한 번 들를 때 종류별 한 그릇 · {VillageKeysHint("1~5")}",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true });

            bool healthFull = HealthFull, manaFull = ManaFull;
            float rate = VillagePriceRate;
            float y = rect.y + 80f;
            for (int i = 0; i < count; i++)
            {
                var food = VillageFood.All[i];
                var check = restaurantVisit.Check(food.Id, SpendableGold, healthFull, manaFull, rate);
                var row = new Rect(rect.x + 20f, y, rect.width - 40f, rowHeight - 6f);
                GUI.Box(row, GUIContent.none, panel);
                DrawVillageRowFlash(row, i);
                if (villageKeyboard && i == villageRow) CuteButton.DrawSelection(row);
                string name = check == FoodBuyCheck.Ok ? $"<b>{food.Name}</b>" : $"<color=#8a8a8a><b>{food.Name}</b></color>";
                string desc = check == FoodBuyCheck.AlreadyBought ? "<color=#8a8a8a>다 먹었어요</color>" : food.Describe();
                var icon = FoodIcon(food.Id);
                if (icon != null)
                {
                    var tint = GUI.color;
                    if (check == FoodBuyCheck.AlreadyBought) GUI.color = new Color(tint.r, tint.g, tint.b, tint.a * 0.4f); // 다 먹은 건 흐리게
                    GUI.DrawTexture(new Rect(row.x + 6f, row.y + (row.height - FoodIconSize) / 2f, FoodIconSize, FoodIconSize), icon, ScaleMode.ScaleToFit);
                    GUI.color = tint;
                }
                float textX = row.x + FoodIconSize + 12f;
                GUI.Label(new Rect(textX, row.y + 2f, row.xMax - 130f - textX, 20f), $"{i + 1}. {name}", new GUIStyle(label) { richText = true });
                GUI.Label(new Rect(textX + 18f, row.y + 20f, row.xMax - 130f - textX - 18f, 18f), desc, new GUIStyle(small) { richText = true });

                var buy = new Rect(row.xMax - 120f, row.y + 5f, 110f, row.height - 10f);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = check == FoodBuyCheck.Ok;
                int cost = VillagePrice.Apply(food.Price, rate);
                string price = check == FoodBuyCheck.NotEnoughGold ? $"<color=#ff6b6b>{cost} G</color>" : $"{cost} G";
                if (cost < food.Price) price += " <size=11><color=#7dff8a>도장</color></size>"; // 단골 도장 할인
                if (CuteButton.Button(buy, price, new GUIStyle(cardButton) { richText = true })) BuyFood(food);
                GUI.enabled = wasEnabled;
                y += rowHeight;
            }

            if (!string.IsNullOrEmpty(villageMessage))
                GUI.Label(new Rect(rect.x + 20f, y + 2f, rect.width - 40f, 22f), villageMessage,
                    new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true });
            if (CuteButton.Button(new Rect(rect.center.x - 70f, rect.yMax - 44f, 140f, 32f), "닫기", cardButton)) CloseVillage(false, true);
            GUI.matrix = previousMatrix;
        }
    }
}
