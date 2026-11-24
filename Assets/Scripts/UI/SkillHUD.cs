using System;
using System.Collections.Generic;
using IBlameYou.Core;
using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;
using GUI = IBlameYou.Net.NetGUI; // HUD를 온라인 친구 화면에도 (Net/NetGUI)
using GUILayout = IBlameYou.Net.NetGUILayout;

namespace IBlameYou.UI
{
    // 테스트용 화면 UI (IMGUI). 정식 UI 아트가 생기기 전까지 이걸로 확인한다.
    // - 좌상단: 체력·마나 바 (SkillHUD.Bars.cs), A와 B가 쓴/날린 마나, 경험치, 모드
    // - 우상단: 스킬 알림 피드 (실패는 빨간색)
    // - 중앙: B의 마법진 그리기 (SkillHUD.MagicCircle.cs)
    // - 좌상단 아래: 스테이지 이름 꼬리표·골드, 화면 위쪽 가운데: 스테이지 안내 문구
    // - 하단: 조작 안내, 사망/클리어 시 다시 시작 안내 (사망 연출: SkillHUD.Death.cs)
    // - Tab: 레벨업 특성 창 (SkillHUD.Traits.cs), 화면 위 가운데: 보스 체력바 (SkillHUD.Boss.cs)
    // - 화면 가장자리: 체력 30% 이하 경고등 (SkillHUD.LowHealth.cs), Esc: 일시정지 메뉴 (SkillHUD.Pause.cs)
    // - 정예 구역 유물 상자: 유물 고르기 창 (SkillHUD.Relics.cs)
    public partial class SkillHUD : MonoBehaviour
    {
        private const int MaxFeed = 7;
        private const float FeedLifetime = 6f;
        private readonly List<FeedEntry> feed = new List<FeedEntry>();

        private HealthSystem health;
        private ManaSystem mana;
        private PlayerControlState control;
        private SpellCaster caster;
        private DevCheats cheats;
        private Action<bool> restart; // (시작 화면으로 돌아갈지)
        private PlayerProgression progression;
        private bool deathHandled;
        private bool stageCleared;
        private float runStartTime;
        private float stageStartTime;  // 이 스테이지를 시작한 때 (판 시간은 runStartTime, 앞 스테이지까지 이어짐)
        private float stageTime;       // 클리어 때 이 스테이지만 걸린 시간 (결과 창 목표 대비 줄)
        private Action<bool> goToRest; // 스테이지 1~4 클리어 뒤 결과 창 버튼: 쉼터로 (false) / 저장하고 시작 화면 (true)
        private float clearTime;
        private bool newBestTime;
        private string notice;
        private float noticeTime;

        private const float NoticeDuration = 2.5f;

        private GUIStyle panel;
        private GUIStyle label;
        private GUIStyle small;
        private GUIStyle smallCenter; // 마법진 아래 안내 줄
        // OnGUI마다 new GUIStyle을 만들지 않도록 자주 쓰는 변형을 한 번만 만든다 (GUIStyle은 네이티브 객체라 프레임마다 만들면 쓰레기·종료자 부담)
        private GUIStyle labelCenter;
        private GUIStyle labelCenterTint;   // 글자색을 그릴 때마다 바꿔 쓰는 가운데 줄 (결과 창 기록 비교 줄)
        private GUIStyle smallRight;
        private GUIStyle smallRowLeft;      // 한 줄·왼쪽 (유물 창 가진 유물 줄)
        private GUIStyle smallRowRight;
        private GUIStyle smallLine;         // 한 줄·왼쪽·여백 없음 (결과 창 유물·장비 줄)
        private GUIStyle smallLineCenterCream; // 한 줄·가운데·넘침 허용·크림색 (결과 창 탐험·사건·최애 줄)
        private GUIStyle smallLineRightTint;   // 한 줄·오른쪽·넘침 허용, 글자색은 그릴 때 정함 (결과 창 골드 아래 줄)
        private GUIStyle tipWrap;           // 결과 창 말풍선 (줄바꿈)
        private GUIStyle tipLine;           // 결과 창 말풍선 (한 줄, 폭 재기·장비 이름)
        private GUIStyle big;
        private GUIStyle menuButton;  // 일시정지 메뉴 큰 버튼 (CuteButton.cs)
        private GUIStyle cardButton;  // 유물·특성 창 작은 버튼

        private static readonly Color ColorA = new Color(0.45f, 0.85f, 1f);
        private static readonly Color ColorB = new Color(1f, 0.65f, 0.35f);
        private static readonly Color ColorBad = new Color(1f, 0.35f, 0.35f);

