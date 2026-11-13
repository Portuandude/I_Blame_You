using System;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 게임 시작 전 화면 (기획서 2.8/2.9). 개발자 모드를 켜고 끄고, 볼륨을 맞추고, 로컬 테스트를 시작한다.
    // 설정과 최고 기록은 SaveData(PlayerPrefs)에 남는다.
    // 온라인(데디케이티드 서버)은 아직 없어 버튼만 자리를 잡아 둔다.
    // IMGUI로 그린다 (버튼은 UI/CuteButton.cs의 둥근 그림).
    public class StartMenu : MonoBehaviour
    {
        private Action onStart;
        private Action onContinue;
        private RunSave.Data saved; // 이어 할 수 있는 런 (없으면 null)
        private bool confirmNewRun;  // 저장이 있을 때 "새로 시작"은 한 번 더 눌러야 한다
        private bool devMode;
        private float musicVolume;
        private float sfxVolume;
        private GUIStyle title;
        private GUIStyle body;
        private GUIStyle recordSmall; // 창이 낮을 때 기록 줄용 작은 글씨
        private bool recordsExpanded; // 접힌 기록 줄을 펼쳤는지 (H 또는 클릭)
        private Vector2 recordScroll;
        private bool recordsTogglePending;
        private readonly DisplaySettingsWindow display = new DisplaySettingsWindow(); // 화면 설정 창 (일시정지 메뉴와 같은 창)
        private readonly RelicCodexWindow codex = new RelicCodexWindow(); // 유물 도감 창 (C 또는 도감 줄 클릭). 게임 중에도 같은 창 (SkillHUD.Codex)
        private GUIStyle button;
        private GUIStyle toggle;
        private GUIStyle panel;    // 둥근 패널 (HUD와 같은 Resources/UI/HUD/Panel.png)
        private Texture2D sparkle; // 제목 둘레 반짝이 별 (HUD 마나 별 그림)
        private int keySelection;   // 키보드로 고른 아래 큰 버튼 (0부터: 이어하기가 있으면 이어하기, 시작, 기능 테스트)
        private bool keyboardUsed;  // 화살표를 눌렀으면 금색 테두리를 보인다 (마우스만 쓰면 안 보인다)
        private float openedAt = -1f; // 패널이 처음 그려진 시각 (톡 커지는 연출)
        private const float PopTime = 0.3f;
        private float[] letterWidths;  // 제목 글자별 폭 (글자마다 물결치게 따로 그린다)
        private int keyActivate = -1;
        private Texture2D glow;        // 반딧불 둥근 빛 (한 번 만든다)
        private Vector4[] fireflies;   // 반딧불마다 (가로 자리 0~1, 처음 높이 0~1, 오르는 속도, 깜빡임 위상)
        private const int FireflyCount = 26;
 // Enter·Space로 누른 버튼 (그리는 자리에서 눌린 것으로 처리)

        public void Initialize(Action startCallback, Action continueCallback = null)
        {
            onStart = startCallback;
            onContinue = continueCallback;
            saved = continueCallback != null ? RunSave.Load() : null;
            SaveData.LoadSettings();
            devMode = GameSession.DevMode;
            musicVolume = GameAudio.MusicVolume;
            sfxVolume = GameAudio.SfxVolume;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = false };
            title.normal.textColor = Color.white; // 색은 OutlinedLabel이 GUI.color로 입힌다
            panel = new GUIStyle(GUI.skin.box);
            var panelTexture = Resources.Load<Texture2D>("UI/HUD/Panel");
            if (panelTexture != null)
            {
                panel.normal.background = panelTexture;
                panel.border = new RectOffset(10, 10, 10, 10);
            }
            sparkle = Resources.Load<Texture2D>("UI/HUD/Star");
            body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            body.normal.textColor = Color.white;
            recordSmall = new GUIStyle(body) { fontSize = 13 };
            recordSmall.hover.textColor = new Color(1f, 0.92f, 0.6f); // 접힌 줄은 버튼처럼 눌리니 올리면 노랗게
            button = CuteButton.Create(20); // 둥근 젤리 버튼 (UI/CuteButton.cs)
            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 20 };
        }

        private void OnGUI()
        {
            EnsureStyles();

            const float width = 640f;
            float height = Mathf.Min(640f, Screen.height - 20f); // 낮은 창에서도 버튼이 잘리지 않게
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            if (openedAt < 0f) openedAt = Time.unscaledTime;
            var screenMatrix = GUI.matrix;
            float pop = PopScale();
            DrawFireflies(); // 패널 뒤 배경 (화면 좌표, 톡 배율 전에)
            if (pop != 1f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), area.center); // 도감·화면 설정 창은 원래 크기로 (아래에서 되돌린다)
            GUI.Box(area, GUIContent.none, panel);
            GUI.Box(area, GUIContent.none, panel); // 두 겹으로 덜 비치게 (HUD 창과 같은 방식)

            var e = Event.current;
            if (!display.IsOpen && e.type == EventType.KeyDown && (e.keyCode == KeyCode.C || (codex.IsOpen && e.keyCode == KeyCode.Escape)))
            {
                if (e.keyCode == KeyCode.C && !codex.IsOpen) codex.Show(devMode); // 다시 열면 고른 칸 없이, 메뉴 토글 모드로 시작
                else codex.Close();
                e.Use();
            }
            if (!display.IsOpen && !codex.IsOpen) HandleMenuKeys(e);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !codex.IsOpen && !display.IsOpen; // 도감·화면 설정 창이 열려 있으면 뒤 메뉴는 누를 수 없게

            GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 16f, width - 48f, height - 32f));
            DrawTitle(GUILayoutUtility.GetRect(width - 48f, 60f, GUILayout.Height(60f)));
            GUILayout.Space(4f); // 예전 Label의 아래 여백 (리뷰 LOW)
            GUILayout.Label(IntroText, body);
            GUILayout.Space(12f);

            devMode = GUILayout.Toggle(devMode, "  개발자 모드 (한 사람이 A/B 동시 조작, 미니게임 생략)", toggle);
            GUILayout.Space(8f);
            GUILayout.Label(devMode ? DevModeHelp : NormalModeHelp, body);
            GUILayout.Space(12f);
            musicVolume = VolumeSlider("배경음악", musicVolume);
            sfxVolume = VolumeSlider("효과음", sfxVolume);
            GameAudio.MusicVolume = musicVolume; // 메뉴 음악에 바로 반영
            GameAudio.SfxVolume = sfxVolume;
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (IBlameYou.UI.CuteButton.LayoutButton($"{SaveData.RelicCodexSummary(devMode, IBlameYou.Player.RelicBook.All.Length)} — 보기 (C)", recordSmall))
            {
                codex.Show(devMode);
            }
            GUILayout.FlexibleSpace();
            if (IBlameYou.UI.CuteButton.LayoutButton("화면 설정", recordSmall)) display.Show();
            GUILayout.EndHorizontal();
            DrawRecords(width - 48f, height - 32f);
            GUILayout.FlexibleSpace();

            if (saved != null)
            {
                string mode = saved.devMode ? "개발자 모드" : "일반 모드";
                if (MenuButton(0, $"이어하기 — 레벨 {saved.level} · {SaveData.FormatTime(saved.elapsed)} ({mode})", 44f))
                {
                    GameSession.DevMode = devMode; // 메뉴의 토글이 설정값 (런은 저장된 모드로 진행, PlaytestBootstrap.ContinueWorld)
                    SaveData.SaveSettings();
                    GameSession.Started = true;
                    GameSession.FeatureTest = false;
                    Launch(onContinue);
                }

                GUILayout.Space(4f);
            }

            string startLabel = saved == null ? "플레이 테스트 시작 (일반 판)"
                : confirmNewRun ? "정말 새로 시작? 이어하던 런이 지워진다 (한 번 더 누르기)" : "플레이 테스트 새로 시작";
            bool startPressed = MenuButton(StartSlot, startLabel, 48f);
            if (startPressed && saved != null && !confirmNewRun)
            {
                confirmNewRun = true;
            }
            else if (startPressed)
            {
                StartRun(false); // GUILayout 영역을 닫아야 하므로 return 없이 아래까지 그린다 (예전 시작 버튼과 같음)
            }

            // 기능 테스트 모드: 적 없는 시험용 맵에서 몬스터·유물·협동 기능을 키로 불러 시험 (이어하던 런은 건드리지 않는다)
            GUILayout.Space(4f);
            if (MenuButton(StartSlot + 1, "기능 테스트 시작 (빈 방에서 몬스터·유물·협동 기능 시험, 기록 안 남음)", 40f))
            {
                StartRun(true);
            }

            GUI.enabled = false;
            IBlameYou.UI.CuteButton.LayoutButton("온라인 (데디케이티드 서버) — 준비 중", button, GUILayout.Height(40f));
            GUI.enabled = true;
            GUILayout.EndArea();
            GUI.enabled = wasEnabled;
            GUI.matrix = screenMatrix;
            if (codex.IsOpen) codex.Draw(panel, body, recordSmall, button);
            display.Draw(panel, body, button);
        }

        // 아래 큰 버튼 수: (이어하기) · 시작 · 기능 테스트. 온라인은 준비 중이라 고를 수 없다.
        private int StartSlot => saved != null ? 1 : 0;
        private int MenuButtonCount => StartSlot + 2;

        // 키보드로 아래 큰 버튼 고르기 (↑↓·W/S) · 누르기 (Enter·Space). 일시정지 창과 같은 방식.
        // OnGUI는 Update 뒤에 돌고, 시작하면 만들어지는 플레이어는 다음 프레임부터 Update를 돌아 이 Enter·Space가 순간이동·점프로 새지 않는다.
        private void HandleMenuKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            int step = 0;
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.W) step = -1;
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.S) step = 1;
            if (step != 0)
            {
                // 첫 화살표는 지금 고른 칸(처음엔 맨 위 버튼)을 보여 주기만 한다
                if (keyboardUsed) keySelection = (keySelection + step + MenuButtonCount) % MenuButtonCount;
                keyboardUsed = true;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space)
            {
                keyActivate = Mathf.Clamp(keySelection, 0, MenuButtonCount - 1);
                keyboardUsed = true;
                e.Use();
            }
        }

        // 아래 큰 버튼 하나: 마우스로 눌렀거나 Enter·Space로 골라 눌렀으면 true. 고른 버튼에는 금색 테두리.
        private bool MenuButton(int slot, string label, float height)
        {
            bool pressed = IBlameYou.UI.CuteButton.LayoutButton(label, button, GUILayout.Height(height));
            if (keyboardUsed && slot == keySelection && GUI.enabled) IBlameYou.UI.CuteButton.DrawSelection(GUILayoutUtility.GetLastRect());
            if (pressed) keySelection = slot; // 마우스로 누른 버튼도 고른 칸으로 (새로 시작 확인을 Enter로 이어 누를 수 있게)
            if (keyActivate == slot)
            {
                keyActivate = -1;
                if (GUI.enabled && !pressed)
                {
                    GameAudio.PlaySfx("UiClick", 0.7f, 0.04f); // 마우스로 누를 때와 같은 소리
                    pressed = true;
                }
            }
            return pressed;
        }

        private void StartRun(bool featureTest)
        {
            GameSession.DevMode = devMode;
            SaveData.SaveSettings();
            GameSession.Started = true;
            GameSession.FeatureTest = featureTest;
            Launch(onStart);
        }

        // 출발: 밝은 "따-링!"(Tools/AudioGen RunStart) + 월드를 만든 뒤 어두운 덮개에서 0.5초 동안 밝아지며 시작 (ScreenFlash.StartFade).
        // 덮개는 월드가 만든 카메라에 붙어야 하므로 callback 뒤에 부른다 (Destroy는 프레임 끝에 일어나 이 코드는 끝까지 돈다).
        private void Launch(Action callback)
        {
            Destroy(this);
            callback?.Invoke();
            GameAudio.PlaySfx("RunStart", 0.8f);
            ScreenFlash.StartFade();
        }

        // 제목: 진한 외곽선 + gold 테두리 + 크림 글자, 위아래로 둥실, 둘레에 반짝이 별 4개가 번갈아 깜빡임.
        private static readonly Vector2[] SparkleSpots = { new Vector2(-0.36f, -0.3f), new Vector2(0.38f, -0.25f), new Vector2(-0.3f, 0.4f), new Vector2(0.33f, 0.42f) };

        // 패널이 열린 직후 톡 커지는 배율 (0.85 → 1.04 → 1, 일시정지 창과 같은 모양). 끝났으면 1.
        private float PopScale()
        {
            float t = (Time.unscaledTime - openedAt) / PopTime;
            if (t >= 1f || t < 0f) return 1f;
            return t < 0.6f
                ? Mathf.Lerp(0.85f, 1.04f, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }

        // 패널 뒤 배경: 아래에서 천천히 떠오르며 좌우로 흔들리고 깜빡이는 반딧불 (연두·금빛). Repaint 때만 그린다.
        private void DrawFireflies()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (fireflies == null)
            {
                var random = new System.Random(20261113); // 매번 같은 배치 (화면을 다시 열어도 튀지 않게)
                fireflies = new Vector4[FireflyCount];
                for (int i = 0; i < FireflyCount; i++)
                    fireflies[i] = new Vector4((float)random.NextDouble(), (float)random.NextDouble(),
                        0.025f + 0.035f * (float)random.NextDouble(), (float)random.NextDouble() * Mathf.PI * 2f);
            }
            if (glow == null) glow = MakeGlow();

            float time = Time.unscaledTime;
            float span = Screen.height + 40f; // 화면 아래 밖 20픽셀에서 위 밖 20픽셀까지
            var previous = GUI.color;
            for (int i = 0; i < fireflies.Length; i++)
            {
                var f = fireflies[i];
                float rise = Mathf.Repeat(f.y + time * f.z, 1f);
                float y = Screen.height + 20f - rise * span;
                float x = f.x * Screen.width + Mathf.Sin(time * 0.7f + f.w) * 24f;
                float twinkle = 0.35f + 0.65f * Mathf.Max(0f, Mathf.Sin(time * 2.1f + f.w * 3f));
                float edge = Mathf.Min(1f, Mathf.Min(rise, 1f - rise) * 6f); // 위아래 끝에서 흐려지며 나타나고 사라진다
                float size = 10f + 6f * ((i * 7) % 5) / 4f;
                GUI.color = (i % 3 == 0 ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 1f, 0.45f)) * new Color(1f, 1f, 1f, 0.8f * twinkle * edge);
                GUI.DrawTexture(new Rect(x - size / 2f, y - size / 2f, size, size), glow, ScaleMode.StretchToFill, true);
            }
            GUI.color = previous;
        }

        // 가운데가 밝고 가장자리로 흐려지는 16×16 둥근 빛
        private static Texture2D MakeGlow()
        {
            const int n = 16;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (glow != null) Destroy(glow); // 런을 시작하면 메뉴가 사라지므로 만든 텍스처도 치운다
        }

        private const string TitleText = "I Blame You!";

        private void DrawTitle(Rect rect)
        {
            float time = Time.unscaledTime;
            // 글자마다 따로 그려 왼쪽부터 물결치듯 통통 (한 글자씩 0.45 라디안 늦게, 위로만 튄다)
            if (letterWidths == null)
            {
                letterWidths = new float[TitleText.Length];
                for (int i = 0; i < TitleText.Length; i++)
                {
                    // 글자 폭만 (스타일 여백은 빼서 글자 사이가 벌어지지 않게). 공백은 CalcSize가 작게 잡으니 글자 크기의 0.3
                    letterWidths[i] = TitleText[i] == ' ' ? title.fontSize * 0.3f
                        : Mathf.Max(1f, title.CalcSize(new GUIContent(TitleText[i].ToString())).x - title.padding.horizontal);
                }
            }
            float total = 0f;
            for (int i = 0; i < letterWidths.Length; i++) total += letterWidths[i];
            float x = rect.center.x - total / 2f;
            for (int i = 0; i < TitleText.Length; i++)
            {
                float w = letterWidths[i];
                if (TitleText[i] != ' ')
                {
                    // 예전 둥실(±3)처럼 위로 3픽셀까지만 넘게: 2픽셀 내려 앉혀 두고 5픽셀 튄다 (BeginArea 위쪽에서 잘리지 않게)
                    float hop = Mathf.Max(0f, Mathf.Sin(time * 3.2f - i * 0.45f)) * 5f - 2f;
                    float boxWidth = w + title.padding.horizontal + 8f; // 외곽선이 잘리지 않게 양옆 4픽셀 여유 (가운데 정렬이라 글자는 x+w/2에 선다)
                    var letter = new Rect(x + w / 2f - boxWidth / 2f, rect.y - hop, boxWidth, rect.height);
                    OutlinedLabel.Draw(letter, TitleText[i].ToString(), title, OutlinedLabel.Cream, OutlinedLabel.Gold, 2.5f, 2f);
                }
                x += w;
            }

            if (sparkle == null) return;
            var previous = GUI.color;
            for (int i = 0; i < SparkleSpots.Length; i++)
            {
                float pulse = Mathf.Max(0f, Mathf.Sin(time * 3f + i * 1.7f)); // 0이면 숨는다
                if (pulse <= 0.01f) continue;
                float size = 10f + 8f * pulse;
                var center = new Vector2(rect.center.x + SparkleSpots[i].x * 400f, rect.center.y + SparkleSpots[i].y * rect.height);
                GUI.color = new Color(1f, 1f, 1f, pulse);
                GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), sparkle, ScaleMode.ScaleToFit, true);
            }

            GUI.color = previous;
        }

        // 시작하지 않고 메뉴에서 바로 끄더라도 바꾼 설정은 남긴다.
        private void OnApplicationQuit()
        {
            GameSession.DevMode = devMode;
            SaveData.SaveSettings();
        }

        private float VolumeSlider(string name, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{name} {value * 100f:0}%", body, GUILayout.Width(150f));
            value = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.Height(24f));
            GUILayout.EndHorizontal();
            return value;
        }

        private const string IntroText = "한 마법사를 두 사람이 나눠 조작한다. A는 이동·방어, B는 공격. 마나는 하나뿐이다.";

        // 기록 줄(최고·최근 클리어·쓰러진 곳·탓)이 늘어 낮은 창에서 버튼을 밀어내지 않게:
        // 다 들어가면 보통 글씨, 모자라면 작은 글씨, 그래도 모자라면 첫 줄만 두고 "기록 더 보기 (H)"로 접는다.
        private void DrawRecords(float innerWidth, float innerHeight)
        {
            string full = RecordLine(devMode);
            float available = innerHeight - FixedContentHeight(innerWidth);
            if (body.CalcHeight(new GUIContent(full), innerWidth) <= available)
            {
                recordsTogglePending = false; // 접을 필요가 없어졌으면 남은 전환 요청은 버린다 (리뷰 LOW)
                GUILayout.Label(full, body);
                return;
            }
            if (recordSmall.CalcHeight(new GUIContent(full), innerWidth) <= available)
            {
                recordsTogglePending = false;
                GUILayout.Label(full, recordSmall);
                return;
            }

            // 펼침 상태는 Layout 이벤트에서만 바꾼다: 한 이벤트 안에서 Layout과 다른 컨트롤 구조가 되지 않게
            if (Event.current.type == EventType.Layout && recordsTogglePending)
            {
                recordsExpanded = !recordsExpanded;
                recordsTogglePending = false;
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.H)
            {
                recordsTogglePending = true;
                Event.current.Use();
            }
            int firstBreak = full.IndexOf('\n');
            if (firstBreak < 0) // 한 줄뿐이면 접을 것이 없다
            {
                GUILayout.Label(full, recordSmall);
                return;
            }
            string shown = recordsExpanded ? full + "\n▲ 기록 접기 (H)" : full.Substring(0, firstBreak) + "\n▼ 기록 더 보기 (H)";
            if (!recordsExpanded)
            {
                if (IBlameYou.UI.CuteButton.LayoutButton(shown, recordSmall)) recordsTogglePending = true;
                return;
            }
            // 펼쳐도 버튼을 밀어내지 않게 남는 높이 안에서 스크롤한다 (리뷰 MEDIUM)
            recordScroll = GUILayout.BeginScrollView(recordScroll, GUILayout.Height(Mathf.Max(40f, available)));
            if (IBlameYou.UI.CuteButton.LayoutButton(shown, recordSmall)) recordsTogglePending = true;
            GUILayout.EndScrollView();
        }

        // 기록 줄을 뺀 나머지(제목·설명·토글·조작법·음량·버튼)가 차지하는 높이 어림값. GUILayout 기본 여백(줄마다 약 4px)까지 더한다.
        private float FixedContentHeight(float innerWidth)
        {
            float h = 60f + 4f + body.CalcHeight(new GUIContent(IntroText), innerWidth) + 12f;
            h += toggle.CalcHeight(new GUIContent("  개발자 모드"), innerWidth) + 8f;
            h += body.CalcHeight(new GUIContent(devMode ? DevModeHelp : NormalModeHelp), innerWidth) + 12f;
            h += 2f * 26f + 8f;                       // 음량 슬라이더 두 줄
            h += 22f;                                 // 유물 도감 줄
            if (saved != null) h += 44f + 4f;         // 이어하기 버튼
            h += 48f + 4f + 40f + 40f;                // 시작·기능 테스트·온라인 버튼
            return h + 12f * 4f;                      // 요소 사이 기본 여백
        }

        private static string RecordLine(bool dev)
        {
            int level = SaveData.BestLevel(dev);
            int gold = SaveData.BestGold(dev); // 한 판에 모은 골드
            string mode = dev ? "개발자 모드" : "일반 모드";
            string line = $"최고 기록 ({mode})  클리어 {SaveData.FormatTime(SaveData.BestClearTime(dev))} · 최고 레벨 {(level > 0 ? level.ToString() : "-")} · 최고 골드 {(gold > 0 ? gold.ToString() : "-")}";
            string recent = SaveData.RecentClearSummary(dev); // 최근 5판 평균 (15분 조정 판단용)
            if (!string.IsNullOrEmpty(recent)) line += "\n" + recent;
            string defeats = SaveData.RecentDefeatSummary(dev); // 최근 쓰러진 판들이 간 방 거리 평균 (어려운 구간 찾기용)
            if (!string.IsNullOrEmpty(defeats)) line += "\n" + defeats;
            string blame = SaveData.RecentBlameSummary(dev); // 최근 10판 누구 탓 (서로 놀릴 거리)
            return string.IsNullOrEmpty(blame) ? line : line + "\n" + blame;
        }

        private const string DevModeHelp =
            "A  이동 A/D · 점프 Space · 달리기 Shift · 대쉬 Left Ctrl · 순간이동 W/A/S/D+Enter\n" +
            "A  방어막 상단 1~5 (불/물/바람/땅/무) 즉시 발동\n" +
            "B  평타 마우스 좌클릭 (마우스 쪽으로)\n" +
            "B  공격 스킬 상단 6~9, 0 (불/물/바람/땅/무) 즉시 발동, 마우스 방향\n" +
            "치트  F1 무적 · F2 마나 무한 · F3 방 안의 적 처치 · F4 체력 회복";

        private const string NormalModeHelp =
            "A  이동 A/D · 점프 Space · 달리기 Shift · 대쉬 Left Ctrl · 순간이동 W/A/S/D+Enter\n" +
            "A  방어막 넘패드 1~5 → 화면에 뜬 순서대로 W/A/S/D (입력 중 캐릭터 정지, 틀리면 마나만 소모)\n" +
            "B  평타 마우스 좌클릭\n" +
            "B  공격 스킬 상단 1~5 → 영창문을 똑같이 타이핑 → Enter (오타면 마나만 소모, Esc 취소)\n" +
            "지금은 한 키보드를 같이 써서, B가 타이핑하는 동안 A의 키 입력은 막힌다.";
    }
}
