using System;
using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // 온라인 친구 화면에도 (Net/NetGUI)
using GUILayout = IBlameYou.Net.NetGUILayout;

namespace IBlameYou.Core
{
    // 스테이지 사이 쉼터 (2026-11-24 사람 결정, Chunks/RestRule): 스테이지 1~4 클리어 결과 창 "쉼터로" → 이 창 → 다음 스테이지.
    // 판은 치운 상태라 저장 데이터(RunSave.Data)를 직접 고친다: 샘(한 번, 체력 가득), 떠돌이 상인(장비 4개, 낀 장비 판매가만큼 깎아 줌),
    // 상인 한마디. [다음 스테이지]·[저장하고 시작 화면] 둘 다 고친 데이터를 저장하고 넘긴다. 들어올 때 이미 한 번 저장했다.
    // 그림은 아직 없다 (메뉴 창만, 걷는 쉼터 장면은 아트 승인 뒤). CityMenu와 같은 둥근 패널·젤리 버튼.
    public class RestMenu : MonoBehaviour
    {
        private RunSave.Data data;
        private Action<RunSave.Data> onNext;
        private Action<RunSave.Data> onSaveQuit;
        private EquipmentShop shop;
        private string line;
        private int springLeft = RestRule.SpringUses;
        private string feedback = "";

        private GUIStyle title;
        private GUIStyle body;
        private GUIStyle small;
        private GUIStyle button;
        private GUIStyle panel;
        private int keySelection;
        private bool keyboardUsed;
        private bool activateKeyHeld;
        private bool firstFrame = true;
        private bool done;
        private float openedAt = -1f;

        // 칸: 0 샘 · 1..진열 수 상인 · 그다음 다음 스테이지 · 저장하고 시작 화면
        private int SlotSpring => 0;
        private int SlotShop(int i) => 1 + i;
        private int SlotNext => 1 + shop.Stock.Count;
        private int SlotQuit => SlotNext + 1;
        private int SlotCount => SlotQuit + 1;

        public void Initialize(RunSave.Data runData, Action<RunSave.Data> nextCallback, Action<RunSave.Data> saveQuitCallback)
        {
            data = runData;
            onNext = nextCallback;
            onSaveQuit = saveQuitCallback;
            shop = new EquipmentShop(RunFlowRule.StageSeed(data.seed, data.stage), RestRule.ShopRoom(data.stage));
            line = RestRule.Line(data.seed, data.stage);
            keySelection = SlotNext;
            GameAudio.PlayMusic("Menu");
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            title.normal.textColor = new Color(1f, 0.95f, 0.8f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, richText = true, alignment = TextAnchor.MiddleCenter };
            body.normal.textColor = Color.white;
            small = new GUIStyle(body) { fontSize = 13 };
            small.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            panel = new GUIStyle(GUI.skin.box);
            var panelTexture = Resources.Load<Texture2D>("UI/HUD/Panel");
            if (panelTexture != null)
            {
                panel.normal.background = panelTexture;
                panel.border = new RectOffset(10, 10, 10, 10);
            }
            button = CuteButton.Create(17);
        }

        private void OnGUI()
        {
            if (done || data == null) return;
            EnsureStyles();
            if (firstFrame)
            {
                firstFrame = false;
                activateKeyHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
            }
            if (openedAt < 0f) openedAt = Time.unscaledTime;

            float width = Mathf.Min(600f, Screen.width - 20f); // 좁은 창에서도 화면 안에
            float height = Mathf.Min(620f, Screen.height - 20f);
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.3f);
            var screenMatrix = GUI.matrix;
            float pop = t >= 1f ? 1f : Mathf.Lerp(0.88f, 1f, Mathf.SmoothStep(0f, 1f, t));
            if (pop != 1f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), area.center);
            GUI.Box(area, GUIContent.none, panel);
            GUI.Box(area, GUIContent.none, panel);

            int pressed = HandleKeys(Event.current);