        public void Initialize(GameObject player, DevCheats devCheats, Action<bool> restartCallback)
        {
            restart = restartCallback;
            health = player.GetComponent<HealthSystem>();
            mana = player.GetComponent<ManaSystem>();
            control = player.GetComponent<PlayerControlState>();
            caster = player.GetComponent<SpellCaster>();
            progression = player.GetComponent<PlayerProgression>();
            cheats = devCheats;

            SkillMessages.Posted += OnSkillMessage;
            StageEvents.Notice += OnStageNotice;
            StageEvents.Cleared += OnStageCleared;
            if (progression != null)
            {
                progression.LeveledUp += OnLeveledUp;
                progression.TraitRaised += OnTraitRaised;
            }
            runStartTime = Time.time;
            stageStartTime = Time.time;
            RunStats.Reset();
            RunGold.Reset();
            VillageNpc.ResetGreetings(); // 새 판이면 마을 NPC가 다시 인사
            RunGold.Arrived += OnGoldArrived; // 동전이 닿으면 골드 숫자가 톡 튄다 (SkillHUD.Gold.cs)
            RunStats.GoldSpentNow += OnGoldSpent; // 골드를 쓰면 알약 아래 빨간 -N (SkillHUD.Gold.cs)
            EnemyController.Killed += OnEnemyKilled;
            TreasureGoblinController.Hit += OnTreasureGoblinHit;
            health.Damaged += OnPlayerDamaged;
            InitializeRelics();
            InitializeEquipmentCard();
            InitializeVillage();
            InitializeChoice(); // A·B 선택지 룰렛 (SkillHUD.Choice.cs)
        }

        // 스테이지 사이 쉼터로 가는 길 (PlaytestBootstrap). 없으면 클리어는 언제나 판 끝 (도시로).
        public void SetRestCallback(Action<bool> callback) => goToRest = callback;

        // 지금 클리어가 판 끝이 아니라 쉼터로 이어지는지 (스테이지 1~4, 기능 테스트 제외 — Chunks/RunFlowRule)
        private bool RestNext => stageCleared && goToRest != null && RunFlowRule.HasRest(GameSession.Stage, GameSession.FeatureTest);

        private void OnEnemyKilled(EnemyController enemy, float xp)
        {
            if (!RunStats.Ended) RunStats.Kills++;
            CountKillStreak(enemy); // "N연속!" (SkillHUD.KillStreak.cs)
            if (health != null) RunGold.OnEnemyKilled(enemy, health.transform); // 골드 동전 (Core/RunGold.cs)
            // 영혼석 (도시 화폐): 보스가 되풀이해 소환하는 적은 없음. 보스는 클리어와 같은 순간이라 판이 끝난 뒤에도 받는다 (골드와 같은 규칙)
            if (enemy != null && enemy.GetComponent<BossSummon>() == null && (!RunStats.Ended || enemy is SlimeKingController))
                RunStats.AddHeartDust(HeartDustRule.ForKill(RunGold.RankOf(enemy)) * RunGold.MidBossShares(enemy)); // 프리즘 젤리는 기사 둘 몫
        }

        private void OnTreasureGoblinHit(TreasureGoblinController goblin)
        {
            if (health != null) RunGold.OnTreasureGoblinHit(goblin, health.transform); // 맞힐 때마다 금화 조금 (Core/RunGold.cs)
        }

        private void OnPlayerDamaged(float amount)
        {
            if (!RunStats.Ended) RunStats.DamageTaken += amount;
        }

        private int RunLevel => progression != null ? progression.Level : 1;

        // 런 시작부터 지금까지 (런 진행 저장용).
        public float RunElapsed => Time.time - runStartTime;
        // 쉼터 저장에 넣을 판 시간: 클리어했으면 클리어 순간에 멈춘 값 (결과 창을 보는 시간은 판 시간이 아니다)
        public float RunElapsedForSave => stageCleared ? clearTime : RunElapsed;

        // 저장된 런을 이어 할 때: Initialize 뒤에 불러 걸린 시간과 런 기록을 되살린다.
        public void ResumeRun(float elapsed, int kills, float damageTaken, int arenasCleared, int gatesOpened)
        {
            runStartTime = Time.time - Mathf.Max(0f, elapsed);
            RunStats.Kills = kills;
            RunStats.DamageTaken = damageTaken;
            RunStats.ArenasCleared = arenasCleared;
            RunStats.GatesOpened = gatesOpened;
        }

