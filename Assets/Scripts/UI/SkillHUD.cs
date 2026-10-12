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
    // - 좌상단: 체력/마나, A와 B가 쓴/날린 마나, 경험치, 모드
    // - 우상단: 스킬 알림 피드 (실패는 빨간색, 마나 부족이면 최근에 마나를 많이 쓴 쪽을 탓함)
    // - 중앙: A의 방어막 미니게임(방향키 순서), B의 영창 입력창
    // - 하단: 조작 안내, 사망 시 다시 시작 안내
    public class SkillHUD : MonoBehaviour
    {
        private const int MaxFeed = 7;
        private const float FeedLifetime = 6f;
        private const float BlameWindow = 6f; // 이 시간 안에 상대가 쓴 마나를 "탓할 거리"로 본다

        private struct FeedEntry
        {
            public float Time;
            public string Text;
            public Color Color;
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
        private float experience;
        private string chantText = "";
        private bool deathHandled;

        private GUIStyle panel;
        private GUIStyle label;
        private GUIStyle small;
        private GUIStyle big;
        private GUIStyle keyBox;
        private GUIStyle input;

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
            cheats = devCheats;

            mana.ManaSpent += OnManaSpent;
            SkillMessages.Posted += OnSkillMessage;
            EnemyController.Killed += OnEnemyKilled;
        }

        private void OnDestroy()
        {
            if (mana != null) mana.ManaSpent -= OnManaSpent;
            SkillMessages.Posted -= OnSkillMessage;
            EnemyController.Killed -= OnEnemyKilled;
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }

        private void OnManaSpent(ManaUser user, float amount, bool wasted)
        {
            if (!wasted) recentSpending.Add(new SpendRecord { Time = Time.time, User = user, Amount = amount });
        }

        private void OnEnemyKilled(EnemyController enemy, float xp)
        {
            experience += xp;
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
            feed.Add(new FeedEntry { Time = Time.time, Text = line, Color = color });
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
                deathHandled = true;
                control.Locked = true;
                var visual = health.GetComponentInChildren<SpriteRenderer>();
                if (visual != null) visual.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            }

            if (deathHandled)
            {
                if (Input.GetKeyDown(KeyCode.R)) restart?.Invoke(false);
                else if (Input.GetKeyDown(KeyCode.M)) restart?.Invoke(true);
            }
        }

        private void EnsureStyles()
        {
            if (panel != null) return;
            panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 13, wordWrap = true };
            big = new GUIStyle(label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            keyBox = new GUIStyle(GUI.skin.box) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            input = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleLeft };
        }

        private void OnGUI()
        {
            if (health == null) return;
            EnsureStyles();

            DrawStatusPanel();
            DrawFeed();
            DrawShieldMinigame();
            DrawChantInput();
            DrawHelp();
            if (deathHandled) DrawGameOver();
        }

        private void DrawStatusPanel()
        {
            var rect = new Rect(12f, 12f, 360f, 150f);
            GUI.Box(rect, GUIContent.none, panel);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 12f));
            GUILayout.Label($"체력 {health.Current:0}/{health.Max:0}    마나 {mana.Current:0}/{mana.Max:0}" +
                            (mana.Infinite ? " <color=#ffd84d>(무한)</color>" : ""), label);
            GUILayout.Label($"<color=#73d9ff>A</color> 마나 사용 {mana.SpentBy(ManaUser.PlayerA):0}  (날림 {mana.WastedBy(ManaUser.PlayerA):0})", label);
            GUILayout.Label($"<color=#ffa659>B</color> 마나 사용 {mana.SpentBy(ManaUser.PlayerB):0}  (날림 {mana.WastedBy(ManaUser.PlayerB):0})", label);
            string shieldText = shield != null && shield.Active
                ? $"방어막: {ElementInfo.KoreanName(shield.ActiveElement)} {shield.Remaining:0.0}초"
                : "방어막: 없음";
            GUILayout.Label($"경험치 {experience:0}    {shieldText}", label);
            string mode = GameSession.DevMode ? "<color=#ffd84d>개발자 모드</color>" : "일반 모드 (로컬)";
            if (cheats != null && cheats.GodMode) mode += "  <color=#ffd84d>무적</color>";
            GUILayout.Label(mode, label);
            GUILayout.EndArea();
        }

        private void DrawFeed()
        {
            float width = 520f;
            float y = 12f;
            foreach (var entry in feed)
            {
                float alpha = Mathf.Clamp01((FeedLifetime - (Time.time - entry.Time)) / 1.5f);
                var color = entry.Color;
                color.a = alpha;
                var previous = GUI.color;
                GUI.color = color;
                GUI.Label(new Rect(Screen.width - width - 12f, y, width, 24f), entry.Text, label);
                GUI.color = previous;
                y += 24f;
            }
        }

        private void DrawShieldMinigame()
        {
            if (shieldSkill == null || !shieldSkill.MinigameActive) return;

            string sequence = shieldSkill.Sequence;
            const float keySize = 70f;
            const float gap = 12f;
            float totalWidth = sequence.Length * keySize + (sequence.Length - 1) * gap;
            float x = (Screen.width - totalWidth) / 2f;
            float y = Screen.height * 0.28f;

            GUI.Label(new Rect(0f, y - 50f, Screen.width, 40f),
                $"<color=#73d9ff>A</color> {ElementInfo.KoreanName(shieldSkill.PendingElement)} 방어막 — 순서대로 입력!", big);

            for (int i = 0; i < sequence.Length; i++)
            {
                var previous = GUI.color;
                GUI.color = i < shieldSkill.Progress ? new Color(0.4f, 1f, 0.5f) : (i == shieldSkill.Progress ? Color.yellow : Color.white);
                GUI.Box(new Rect(x + i * (keySize + gap), y, keySize, keySize), sequence[i].ToString(), keyBox);
                GUI.color = previous;
            }

            float ratio = Mathf.Clamp01(shieldSkill.TimeLeft / shieldSkill.TimeLimit);
            GUI.Box(new Rect(x, y + keySize + 10f, totalWidth, 10f), GUIContent.none);
            var bar = GUI.color;
            GUI.color = Color.Lerp(ColorBad, new Color(0.4f, 1f, 0.5f), ratio);
            GUI.Box(new Rect(x, y + keySize + 10f, totalWidth * ratio, 10f), GUIContent.none);
            GUI.color = bar;
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
            GUI.Label(new Rect(x, y - 30f, width, 30f), $"<b>{spell.Chant}</b>", big);

            GUI.SetNextControlName("ChantField");
            chantText = GUI.TextField(new Rect(x, y + 8f, width, 40f), chantText, 40, input);
            GUI.FocusControl("ChantField");

            // 스킬을 고른 숫자키가 그대로 입력창에 들어가지 않도록, 연 프레임에 들어온 글자는 버린다.
            if (Time.frameCount <= caster.ChantOpenedFrame) chantText = "";

            GUI.Label(new Rect(x, y + 54f, width, 24f), "똑같이 입력하고 Enter (오타면 마나만 소모) · Esc 취소", small);
        }

        private void DrawHelp()
        {
            string help = GameSession.DevMode
                ? "A: A/D 이동 · Space 점프 · Shift 달리기 · L.Ctrl 대쉬 · 방향키+Enter 순간이동 · 1~5 방어막    " +
                  "B: 좌클릭 평타 · 6~9,0 공격 스킬    F1 무적 · F2 마나 무한 · F3 적 처치 · F4 회복"
                : "A: A/D 이동 · Space 점프 · Shift 달리기 · L.Ctrl 대쉬 · 방향키+Enter 순간이동 · 넘패드 1~5 방어막(→W/A/S/D)    " +
                  "B: 좌클릭 평타 · 1~5 공격 스킬(→영창 타이핑+Enter)";
            GUI.Label(new Rect(12f, Screen.height - 30f, Screen.width - 24f, 24f), help, small);
        }

        private void DrawGameOver()
        {
            var rect = new Rect((Screen.width - 520f) / 2f, Screen.height * 0.35f, 520f, 130f);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Label(new Rect(rect.x, rect.y + 14f, rect.width, 40f), "쓰러졌다! 누구 탓이지?", big);
            GUI.Label(new Rect(rect.x, rect.y + 60f, rect.width, 30f),
                $"A가 날린 마나 {mana.WastedBy(ManaUser.PlayerA):0} · B가 날린 마나 {mana.WastedBy(ManaUser.PlayerB):0}",
                new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(rect.x, rect.y + 92f, rect.width, 30f), "R: 다시 시작 · M: 시작 화면",
                new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
