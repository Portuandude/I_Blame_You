using System;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 런 사이 도시 클라리온 — 메뉴 창 (Docs/CityDesign.md 6절 2단계, 2026-11-15 사람 결정 "도시 방식은 C": 메뉴 먼저, 걸어 다니는 광장은 베타 뒤).
    // 시작 화면 "시작" → 스토리 소개 → 이 창 → 출발 → 판. 판이 끝나면 결과 창 "도시로 (R)" → 이 창.
    // 수정 탑(영구 능력치, 3단계)·아카데미(새 마법, 4단계)는 자리만 있고 아직 닫혀 있다. 기능 테스트는 이 창을 거치지 않는다.
    // IMGUI로 그린다 (시작 화면과 같은 둥근 패널·젤리 버튼).
    public class CityMenu : MonoBehaviour
    {
        private Action onDepart;
        private Action onBack;
        private GUIStyle title;
        private GUIStyle body;
        private GUIStyle small;
        private GUIStyle button;
        private GUIStyle panel;
        private int keySelection = SlotDepart; // 0 수정 탑 · 1 출발 · 2 시작 화면 (닫힌 아카데미는 고를 수 없다)
        private bool keyboardUsed;
        private bool activateKeyHeld; // 스토리 소개를 Space로 넘기고 누르고 있으면 그 키가 바로 출발로 새지 않게
        private bool firstFrame = true;
        private bool done;
        private float openedAt = -1f;
        private const int SlotTower = 0;
        private const int SlotDepart = 1;
        private const int SlotBack = 2;
        private const int SlotCount = 3;
        private const int EscapePressed = 100;
        private bool towerOpen;       // 수정 탑 창 (도시 3단계)
        private int towerSelection;   // 0~3 항목 · 4 돌아가기
        private float towerFlashAt = -10f; // 바친 순간 (창 반짝)
        private int towerFlashRow = -1;
        private const int TowerSlotCount = CrystalTowerRule.StatCount + 1;

        public void Initialize(Action departCallback, Action backCallback)
        {
            onDepart = departCallback;
            onBack = backCallback;
            GameAudio.PlayMusic("Menu");
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
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
            button = CuteButton.Create(20);
        }

        private void OnGUI()
        {
            if (done) return;
            EnsureStyles();
            if (firstFrame)
            {
                firstFrame = false;
                activateKeyHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
            }
            if (openedAt < 0f) openedAt = Time.unscaledTime;

            const float width = 560f;
            float height = Mathf.Min(520f, Screen.height - 20f);
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.3f);
            var screenMatrix = GUI.matrix;
            float pop = t >= 1f ? 1f : Mathf.Lerp(0.88f, 1f, Mathf.SmoothStep(0f, 1f, t));
            if (pop != 1f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), area.center);
            GUI.Box(area, GUIContent.none, panel);
            GUI.Box(area, GUIContent.none, panel); // 두 겹으로 덜 비치게 (시작 화면과 같은 방식)

            int pressed = HandleKeys(Event.current);
            bool dev = GameSession.DevMode;

            GUILayout.BeginArea(new Rect(area.x + 28f, area.y + 18f, width - 56f, height - 36f));
            GUILayout.Label(towerOpen ? "수정 탑" : "마법 도시 클라리온", title, GUILayout.Height(52f));
            GUILayout.Label(towerOpen ? "마음 가루를 바치면 몸이 튼튼해진다. 바칠수록 수정의 금이 메워진다."
                : "금이 간 수정 탑 아래 광장. 성문 밖에는 괴물이 다가오고 있다.", small);
            GUILayout.Space(10f);
            string mode = dev ? "개발자 모드" : "일반 모드";
            GUILayout.Label($"<color=#f0b8ff><b>마음 가루 {SaveData.HeartDust(dev)}</b></color>  <size=12>({mode})</size>", body);
            if (!towerOpen) GUILayout.Label("괴물을 물리치고 퍼즐을 풀면 쌓인다. 쓰러져도 남는다.", small);
            GUILayout.Space(14f);

            bool depart = false, back = false;
            if (towerOpen) DrawTower(pressed, dev);
            else
            {
                if (MenuButton(SlotTower, $"수정 탑 — 영구 능력치 ({TowerSummary(dev)})", 46f, pressed)) OpenTower();
                GUILayout.Space(6f);
                bool wasEnabled = GUI.enabled;
                GUI.enabled = false; // 4단계에서 연다 (Docs/CityDesign.md 5절)
                CuteButton.LayoutButton("아카데미 — 새 마법 배우기 (준비 중)", button, GUILayout.Height(46f));
                GUI.enabled = wasEnabled;
                GUILayout.Space(14f);
                depart = MenuButton(SlotDepart, "출발 — 성문 밖 스테이지 1로", 54f, pressed);
                GUILayout.Space(6f);
                back = MenuButton(SlotBack, "시작 화면 (Esc)", 40f, pressed);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(towerOpen ? "↑↓ 고르기 · Enter 바치기 · Esc 돌아가기" : "↑↓ 고르기 · Enter 누르기", small);
            GUILayout.EndArea();
            GUI.matrix = screenMatrix;

            if (towerOpen) return;
            if (depart) Depart();
            else if (back || pressed == EscapePressed) Back();
        }

        // ↑↓·W/S 고르기, Enter·Space 누르기 → 눌린 칸 번호, Esc → EscapePressed. 없으면 -1. 수정 탑 창이 열려 있으면 그 창의 칸.
        private int HandleKeys(Event e)
        {
            if (e.type == EventType.KeyUp && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space))
                activateKeyHeld = false;
            if (e.type != EventType.KeyDown) return -1;
            if (e.keyCode == KeyCode.Escape)
            {
                e.Use();
                if (towerOpen)
                {
                    CloseTower();
                    return -1;
                }
                return EscapePressed;
            }
            int step = 0;
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W) step = -1;
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S) step = 1;
            if (step != 0)
            {
                if (towerOpen) towerSelection = (towerSelection + step + TowerSlotCount) % TowerSlotCount;
                else if (keyboardUsed) keySelection = (keySelection + step + SlotCount) % SlotCount;
                keyboardUsed = true;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                e.Use();
                return -1;
            }
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space)
            {
                e.Use();
                if (activateKeyHeld) return -1; // 누른 채 키 반복
                activateKeyHeld = true;
                keyboardUsed = true;
                GameAudio.PlaySfx("UiClick", 0.7f, 0.04f);
                return towerOpen ? towerSelection : keySelection;
            }
            return -1;
        }

        private bool MenuButton(int slot, string label, float height, int keyPressed)
        {
            bool pressed = CuteButton.LayoutButton(label, button, GUILayout.Height(height));
            if (keyboardUsed && slot == keySelection) CuteButton.DrawSelection(GUILayoutUtility.GetLastRect());
            return pressed || keyPressed == slot;
        }

        // ---- 수정 탑 창 (Docs/CityDesign.md 4절): 4항목 × 5단계, 줄마다 단계 점·지금/다음 값·바치기 버튼 ----

        private void OpenTower()
        {
            towerOpen = true;
            towerSelection = 0;
        }

        private void CloseTower()
        {
            towerOpen = false;
            keySelection = SlotTower;
        }

        private static string TowerSummary(bool dev)
        {
            int total = 0;
            for (int i = 0; i < CrystalTowerRule.StatCount; i++) total += SaveData.TowerLevel((TowerStat)i, dev);
            return $"{total}/{CrystalTowerRule.StatCount * CrystalTowerRule.MaxLevel}단계";
        }

        private void DrawTower(int pressed, bool dev)
        {
            for (int i = 0; i < CrystalTowerRule.StatCount; i++)
            {
                var stat = (TowerStat)i;
                int level = SaveData.TowerLevel(stat, dev);
                int cost = CrystalTowerRule.Cost(stat, level);
                bool canBuy = CrystalTowerRule.CanBuy(stat, level, SaveData.HeartDust(dev));
                string dots = new string('●', level) + new string('○', CrystalTowerRule.MaxLevel - level);
                string now = level > 0 ? CrystalTowerRule.BonusText(stat, level) : "+0";
                string next = cost < 0 ? "최대" : $"→ {CrystalTowerRule.BonusText(stat, level + 1)}";

                GUILayout.BeginHorizontal();
                var rowColor = Time.unscaledTime - towerFlashAt < 0.4f && towerFlashRow == i ? "#fff3a0" : "#ffffff";
                GUILayout.Label($"<color={rowColor}><b>{CrystalTowerRule.Name(stat)}</b>  <color=#f0b8ff>{dots}</color>  {now} {next}</color>",
                    new GUIStyle(body) { alignment = TextAnchor.MiddleLeft }, GUILayout.Height(40f));
                bool wasEnabled = GUI.enabled;
                GUI.enabled = canBuy;
                bool buy = CuteButton.LayoutButton(cost < 0 ? "완성" : $"바치기 {cost}", button, GUILayout.Width(150f), GUILayout.Height(40f));
                if (keyboardUsed && towerSelection == i) CuteButton.DrawSelection(GUILayoutUtility.GetLastRect());
                GUI.enabled = wasEnabled;
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
                if (buy || pressed == i) BuyTower(stat, i, dev);
            }
            GUILayout.Space(10f);
            bool back = CuteButton.LayoutButton("돌아가기 (Esc)", button, GUILayout.Height(40f));
            if (keyboardUsed && towerSelection == CrystalTowerRule.StatCount) CuteButton.DrawSelection(GUILayoutUtility.GetLastRect());
            if (back || pressed == CrystalTowerRule.StatCount) CloseTower();
        }

        private void BuyTower(TowerStat stat, int row, bool dev)
        {
            if (!SaveData.BuyTowerLevel(stat, dev))
            {
                GameAudio.PlaySfx("UiClick", 0.3f, 0.02f); // 가루가 모자라거나 최대 (Enter로 눌렀을 때만 여기 온다)
                return;
            }
            towerFlashAt = Time.unscaledTime;
            towerFlashRow = row;
            GameAudio.PlaySfx("RunStart", 0.5f); // 맑은 "따-링" — 수정에 가루가 스며드는 소리
        }

        // 출발: 시작 화면의 출발과 같은 "따-링!" + 어두운 덮개에서 밝아지며 판 시작 (ScreenFlash.StartFade는 월드 카메라가 생긴 뒤).
        private void Depart()
        {
            done = true;
            Destroy(this);
            onDepart?.Invoke();
            GameAudio.PlaySfx("RunStart", 0.8f);
            ScreenFlash.StartFade();
        }

        private void Back()
        {
            done = true;
            Destroy(this);
            onBack?.Invoke();
        }
    }
}
