using System;
using IBlameYou.Player;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 시작 전 웹툰식 스토리 소개 (2026-11-14 사람 지시, 줄거리 Docs/Story.md 5장 프롤로그).
    // 시작 화면 "플레이 테스트 시작"을 누르면 게임 월드를 만들기 전에 띄운다 (기능 테스트·이어하기·다시 시작은 건너뜀).
    // 컷이 세로로 이어지고 한 컷씩 아래로 넘어가며, 컷마다 말풍선이 차례로 톡 나온다.
    //   - 다음: P1(A) Space 또는 P2(B) 마우스 왼쪽을 짧게 누름 (말풍선이 남았으면 먼저 다 보여 줌, 마지막 컷이면 시작)
    //   - 건너뛰기: P1과 P2가 동시에 꾹 (0.8초) — 한쪽만으로는 건너뛸 수 없다 (RoleInput.StoryButtonA/B)
    // 컷 그림은 Tools/ArtGen/generate_story.py가 만든 Resources/Story/Panel01~07, 글은 여기서 IMGUI로 얹는다.
    public class StoryIntro : MonoBehaviour
    {
        private enum Who { Narration, A, B, Tricks, Both, Sfx }

        private struct Line
        {
            public Who who;
            public string text;
            public Vector2 at; // 컷 안 자리 (0~1, 왼쪽 위 기준, 말풍선 가운데)
            public Line(Who who, string text, float x, float y) { this.who = who; this.text = text; at = new Vector2(x, y); }
        }

        private static readonly Line[][] Panels =
        {
            new[]
            {
                new Line(Who.Narration, "평생 앙숙이던 두 라이벌.", 0.2f, 0.1f),
                new Line(Who.A, "내가 먼저 건넌다!", 0.24f, 0.36f),
                new Line(Who.B, "무슨 소리, 내가 먼저야!", 0.78f, 0.36f),
            },
            new[]
            {
                new Line(Who.Sfx, "빠아앙——!!", 0.5f, 0.2f),
                new Line(Who.Narration, "…그리고 둘은 사이좋게 같은 트럭에 치였다.", 0.5f, 0.88f),
            },
            new[]
            {
                new Line(Who.Narration, "그걸 우연히 구경한 이가 있었으니 — 이 세계의 악신, 트릭스.", 0.5f, 0.1f),
                new Line(Who.Tricks, "오호? 둘이 같이 죽었네?", 0.24f, 0.4f),
                new Line(Who.Tricks, "이거… 재밌겠는데?", 0.78f, 0.4f),
            },
            new[]
            {
                new Line(Who.Tricks, "둘이 한 몸이면 얼마나 싸울까? 낄낄.", 0.5f, 0.13f),
                new Line(Who.Sfx, "꾸욱—", 0.5f, 0.86f),
            },
            new[]
            {
                new Line(Who.Narration, "마법 도시 클라리온, 수정 탑 앞.", 0.22f, 0.1f),
                new Line(Who.A, "다리가… 내 맘대로 움직여!", 0.2f, 0.45f),
                new Line(Who.B, "잠깐, 손은 내 거잖아?!", 0.8f, 0.45f),
            },
            new[]
            {
                new Line(Who.Tricks, "내 놀이판을 전부 깨고 나한테 와 봐.", 0.24f, 0.58f),
                new Line(Who.Tricks, "그럼 떼어 줄지… 생각해 보지.", 0.76f, 0.75f),
            },
            new[]
            {
                new Line(Who.Both, "이게 다 너 때문이야!!", 0.5f, 0.9f),
            },
        };

        private const float LineDelay = 0.55f;   // 말풍선 사이 간격
        private const float FirstLineDelay = 0.35f;
        private const float PopTime = 0.2f;
        private const float TapSeconds = 0.35f;  // 이보다 짧게 눌렀다 떼면 "다음"
        private const float SkipHoldSeconds = 0.8f;
        private const float FadeOutSeconds = 0.35f;

        private static readonly Color AColor = new Color(0.45f, 0.85f, 1f);   // A 하늘색 (FeatureTestTools 알약과 같음)
        private static readonly Color BColor = new Color(1f, 0.65f, 0.35f);   // B 주황
        private static readonly Color TricksColor = new Color(0.85f, 0.75f, 1f); // palette arcane 밝은 쪽

        private Action onDone;
        private Texture2D[] art;
        private Texture2D bubble;
        private GUIStyle bubbleStyle, nameStyle, sfxStyle, titleStyle, hintStyle, narrationStyle;
        private int current;          // 지금 보는 컷
        private float[] revealedAt;   // 컷이 나타난 시각 (-1 = 아직)
        private float[] linesShownAt; // 말풍선을 한꺼번에 보이게 한 시각 (-1 = 차례대로)
        private float scroll, scrollTarget;
        private float skipHeld;       // 둘이 동시에 누른 시간
        private bool comboUsed;       // 둘이 함께 누른 뒤 둘 다 뗄 때까지는 "다음"으로 치지 않는다
        private bool aWasHeld, bWasHeld;
        private float aDownAt, bDownAt;
        private float finishingAt = -1f;
        private bool started;
        private int[] sounded;        // 컷마다 소리를 낸 말풍선 수 (OnGUI는 한 프레임에 여러 번 불리므로 소리는 Update에서)

        public void Begin(Action done)
        {
            onDone = done;
            art = new Texture2D[Panels.Length];
            for (int i = 0; i < art.Length; i++) art[i] = Resources.Load<Texture2D>($"Story/Panel{i + 1:00}");
            bubble = Resources.Load<Texture2D>("Story/Bubble");
            revealedAt = new float[Panels.Length];
            linesShownAt = new float[Panels.Length];
            sounded = new int[Panels.Length];
            for (int i = 0; i < Panels.Length; i++) { revealedAt[i] = -1f; linesShownAt[i] = -1f; }
            // 시작 화면에서 누른 Space·클릭이 아직 눌려 있으면 "다음"으로 치지 않게 뗄 때까지 기다린다
            aWasHeld = RoleInput.StoryButtonA;
            bWasHeld = RoleInput.StoryButtonB;
            comboUsed = aWasHeld || bWasHeld;
            Reveal(0);
            started = true;
        }

        private void Reveal(int index)
        {
            current = index;
            revealedAt[index] = Time.unscaledTime;
            GameAudio.PlaySfx("BookOpen", 0.35f, 0.04f);
        }

        private void Update()
        {
            if (!started || finishingAt >= 0f)
            {
                if (finishingAt >= 0f && Time.unscaledTime - finishingAt >= FadeOutSeconds) Done();
                return;
            }

            bool a = RoleInput.StoryButtonA;
            bool b = RoleInput.StoryButtonB;
            float now = Time.unscaledTime;
            if (a && !aWasHeld) aDownAt = now;
            if (b && !bWasHeld) bDownAt = now;

            if (a && b)
            {
                comboUsed = true;
                skipHeld += Time.unscaledDeltaTime;
                if (skipHeld >= SkipHoldSeconds)
                {
                    GameAudio.PlaySfx("RoomPass", 0.6f);
                    Finish();
                    return;
                }
            }
            else skipHeld = Mathf.Max(0f, skipHeld - Time.unscaledDeltaTime * 2f); // 놓으면 게이지가 빠르게 줄어듦

            if (!comboUsed)
            {
                if (!a && aWasHeld && now - aDownAt <= TapSeconds) Advance();
                else if (!b && bWasHeld && now - bDownAt <= TapSeconds) Advance();
            }
            if (!a && !b) comboUsed = false;
            aWasHeld = a;
            bWasHeld = b;

            PlayLineSounds(now);

            // 마우스 휠로 지난 컷을 다시 볼 수 있다 (지금 컷 아래로는 안 감)
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f) scrollTarget = Mathf.Clamp(scrollTarget - wheel * 60f, 0f, ScrollFor(current));
            scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }

        private void PlayLineSounds(float now)
        {
            int shown = LinesVisible(current, now);
            if (shown <= sounded[current]) return;
            if (shown - sounded[current] == 1) // 한꺼번에 여러 개 보이면 소리는 한 번만
            {
                var who = Panels[current][shown - 1].who;
                if (who == Who.Sfx) GameAudio.PlaySfx("Crit", 0.5f, 0.05f);
                else if (who != Who.Narration) GameAudio.PlaySfx("NpcTalk", 0.35f, 0.08f);
            }
            else GameAudio.PlaySfx("NpcTalk", 0.35f, 0.08f);
            sounded[current] = shown;
        }

        private void Advance()
        {
            if (LinesVisible(current, Time.unscaledTime) < Panels[current].Length)
            {
                linesShownAt[current] = Time.unscaledTime; // 남은 말풍선을 한꺼번에
                return;
            }
            if (current + 1 >= Panels.Length)
            {
                Finish();
                return;
            }
            Reveal(current + 1);
            scrollTarget = ScrollFor(current);
        }

        private void Finish()
        {
            if (finishingAt < 0f) finishingAt = Time.unscaledTime;
        }

        private void Done()
        {
            var done = onDone;
            onDone = null;
            Destroy(this);
            done?.Invoke();
        }

        private int LinesVisible(int panel, float now)
        {
            if (revealedAt[panel] < 0f) return 0;
            if (linesShownAt[panel] >= 0f) return Panels[panel].Length;
            float t = now - revealedAt[panel] - FirstLineDelay;
            if (t < 0f) return 0;
            return Mathf.Min(Panels[panel].Length, 1 + Mathf.FloorToInt(t / LineDelay));
        }

        // 말풍선 k가 나타난 시각 (톡 커지는 연출용)
        private float LineAt(int panel, int k)
        {
            float auto = revealedAt[panel] + FirstLineDelay + k * LineDelay;
            return linesShownAt[panel] >= 0f ? Mathf.Min(auto, linesShownAt[panel]) : auto;
        }

        // ---- 배치 ----

        private float ColumnWidth => Mathf.Min(760f, Screen.width - 40f, (Screen.height - 140f) * 16f / 9f);
        private float PanelHeight => ColumnWidth * 9f / 16f;
        private float Gap => 40f * Scale;
        private float Scale => ColumnWidth / 720f;
        private float TopPad => 30f;

        // 컷 index를 화면 가운데(아래 안내 줄 위)에 두는 스크롤 값
        private float ScrollFor(int index)
        {
            float y = index * (PanelHeight + Gap);
            float center = (Screen.height - 70f - PanelHeight) / 2f;
            return Mathf.Max(0f, y + TopPad - center);
        }

        private void EnsureStyles()
        {
            if (bubbleStyle != null) return;
            bubbleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
            if (bubble != null)
            {
                bubbleStyle.normal.background = bubble;
                bubbleStyle.border = new RectOffset(24, 24, 24, 24);
            }
            bubbleStyle.normal.textColor = OutlinedLabel.Outline;
            nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = false };
            nameStyle.normal.textColor = Color.white;
            sfxStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = false };
            sfxStyle.normal.textColor = Color.white;
            titleStyle = new GUIStyle(sfxStyle);
            narrationStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = false };
            var panelTexture = Resources.Load<Texture2D>("UI/HUD/Panel");
            if (panelTexture != null)
            {
                narrationStyle.normal.background = panelTexture;
                narrationStyle.border = new RectOffset(10, 10, 10, 10);
            }
            narrationStyle.normal.textColor = OutlinedLabel.Cream;
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 15 };
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        private void OnGUI()
        {
            if (!started) return;
            EnsureStyles();
            float now = Time.unscaledTime;
            GUI.depth = -10;
            var previousColor = GUI.color;

            GUI.color = new Color(0.06f, 0.05f, 0.08f, 1f); // palette outline 계열 바탕
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float k = Scale;
            bubbleStyle.fontSize = Mathf.RoundToInt(19f * k);
            bubbleStyle.padding = new RectOffset(Mathf.RoundToInt(16f * k), Mathf.RoundToInt(16f * k), Mathf.RoundToInt(10f * k), Mathf.RoundToInt(10f * k));
            nameStyle.fontSize = Mathf.RoundToInt(15f * k);
            sfxStyle.fontSize = Mathf.RoundToInt(44f * k);
            titleStyle.fontSize = Mathf.RoundToInt(64f * k);
            narrationStyle.fontSize = Mathf.RoundToInt(16f * k);
            narrationStyle.padding = new RectOffset(Mathf.RoundToInt(12f * k), Mathf.RoundToInt(12f * k), Mathf.RoundToInt(8f * k), Mathf.RoundToInt(8f * k));

            float x = (Screen.width - ColumnWidth) / 2f;
            for (int i = 0; i <= current; i++)
            {
                float y = TopPad + i * (PanelHeight + Gap) - scroll;
                if (y > Screen.height || y + PanelHeight < 0f) continue;
                float appear = Mathf.Clamp01((now - revealedAt[i]) / 0.3f);
                float rise = (1f - appear) * 24f * k; // 아래에서 살짝 떠오르며 나타남
                var rect = new Rect(x, y + rise, ColumnWidth, PanelHeight);
                float dim = i == current ? 1f : 0.55f; // 지난 컷은 조금 어둡게
                GUI.color = new Color(dim, dim, dim, appear);
                if (art[i] != null) GUI.DrawTexture(rect, art[i], ScaleMode.StretchToFill);
                else GUI.DrawTexture(rect, Texture2D.whiteTexture); // 그림이 없으면 빈 컷
                GUI.color = new Color(1f, 1f, 1f, appear * (i == current ? 1f : 0.7f));
                if (i == Panels.Length - 1) DrawTitle(rect, now - revealedAt[i]);
                DrawLines(i, rect, now);
            }

            GUI.color = previousColor;
            DrawHint(now);

            if (finishingAt >= 0f) // 끝날 때 어둡게 (게임 화면은 시작 화면처럼 덮개에서 밝아진다)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01((now - finishingAt) / FadeOutSeconds));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = previousColor;
        }

        private void DrawTitle(Rect panel, float age)
        {
            float t = Mathf.Clamp01(age / 0.35f);
            float pop = t < 1f ? Mathf.Lerp(1.6f, 1f, t) : 1f + Mathf.Sin(age * 3f) * 0.02f;
            var rect = new Rect(panel.x, panel.y + panel.height * 0.06f, panel.width, panel.height * 0.24f);
            var matrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), rect.center);
            OutlinedLabel.Draw(rect, "I Blame You!", titleStyle, OutlinedLabel.Cream, OutlinedLabel.Gold, 3f, 3f);
            GUI.matrix = matrix;
        }

        private void DrawLines(int panel, Rect rect, float now)
        {
            int shown = LinesVisible(panel, now);
            float k = Scale;
            for (int j = 0; j < shown; j++)
            {
                var line = Panels[panel][j];
                float age = now - LineAt(panel, j);
                if (age < 0f) continue;
                float pop = age >= PopTime ? 1f : age < PopTime * 0.6f ? Mathf.Lerp(0.6f, 1.08f, age / (PopTime * 0.6f)) : Mathf.Lerp(1.08f, 1f, (age - PopTime * 0.6f) / (PopTime * 0.4f));

                var center = new Vector2(rect.x + rect.width * line.at.x, rect.y + rect.height * line.at.y);
                var matrix = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), center);
                switch (line.who)
                {
                    case Who.Sfx:
                    {
                        var r = new Rect(center.x - rect.width / 2f, center.y - 30f * k, rect.width, 60f * k);
                        OutlinedLabel.Draw(r, line.text, sfxStyle, OutlinedLabel.Gold, new Color(1f, 0.44f, 0.68f), 3f, 3f);
                        break;
                    }
                    case Who.Narration:
                    {
                        var content = new GUIContent(line.text);
                        float w = Mathf.Min(rect.width * 0.9f, narrationStyle.CalcSize(content).x + 4f);
                        float h = narrationStyle.CalcHeight(content, w);
                        var r = ClampInside(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), rect);
                        GUI.Box(r, content, narrationStyle);
                        break;
                    }
                    default:
                        DrawBubble(line, center, rect, k);
                        break;
                }
                GUI.matrix = matrix;
            }
        }

        private void DrawBubble(Line line, Vector2 center, Rect panel, float k)
        {
            var content = new GUIContent(line.text);
            float maxW = panel.width * 0.42f;
            float w = Mathf.Min(maxW, bubbleStyle.CalcSize(content).x + 8f);
            float h = Mathf.Max(44f * k, bubbleStyle.CalcHeight(content, w));
            var r = ClampInside(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), panel);

            Color tint = line.who == Who.Tricks ? TricksColor : Color.white;
            var previousBg = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            GUI.Box(r, content, bubbleStyle);
            GUI.backgroundColor = previousBg;

            string name; Color color;
            switch (line.who)
            {
                case Who.A: name = "아루 (A)"; color = AColor; break;
                case Who.B: name = "벨 (B)"; color = BColor; break;
                case Who.Tricks: name = "트릭스"; color = TricksColor; break;
                default: name = "아루 & 벨"; color = OutlinedLabel.Gold; break;
            }
            var nameRect = new Rect(r.x + 8f * k, r.y - 20f * k, r.width, 22f * k);
            OutlinedLabel.Draw(nameRect, name, nameStyle, color, OutlinedLabel.Outline, 1f, 1.5f);
        }

        private static Rect ClampInside(Rect r, Rect panel)
        {
            const float margin = 8f;
            r.x = Mathf.Clamp(r.x, panel.x + margin, Mathf.Max(panel.x + margin, panel.xMax - margin - r.width));
            r.y = Mathf.Clamp(r.y, panel.y + margin + 18f, Mathf.Max(panel.y + margin, panel.yMax - margin - r.height));
            return r;
        }

        // 아래 안내 줄: 다음 / 둘이 같이 꾹 = 건너뛰기 (누르는 쪽 점이 켜지고 게이지가 찬다)
        private void DrawHint(float now)
        {
            var bar = new Rect(0f, Screen.height - 62f, Screen.width, 62f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool a = RoleInput.StoryButtonA, b = RoleInput.StoryButtonB;
            string dotA = a ? "<color=#73d9ff>●</color>" : "<color=#73d9ff80>○</color>";
            string dotB = b ? "<color=#ffa659>●</color>" : "<color=#ffa65980>○</color>";
            string next = current + 1 >= Panels.Length ? "시작" : "다음";
            GUI.Label(new Rect(0f, bar.y + 4f, Screen.width, 26f),
                $"<b>Space</b> 또는 <b>클릭</b>: {next}      건너뛰기: {dotA} <b><color=#73d9ff>P1 Space</color></b> + {dotB} <b><color=#ffa659>P2 클릭</color></b> 동시에 꾹", hintStyle);

            float fill = Mathf.Clamp01(skipHeld / SkipHoldSeconds);
            var gauge = new Rect(Screen.width / 2f - 120f, bar.y + 36f, 240f, 10f);
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(gauge, Texture2D.whiteTexture);
            if (fill > 0f)
            {
                GUI.color = OutlinedLabel.Gold;
                GUI.DrawTexture(new Rect(gauge.x, gauge.y, gauge.width * fill, gauge.height), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            // 컷 진행 점 (오른쪽 아래)
            string dots = "";
            for (int i = 0; i < Panels.Length; i++) dots += i == current ? "<color=#ffd84d>●</color> " : i < current ? "<color=#ffffffa0>●</color> " : "<color=#ffffff40>○</color> ";
            GUI.Label(new Rect(Screen.width - 220f, bar.y + 30f, 210f, 24f), dots, hintStyle);
        }
    }
}