            GUILayout.BeginArea(new Rect(area.x + 28f, area.y + 16f, width - 56f, height - 32f));
            GUILayout.Label($"쉼터 — 스테이지 {data.stage} 앞", title, GUILayout.Height(48f));
            GUILayout.Label(line, small);
            if (IBlameYou.Net.NetSession.RemoteP2Active) GUILayout.Label("<color=#9fd8ff>온라인: 쉼터는 호스트(아루) 화면에서 고른다</color>", small); // 친구 화면에도 같은 글이 간다 (버그 점검 2026-11-24)
            GUILayout.Space(8f);
            float max = Mathf.Max(data.maxHealth, data.health);
            GUILayout.Label($"체력 {data.health:0}/{max:0}  ·  <color=#ffd84d><b>골드 {data.gold}</b></color>  ·  레벨 {data.level}", body);
            GUILayout.Label(EquippedText(), small);
            GUILayout.Space(10f);

            string spring = springLeft > 0 ? (data.health >= max ? "샘 — 체력이 이미 가득하다" : "샘 — 한 모금 마시고 체력 가득") : "샘 — 다 마셨다";
            if (MenuButton(SlotSpring, spring, 40f, pressed)) DrinkSpring(max);
            GUILayout.Space(8f);
            GUILayout.Label("떠돌이 상인 (낀 장비는 판매가만큼 깎아 준다)", small);
            int bought = -1;
            int nextSlot = SlotNext, quitSlot = SlotQuit; // 산 칸이 빠지기 전 번호: 마지막 진열품에서 Enter가 [다음 스테이지]까지 누르지 않게
            for (int i = 0; i < shop.Stock.Count; i++)
            {
                var item = shop.Stock[i];
                var current = Equipped(item.Slot);
                int cost = EquipmentShop.NetCost(item, current);
                string swap = current != null ? $" ← {current.Name}" : "";
                string price = data.gold >= cost ? $"{cost}G" : $"<color=#ff8080>{cost}G</color>";
                if (MenuButton(SlotShop(i), $"{item.Name}{swap} · {price}", 34f, pressed)) bought = i;
                GUILayout.Space(3f);
            }
            if (bought >= 0) Buy(bought);
            GUILayout.Label(feedback ?? "", small); // 늘 그린다: 사고 마신 그 이벤트에 칸 수가 바뀌면 IMGUI가 [다음]·[저장] 클릭을 잃는다

            GUILayout.FlexibleSpace();
            bool next = MenuButton(nextSlot, $"다음 스테이지 {data.stage}로", 50f, pressed);
            GUILayout.Space(6f);
            bool quit = MenuButton(quitSlot, "저장하고 시작 화면", 38f, pressed);
            GUILayout.Label("↑↓ 고르기 · Enter 누르기 · 저장은 쉼터에서만", small);
            GUILayout.EndArea();
            GUI.matrix = screenMatrix;