        private void OnDestroy()
        {
            SkillMessages.Posted -= OnSkillMessage;
            StageEvents.Notice -= OnStageNotice;
            StageEvents.Cleared -= OnStageCleared;
            if (paused || deathHandled) GameAudio.SetMusicMuffled(false); // 일시정지 창이 열린 채·쓰러진 채 씬이 정리돼도 먹먹함이 남지 않게
            EnemyController.Killed -= OnEnemyKilled;
            TreasureGoblinController.Hit -= OnTreasureGoblinHit;
            RunGold.Arrived -= OnGoldArrived;
            RunStats.GoldSpentNow -= OnGoldSpent;
            if (health != null) health.Damaged -= OnPlayerDamaged;
            if (progression != null)
            {
                progression.LeveledUp -= OnLeveledUp;
                progression.TraitRaised -= OnTraitRaised;
            }
            ShutdownRelics();
            ShutdownEquipmentCard();
            ShutdownVillage();
            ShutdownChoice();
            if (paused) SaveData.SaveVolume(); // 일시정지 메뉴를 연 채로 끝나도 볼륨은 남긴다
            if (traitWindowOpen || paused || relicWindowOpen || equipCardOpen || villageOpen || choiceOpen || deathSlowMotion || codexLocked) Time.timeScale = 1f;
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }

        private void OnStageNotice(string text)
        {
            PushOldNotice(); // 아직 떠 있던 문구는 위로 밀려 올라가며 사라진다 (SkillHUD.Notice.cs)
            notice = text;
            noticeTime = Time.time;
        }

        private void OnStageCleared()
        {
            if (stageCleared || deathHandled) return;
            CloseTraitWindow();
            ClosePauseMenu();
            CloseRelicWindow();
            CloseEquipCard();
            CloseVillage();
            CloseChoice();
            CloseCodex();
            stageCleared = true;
            RunStats.AddHeartDust(HeartDustRule.StageClear); // 스테이지 클리어 영혼석 (아래 RecordClearTime의 PlayerPrefs.Save로 함께 저장)
            RunStats.Ended = true;
            clearTime = Time.time - runStartTime;
            stageTime = Time.time - stageStartTime;
            // 결과 창을 보는 동안 남은 위험물에 쓰러지지 않게 무적 (스테이지 5·기능 테스트 클리어도, 버그 점검 2026-11-24)
            if (health != null) health.GrantGrace(3600f);
            if (RestNext)
            {
                // 스테이지 1~4: 판은 이어진다 → 기록·저장 지우기 없이 결과 창 "쉼터로". 쉼터 들어갈 때 저장한다 (PlaytestBootstrap.GoToRest).
                PlayerPrefs.Save(); // 영혼석
                control.Locked = true;
                return;
            }
            RunSave.Clear(); // 끝난 런은 이어 할 수 없다
            newBestTime = SaveData.RecordClearTime(clearTime);
            SaveData.RecordRecentClear(stageTime, StageEvents.TargetSeconds, StageEvents.RoomCount); // 시작 화면 최근 5판 평균 (스테이지 하나 몫 — 목표 시간 조정용)
            if (progression != null) SaveData.RecordLevel(progression.Level);
            RecordBestGold(); // 최고 골드 (SkillHUD.Gold.cs)
            control.Locked = true;
        }

        private void OnSkillMessage(ManaUser who, string text, bool bad)
        {
            string line = $"[{SkillMessages.RoleName(who)}] {text}";

            var color = bad ? ColorBad : (who == ManaUser.PlayerA ? ColorA : ColorB);
            AddFeed(line, color, bad); // 같은 줄이 이미 있으면 ×N으로 합친다 (SkillHUD.Feed.cs)
        }

