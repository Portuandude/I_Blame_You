using System;
using System.Collections.Generic;
using IBlameYou.Core;
using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 테스트용 화면 UI (IMGUI). 정식 UI 아트가 생기기 전까지 이걸로 확인한다.
    // - 좌상단: 체력·마나 바 (SkillHUD.Bars.cs), A와 B가 쓴/날린 마나, 경험치, 모드
    // - 우상단: 스킬 알림 피드 (실패는 빨간색, 마나 부족이면 최근에 마나를 많이 쓴 쪽을 탓함)
    // - 중앙: A의 방어막 미니게임(W/A/S/D 순서), B의 영창 입력창
    // - 좌상단 아래: 스테이지 이름 꼬리표·골드, 화면 위쪽 가운데: 스테이지 안내 문구
    // - 하단: 조작 안내, 사망/클리어 시 다시 시작 안내 (사망 연출: SkillHUD.Death.cs)
    // - Tab: 레벨업 특성 창 (SkillHUD.Traits.cs), 화면 위 가운데: 보스 체력바 (SkillHUD.Boss.cs)
    // - 화면 가장자리: 체력 30% 이하 경고등 (SkillHUD.LowHealth.cs), Esc: 일시정지 메뉴 (SkillHUD.Pause.cs)
    // - 정예 구역 유물 상자: 유물 고르기 창 (SkillHUD.Relics.cs)
    public partial class SkillHUD : MonoBehaviour
    {
        private const int MaxFeed = 7;
        private const float FeedLifetime = 6f;
        private const float BlameWindow = 6f; // 이 시간 안에 상대가 쓴 마나를 "탓할 거리"로 본다

        private struct FeedEntry
        {
            public float Time;
            public string Text;
            public Color Color;
            public bool Bad; // 실패 알림이면 왼쪽 띠가 berry
        }

        private struct SpendRecord
        {
            public float Time;
            public ManaUser User;
            public float Amount;
        }

        private readonly List<FeedEntry> feed = new List<FeedEntry>();
        private readonly List<SpendRecord> recentSpending = new List<SpendRecord>();

        private HealthSystem health;
        private ManaSystem mana;
        private PlayerControlState control;
        private ElementShieldSkill shieldSkill;
        private ElementShield shield;
        private SpellCaster caster;
        private DevCheats cheats;
        private Action<bool> restart; // (시작 화면으로 돌아갈지)
        private PlayerProgression progression;
        private string chantText = "";
        private bool deathHandled;
        private bool stageCleared;
        private float runStartTime;
        private float clearTime;
        private bool newBestTime;
        private string notice;
        private float noticeTime;

        private const float NoticeDuration = 2.5f;

        private GUIStyle panel;
        private GUIStyle label;
        private GUIStyle small;
        private GUIStyle big;
        private GUIStyle keyBox;
        private GUIStyle input;
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
            shieldSkill = player.GetComponent<ElementShieldSkill>();
            shield = player.GetComponent<ElementShield>();
            caster = player.GetComponent<SpellCaster>();
            progression = player.GetComponent<PlayerProgression>();
            cheats = devCheats;

            mana.ManaSpent += OnManaSpent;
            SkillMessages.Posted += OnSkillMessage;
            StageEvents.Notice += OnStageNotice;
            StageEvents.Cleared += OnStageCleared;
            if (progression != null)
            {
                progression.LeveledUp += OnLeveledUp;
                progression.TraitRaised += OnTraitRaised;
            }
            runStartTime = Time.time;
            RunStats.Reset();
            RunGold.Reset();
            VillageNpc.ResetGreetings(); // 새 판이면 마을 NPC가 다시 인사
            RunGold.Arrived += OnGoldArrived; // 동전이 닿으면 골드 숫자가 톡 튄다 (SkillHUD.Gold.cs)
            RunStats.GoldSpentNow += OnGoldSpent; // 골드를 쓰면 알약 아래 빨간 -N (SkillHUD.Gold.cs)
            EnemyController.Killed += OnEnemyKilled;
            health.Damaged += OnPlayerDamaged;
            InitializeRelics();
            InitializeEquipmentCard();
            InitializeVillage();
        }

        private void OnEnemyKilled(EnemyController enemy, float xp)
        {
            if (!RunStats.Ended) RunStats.Kills++;
            CountKillStreak(enemy); // "N연속!" (SkillHUD.KillStreak.cs)
            if (health != null) RunGold.OnEnemyKilled(enemy, health.transform); // 골드 동전 (Core/RunGold.cs)
        }

        private void OnPlayerDamaged(float amount)
        {
            if (!RunStats.Ended) RunStats.DamageTaken += amount;
        }

        private int RunLevel => progression != null ? progression.Level : 1;

        // 런 시작부터 지금까지 (런 진행 저장용).
        public float RunElapsed => Time.time - runStartTime;

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
            if (mana != null) mana.ManaSpent -= OnManaSpent;
            SkillMessages.Posted -= OnSkillMessage;
            StageEvents.Notice -= OnStageNotice;
            StageEvents.Cleared -= OnStageCleared;
            if (paused) GameAudio.SetMusicMuffled(false); // 일시정지 창이 열린 채 씬이 정리돼도 먹먹함이 남지 않게
            EnemyController.Killed -= OnEnemyKilled;
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
            if (paused) SaveData.SaveVolume(); // 일시정지 메뉴를 연 채로 끝나도 볼륨은 남긴다
            if (traitWindowOpen || paused || relicWindowOpen || equipCardOpen || villageOpen || deathSlowMotion || codexLocked) Time.timeScale = 1f;
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }

        private void OnManaSpent(ManaUser user, float amount, bool wasted)
        {
            if (!wasted) recentSpending.Add(new SpendRecord { Time = Time.time, User = user, Amount = amount });
        }

        private void OnStageNotice(string text)
        {
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
            CloseCodex();
            stageCleared = true;
            RunStats.Ended = true;
            RunSave.Clear(); // 끝난 런은 이어 할 수 없다
            clearTime = Time.time - runStartTime;
            newBestTime = SaveData.RecordClearTime(clearTime);
            SaveData.RecordRecentClear(clearTime, StageEvents.TargetSeconds, StageEvents.MainRooms + StageEvents.SideRooms); // 시작 화면 최근 5판 평균
            SaveData.RecordBlame(mana.WastedBy(ManaUser.PlayerA), mana.WastedBy(ManaUser.PlayerB)); // 시작 화면 최근 탓 기록
            if (progression != null) SaveData.RecordLevel(progression.Level);
            RecordBestGold(); // 최고 골드 (SkillHUD.Gold.cs)
            control.Locked = true;
        }

        private void OnSkillMessage(ManaUser who, string text, bool bad)
        {
            string line = $"[{SkillMessages.RoleName(who)}] {text}";

            // 마나가 모자랐다면, 최근에 상대가 더 많이 썼을 때 상대 탓을 한 줄 덧붙인다.
            if (bad && text.StartsWith("마나 부족") && (who == ManaUser.PlayerA || who == ManaUser.PlayerB))
            {
                var other = who == ManaUser.PlayerA ? ManaUser.PlayerB : ManaUser.PlayerA;
                float mine = RecentSpend(who);
                float theirs = RecentSpend(other);
                if (theirs > mine) line += $"  ← {SkillMessages.RoleName(other)}가 마나 {theirs:0}을 썼다! {SkillMessages.RoleName(other)} 탓!";
            }

            var color = bad ? ColorBad : (who == ManaUser.PlayerA ? ColorA : ColorB);
            feed.Add(new FeedEntry { Time = Time.time, Text = line, Color = color, Bad = bad });
            if (feed.Count > MaxFeed) feed.RemoveAt(0);
        }

        private float RecentSpend(ManaUser user)
        {
            float total = 0f;
            foreach (var record in recentSpending)
            {
                if (record.User == user && Time.time - record.Time <= BlameWindow) total += record.Amount;
            }

            return total;
        }

        private void Update()
        {
            recentSpending.RemoveAll(r => Time.time - r.Time > BlameWindow);
            feed.RemoveAll(f => Time.time - f.Time > FeedLifetime);

            if (health == null) return;

            if (health.IsDead && !deathHandled)
            {
                CloseTraitWindow();
                ClosePauseMenu();
                CloseRelicWindow();
                CloseEquipCard();
                CloseVillage();
                CloseCodex();
                deathHandled = true;
                defeatTime = Time.time - runStartTime; // 게임오버 창 "버틴 시간" (SkillHUD.EndScreen.cs)
                defeatUsualRooms = stageCleared ? -1 : SaveData.RecentDefeatAverage(GameSession.DevMode); // 이번 판을 기록하기 전 평균 ("평소보다 더/덜 갔다"), 클리어 뒤 쓰러짐은 비교 안 함
                // 시작 화면 최근 쓰러진 곳 평균. 클리어 직후 쓰러진 판은 클리어로 이미 기록했으므로 뺀다
                if (!stageCleared)
                {
                    SaveData.RecordRecentDefeat(StageEvents.FarthestDistance); // 방 격자가 아니면(-1) 기록 안 함
                    SaveData.RecordBlame(mana.WastedBy(ManaUser.PlayerA), mana.WastedBy(ManaUser.PlayerB)); // 시작 화면 최근 탓 기록
                }
                if (!stageCleared) RecordBestGold(); // 최고 골드 (SkillHUD.Gold.cs), 클리어 뒤 쓰러짐은 이미 기록
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
            big = new GUIStyle(label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            keyBox = new GUIStyle(GUI.skin.box) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            if (panelTexture != null)
            {
                keyBox.normal.background = panelTexture; // 방어막 미니게임 화살표 칸도 같은 둥근 패널
                keyBox.border = new RectOffset(10, 10, 10, 10);
            }
            input = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleLeft };
            if (panelTexture != null) // 영창 입력 칸도 둥근 패널 (글자는 크림)
            {
                input.normal.background = panelTexture;
                input.focused.background = panelTexture;
                input.hover.background = panelTexture;
                input.border = new RectOffset(10, 10, 10, 10);
                input.padding = new RectOffset(12, 12, 4, 4);
                input.normal.textColor = OutlinedLabel.Cream;
                input.focused.textColor = OutlinedLabel.Cream;
                input.hover.textColor = OutlinedLabel.Cream;
            }
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
            DrawShieldMinigame();
            DrawChantInput();
            DrawHelp();
            DrawTraitWindow();
            DrawRelicWindow();
            DrawEquipCard();
            DrawVillageWindow();
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
            GUILayout.Label($"<color=#73d9ff>A</color> 마나 사용 {mana.SpentBy(ManaUser.PlayerA):0}  (날림 {mana.WastedBy(ManaUser.PlayerA):0})", label);
            GUILayout.Label($"<color=#ffa659>B</color> 마나 사용 {mana.SpentBy(ManaUser.PlayerB):0}  (날림 {mana.WastedBy(ManaUser.PlayerB):0})", label);
            string shieldText = shield != null && shield.Active
                ? $"방어막: {ElementInfo.KoreanName(shield.ActiveElement)} {shield.Remaining:0.0}초"
                : "방어막: 없음";
            GUILayout.Label(shieldText, label);
            string mode = GameSession.DevMode ? "<color=#ffd84d>개발자 모드</color>" : "일반 모드 (로컬)";
            if (cheats != null && cheats.GodMode) mode += "  <color=#ffd84d>무적</color>";
            GUILayout.Label(mode, label);
            GUILayout.EndArea();
        }

        // 오른쪽 위 알림 피드: 줄마다 반투명 둥근 알약 배경 + 왼쪽 색 띠(실패 berry, 성공은 플레이어/항목 색).
        // 새 줄은 오른쪽 화면 밖에서 살짝 튕기며 미끄러져 들어오고, 사라질 때는 흐려진다.
        private const float FeedMaxWidth = 540f;
        private const float FeedLineHeight = 26f;
        private const float FeedSlideTime = 0.3f;
        private static readonly Color FeedBadStripe = new Color(1f, 0.44f, 0.68f); // berry (palette.py)

        private GUIStyle feedText;
        private GUIStyle feedPill;

        private void DrawFeed()
        {
            if (feedText == null)
            {
                feedText = new GUIStyle(label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Clip };
                var pillTexture = Resources.Load<Texture2D>("UI/HUD/Pill");
                feedPill = new GUIStyle();
                feedPill.normal.background = pillTexture != null ? pillTexture : SolidTexture(new Color(0.1f, 0.08f, 0.19f, 0.7f));
                if (pillTexture != null) feedPill.border = new RectOffset(11, 11, 0, 0);
            }

            float y = 12f;
            var previous = GUI.color;
            foreach (var entry in feed)
            {
                float age = Time.time - entry.Time;
                float alpha = Mathf.Clamp01((FeedLifetime - age) / 1.5f);
                var content = new GUIContent(entry.Text);
                float textWidth = feedText.CalcSize(content).x;
                float width = Mathf.Min(FeedMaxWidth, textWidth + 34f);
                float slide = 1f - EaseOutBack(Mathf.Clamp01(age / FeedSlideTime));
                var pill = new Rect(Screen.width - width - 12f + slide * (width + 24f), y, width, FeedLineHeight - 2f);

                GUI.color = new Color(1f, 1f, 1f, alpha);
                DrawStyle(feedPill, pill);

                var stripe = entry.Bad ? FeedBadStripe : entry.Color;
                stripe.a = alpha;
                GUI.color = stripe;
                GUI.DrawTexture(new Rect(pill.x + 7f, pill.y + 5f, 4f, pill.height - 10f), Texture2D.whiteTexture);

                var color = entry.Color;
                color.a = alpha;
                GUI.color = color;
                GUI.Label(new Rect(pill.x + 17f, pill.y, pill.width - 26f, pill.height), content, feedText);
                y += FeedLineHeight;
            }

            GUI.color = previous;
        }

        // 0→1, 끝에서 살짝 넘쳤다가 돌아온다 (튕기는 느낌)
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void DrawShieldMinigame()
        {
            if (shieldSkill == null || !shieldSkill.MinigameActive)
            {
                keyLastProgress = -1; // 다음 미니게임에서 칸 기록을 새로 (SkillHUD.Minigame.cs)
                return;
            }

            TrackShieldKeys(shieldSkill.Progress);
            string sequence = shieldSkill.Sequence;
            const float keySize = 70f;
            const float gap = 12f;
            float totalWidth = sequence.Length * keySize + (sequence.Length - 1) * gap;
            float x = (Screen.width - totalWidth) / 2f;
            float y = Screen.height * 0.28f;

            GUI.Label(new Rect(0f, y - 50f, Screen.width, 40f),
                $"<color=#73d9ff>A</color> {ElementInfo.KoreanName(shieldSkill.PendingElement)} 방어막 — 순서대로 입력!", big);

            for (int i = 0; i < sequence.Length; i++)
                DrawShieldKey(new Rect(x + i * (keySize + gap), y, keySize, keySize), sequence[i].ToString(), i, shieldSkill.Progress);

            // 남은 시간: 캡슐 바 (초록 → berry, SkillHUD.Bars.cs)
            float ratio = Mathf.Clamp01(shieldSkill.TimeLeft / shieldSkill.TimeLimit);
            var timeColor = Color.Lerp(KeyBerry, KeyLeaf, ratio);
            DrawCapsuleBar(new Rect(x + BarGlow, y + keySize + 12f, totalWidth - 2f * BarGlow, BarHeight), ratio, 0f, timeColor, timeColor, null);
        }

        private void DrawChantInput()
        {
            if (caster == null) return;

            if (!caster.ChantOpen)
            {
                if (chantText.Length > 0) chantText = "";
                if (Input.imeCompositionMode != IMECompositionMode.Auto) Input.imeCompositionMode = IMECompositionMode.Auto;
                return;
            }

            Input.imeCompositionMode = IMECompositionMode.On;
            var spell = caster.PendingSpell;

            // Enter/Esc는 입력창보다 먼저 가로챈다. 한글 조합 중인 글자가 남아 있으면 그 Enter는 조합 확정용으로 둔다.
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && string.IsNullOrEmpty(Input.compositionString))
                {
                    string typed = chantText;
                    chantText = "";
                    e.Use();
                    caster.SubmitChant(typed);
                    return;
                }

                if (e.keyCode == KeyCode.Escape)
                {
                    chantText = "";
                    e.Use();
                    caster.CancelChant();
                    return;
                }
            }

            const float width = 620f;
            float x = (Screen.width - width) / 2f;
            float y = Screen.height * 0.62f;
            GUI.Box(new Rect(x - 12f, y - 64f, width + 24f, 150f), GUIContent.none, panel);
            GUI.Label(new Rect(x, y - 58f, width, 30f),
                $"<color=#ffa659>B</color> {spell.Name} ({ElementInfo.KoreanName(spell.Element)}, 마나 {spell.ManaCost:0}) — 영창하라:", label);
            GUI.Label(new Rect(x, y - 30f, width, 30f), ChantProgressText(spell.Chant, chantText, out bool mistake), big); // 맞게 친 부분 aqua

            var field = new Rect(x, y + 8f, width, 40f);
            if (mistake) // 틀린 글자가 있으면 입력 칸 둘레를 berry로 (Enter 누르기 전에 알아채게)
            {
                var previousColor = GUI.color;
                GUI.color = KeyBerry;
                // 테두리만 (입력 칸 배경이 반투명이라 통째로 깔면 칸 전체가 붉게 비친다 — 리뷰 MEDIUM)
                GUI.DrawTexture(new Rect(field.x - 3f, field.y - 3f, field.width + 6f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(field.x - 3f, field.yMax, field.width + 6f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(field.x - 3f, field.y, 3f, field.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(field.xMax, field.y, 3f, field.height), Texture2D.whiteTexture);
                GUI.color = previousColor;
            }

            GUI.SetNextControlName("ChantField");
            chantText = GUI.TextField(field, chantText, 40, input);
            GUI.FocusControl("ChantField");

            // 스킬을 고른 숫자키가 그대로 입력창에 들어가지 않도록, 연 프레임에 들어온 글자는 버린다.
            if (Time.frameCount <= caster.ChantOpenedFrame) chantText = "";

            GUI.Label(new Rect(x, y + 54f, width, 24f), "똑같이 입력하고 Enter (오타면 마나만 소모) · Esc 취소", small);
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