            if (next) Leave(onNext);
            else if (quit) Leave(onSaveQuit);
        }

        private void Leave(Action<RunSave.Data> callback)
        {
            if (done) return;
            done = true;
            Destroy(this);
            callback?.Invoke(data);
        }

        private void DrinkSpring(float max)
        {
            if (springLeft <= 0 || data.health >= max) return;
            springLeft--;
            data.health = RestRule.SpringHeal(data.health, max);
            feedback = "<color=#9fe8ff>샘물을 마셨다. 몸이 가볍다.</color>";
            RunSave.Save(data); // 쉼터 안에서 바뀐 것도 바로 남긴다 (앱을 꺼도)
            GameAudio.PlaySfx("Pickup", 0.7f, 0.05f);
        }

        // 산 칸은 빠지고 뒤 장비가 당겨지므로 연타·더블클릭으로 다른 장비까지 사지 않게 잠깐 쉰다 (버그 점검 2026-11-24)
        private const float BuyGap = 0.35f;
        private float buyReadyAt;

        private void Buy(int index)
        {
            if (Time.unscaledTime < buyReadyAt) return;
            var item = shop.Stock[index];
            var current = Equipped(item.Slot);
            if (current != null && current.Id == item.Id)
            {
                feedback = "<color=#ffd84d>이미 끼고 있다.</color>"; // 이어하기로 진열이 처음으로 돌아와도 같은 장비를 또 사지 않게 (버그 점검 2026-11-24)
                GameAudio.PlaySfx("UiClick", 0.4f, 0.02f);
                return;
            }
            var got = shop.Buy(index, current, data.gold, out int cost);
            if (got == null)
            {
                feedback = "<color=#ff8080>골드가 모자라다.</color>";
                GameAudio.PlaySfx("UiClick", 0.4f, 0.02f);
                return;
            }
            if (keySelection > SlotShop(index)) keySelection--; // 산 칸이 빠지며 아래 칸이 당겨진다: Enter가 [다음] 대신 [저장하고 시작 화면]을 누르지 않게
            data.gold -= cost;
            buyReadyAt = Time.unscaledTime + BuyGap;
            data.goldSpent += cost;
            var ids = new List<int>();
            if (data.equipment != null)
                foreach (int id in data.equipment)
                    if (!Enum.IsDefined(typeof(EquipmentId), id) || new EquipmentItem((EquipmentId)id).Slot != got.Slot) ids.Add(id);
            ids.Add((int)got.Id);
            data.equipment = ids.ToArray();
            data.maxHealth += MaxHealthOf(got) - MaxHealthOf(current); // 샘이 새 최대 체력까지 채우게 (체력 자체는 판에서 다시 맞춘다)
            data.health = Mathf.Min(data.health, data.maxHealth);     // 최대 체력이 낮은 장비로 바꾸면 창의 체력도 맞춘다
            feedback = $"{got.Name}을(를) 꼈다" + (current != null ? $" ({current.Name}은(는) 상인에게)" : "") + $" · -{cost}G";
            GameAudio.PlaySfx("Coin", 0.7f, 0.05f);
            RunSave.Save(data);
            keySelection = Mathf.Min(keySelection, SlotCount - 1);
        }

        private static float MaxHealthOf(EquipmentItem item)
        {
            if (item == null) return 0f;
            float sum = 0f;
            foreach (var stat in item.Stats())
                if (stat.Stat == EquipStat.MaxHealth) sum += stat.Value;
            return sum;
        }

        private EquipmentItem Equipped(EquipSlot slot)
        {
            if (data.equipment == null) return null;
            foreach (int id in data.equipment)
            {
                if (!Enum.IsDefined(typeof(EquipmentId), id)) continue;
                var item = new EquipmentItem((EquipmentId)id);
                if (item.Slot == slot) return item;
            }
            return null;
        }

        private string EquippedText()
        {
            var names = new List<string>();
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
            {
                var item = Equipped(slot);
                if (item != null) names.Add(item.Name);
            }
            return names.Count > 0 ? "낀 장비: " + string.Join(", ", names) : "낀 장비 없음";
        }

        // ↑↓·W/S 고르기, Enter·Space 누르기 → 눌린 칸 번호. 없으면 -1.
        private int HandleKeys(Event e)
        {
            if (e.type == EventType.KeyUp && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space))
                activateKeyHeld = false;
            if (e.type != EventType.KeyDown) return -1;
            int step = 0;
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W) step = -1;
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S) step = 1;
            if (step != 0)
            {
                if (keyboardUsed) keySelection = (keySelection + step + SlotCount) % SlotCount;
                keyboardUsed = true;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                e.Use();
                return -1;
            }
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space)
            {
                e.Use();
                if (activateKeyHeld) return -1; // 결과 창에서 누르던 키가 바로 새지 않게
                activateKeyHeld = true;
                keyboardUsed = true;
                GameAudio.PlaySfx("UiClick", 0.7f, 0.04f);
                return keySelection;
            }
            return -1;
        }

        private bool MenuButton(int slot, string label, float height, int keyPressed)
        {
            bool pressed = CuteButton.LayoutButton(label, button, GUILayout.Height(height));
            if (keyboardUsed && slot == keySelection) CuteButton.DrawSelection(GUILayoutUtility.GetLastRect());
            return pressed || keyPressed == slot;
        }
    }
}