        private void Update()
        {
            UpdateFeed(); // 다 된 줄 지우기·줄 위치 따라가기 (SkillHUD.Feed.cs)

            if (health == null) return;

            if (health.IsDead && !deathHandled)
            {
                CloseTraitWindow();
                ClosePauseMenu();
                CloseRelicWindow();
                CloseEquipCard();
                CloseVillage();
                CloseChoice();
                CloseCodex();
                deathHandled = true;
                defeatTime = Time.time - runStartTime; // 게임오버 창 "버틴 시간" (SkillHUD.EndScreen.cs)
                defeatUsualRooms = stageCleared ? -1 : SaveData.RecentDefeatAverage(GameSession.DevMode); // 이번 판을 기록하기 전 평균 ("평소보다 더/덜 갔다"), 클리어 뒤 쓰러짐은 비교 안 함
                // 시작 화면 최근 쓰러진 곳 평균. 클리어 직후 쓰러진 판은 클리어로 이미 기록했으므로 뺀다
                if (!stageCleared) SaveData.RecordRecentDefeat(StageEvents.FarthestDistance); // 방 격자가 아니면(-1) 기록 안 함
                if (!stageCleared) RecordBestGold(); // 최고 골드 (SkillHUD.Gold.cs), 클리어 뒤 쓰러짐은 이미 기록
                if (!stageCleared) SaveData.RecordRevival(); // 숨은 능력치 인과율: 쓰러지면 도시에서 다시 깨어난다 (개발자·기능 테스트 모드, 클리어 뒤 쓰러짐은 안 셈)
                RunStats.Ended = true;
                RunSave.Clear(); // 쓰러진 런은 이어 할 수 없다 (저장 시점으로 되돌려 무르기 방지)
                control.Locked = true;
                BeginDeathSequence(); // 슬로모션, 회색으로 바래기, 화면 어두워짐 (SkillHUD.Death.cs)
            }

            UpdateDeathSequence();

            if (!UpdateCodexInput()) // 도감(C)이 열려 있으면 Esc·Tab은 도감 몫 (SkillHUD.Codex.cs)
            {
                UpdateRelicWindowInput();
                UpdateEquipCardInput();
                UpdateChoiceInput();
                if (!UpdateVillageInput()) UpdatePauseInput(); // 마을 창이 열려 있으면 Esc는 마을 창 몫 (SkillHUD.Village.cs) // 특성 창보다 먼저: 특성 창이 열려 있으면 Esc는 특성 창을 닫는다
                UpdateTraitWindowInput();
            }

            if (GameOverReady || stageCleared)
            {
                if (Input.GetKeyDown(KeyCode.R)) RequestRestart(false);
                else if (Input.GetKeyDown(KeyCode.M)) RequestRestart(true);
                else UpdateEndButtonsInput(); // ←→·A/D 고르기, Enter·Space 누르기 (SkillHUD.EndScreen.cs)
            }
        }

        private void EnsureStyles()
        {
            if (panel != null) return;
            panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            // 귀엽게: 둥근 모서리 + 진한 보라 반투명 + 크림 테두리 (generate_hud.py Panel, 9분할 사방 10픽셀). 없으면 기본 상자
            var panelTexture = Resources.Load<Texture2D>("UI/HUD/Panel");
            if (panelTexture != null)
            {
                panel.normal.background = panelTexture;
                panel.border = new RectOffset(10, 10, 10, 10);
            }
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 13, wordWrap = true };
            smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            big = new GUIStyle(label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            labelCenter = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            labelCenterTint = new GUIStyle(labelCenter);
            smallRight = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
            smallRowLeft = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            smallRowRight = new GUIStyle(smallRowLeft) { alignment = TextAnchor.MiddleRight };
            smallLine = new GUIStyle(small) { wordWrap = false, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) };
            smallLineCenterCream = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            smallLineCenterCream.normal.textColor = OutlinedLabel.Cream;
            smallLineRightTint = new GUIStyle(small) { alignment = TextAnchor.MiddleRight, wordWrap = false, clipping = TextClipping.Overflow };
            tipWrap = new GUIStyle(small) { wordWrap = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 3, 3) };
            tipWrap.normal.textColor = OutlinedLabel.Cream;
            tipLine = new GUIStyle(tipWrap) { wordWrap = false };
            menuButton = CuteButton.Create(20);
            cardButton = CuteButton.Create(15);
        }

        private void OnGUI()
        {
            if (health == null) return;
            EnsureStyles();

            DrawLowHealthWarning(); // 화면 가장자리 효과라 다른 UI보다 먼저(아래에) 그린다
            DrawOrbGuides(); // 화면 밖 보상 구슬 쪽 가장자리 하트 (SkillHUD.OrbGuide.cs)
            DrawEnemyGuides(); // 전투 구역에서 화면 밖 적 쪽 가장자리 붉은 화살표 (SkillHUD.EnemyGuide.cs)
            DrawLastEnemyMark(); // 전투 구역 마지막 적 머리 위 ▼ (SkillHUD.LastEnemy.cs)
            DrawAimWarnings(); // 화면 밖에서 시위를 당기는 궁수 쪽 가장자리 노란 "!" (SkillHUD.EnemyGuide.cs)
            DrawStatusPanel();
            DrawGoldPill(); // 골드 (SkillHUD.Gold.cs)
            DrawStageTag();
            DrawLevelBar();
            DrawRelicList();
            DrawBossBar();
            DrawArenaCount(); // 전투 구역 남은 적 수 (SkillHUD.Arena.cs)
            DrawNotice();
            DrawFeed();
            DrawMagicCircle(); // P2 마법진 그리기 (SkillHUD.MagicCircle.cs)
            DrawHelp();
            DrawMouseSpellTag(); // P2 휠로 고른 공격 스킬을 커서 옆에 (SkillHUD.MouseSpell.cs)
            DrawSpellDial(); // P2 우클릭을 누르고 있는 동안 속성 → 마법 2단계 다이얼 (SkillHUD.SpellDial.cs)
            DrawTraitWindow();
            DrawRelicWindow();
            DrawRelicFly(); // 고른 유물 아이콘이 HUD 칸으로 (SkillHUD.RelicFly.cs)
            DrawEquipCard();
            DrawVillageWindow();
            DrawChoice();
            DrawPauseMenu();
            DrawCodexWindow();
            DrawDeathFade();
            if (deathHandled) DrawGameOver();
            else if (stageCleared) DrawStageClear();
        }

        private void DrawStatusPanel()
        {
            var rect = new Rect(12f, 12f, 360f, 150f);
            GUI.Box(rect, GUIContent.none, panel);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 12f));
            DrawVitalBars(GUILayoutUtility.GetRect(rect.width - 20f, BarIconSize + 2f)); // 체력·마나 바 (SkillHUD.Bars.cs)
            GUILayout.Label($"<color=#73d9ff>{(GameSession.DevMode ? "A" : "P1·A")}</color> 마나 사용 {mana.SpentBy(ManaUser.PlayerA):0}  (날림 {mana.WastedBy(ManaUser.PlayerA):0})", label);
            GUILayout.Label($"<color=#ffa659>{(GameSession.DevMode ? "B" : "P2·B")}</color> 마나 사용 {mana.SpentBy(ManaUser.PlayerB):0}  (날림 {mana.WastedBy(ManaUser.PlayerB):0})", label);
            string buffText = "";
            if (caster != null && caster.TailwindActive) // 순풍 (아카데미 바람 마법): 남은 초, 끝나기 1초 전 깜빡임
            {
                float left = caster.TailwindRemaining;
                bool blinkOff = left < 1f && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
                buffText = blinkOff ? " " : $"<color=#8cffb3>순풍 {left:0.0}초 (마법진 한 단계 쉬움)</color>";
            }
            GUILayout.Label(buffText.Length > 0 ? buffText : " ", label);
            string mode = GameSession.DevMode ? "<color=#ffd84d>개발자 모드</color>" : "일반 모드 (로컬)";
            if (cheats != null && cheats.GodMode) mode += "  <color=#ffd84d>무적</color>";
            GUILayout.Label(mode, label);
            GUILayout.EndArea();
        }

        // DrawHelp: 아래 조작 도움말 줄 (SkillHUD.Help.cs)

        private void DrawGameOver()
        {
            float alpha = GameOverAlpha;
            if (alpha <= 0f) return;
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            DrawEndPanel(false, 600f); // 줄 목록·차례로 나타남·버튼 (SkillHUD.EndScreen.cs)
            GUI.color = previousColor;
        }

        // 스테이지 이름 꼬리표. 진행도 막대(%)는 2026-11-12 사람 결정으로 없앴다 (방을 돌아다니며 탐험 → Trash/Assets/Scripts/UI/SkillHUD.DrawStageBar_20261112.cs.txt).
        private static readonly Rect StageTagRect = new Rect(12f, 168f, 130f, 30f);

        private void DrawStageTag()
        {
            GUI.Box(StageTagRect, GUIContent.none, panel);
            DrawIcon(flagIcon, new Rect(StageTagRect.x + 6f, StageTagRect.y + 4f, 22f, 22f), 1f);
            GUI.Label(new Rect(StageTagRect.x + 32f, StageTagRect.y + 4f, StageTagRect.width - 36f, 24f), StageEvents.StageName, small);
        }

        private void DrawStageClear()
        {
            DrawConfetti(); // 패널 뒤로 별·하트 색종이 (SkillHUD.EndScreen.cs)
            DrawEndPanel(true, 680f); // 줄 목록·차례로 나타남·버튼 (SkillHUD.EndScreen.cs)
        }
    }
}
