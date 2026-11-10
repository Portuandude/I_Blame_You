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
        private GUIStyle button;
        private GUIStyle toggle;
        private GUIStyle panel;    // 둥근 패널 (HUD와 같은 Resources/UI/HUD/Panel.png)
        private Texture2D sparkle; // 제목 둘레 반짝이 별 (HUD 마나 별 그림)

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
            button = CuteButton.Create(20); // 둥근 젤리 버튼 (UI/CuteButton.cs)
            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 20 };
        }

        private void OnGUI()
        {
            EnsureStyles();

            const float width = 640f;
            float height = Mathf.Min(640f, Screen.height - 20f); // 낮은 창에서도 버튼이 잘리지 않게
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(area, GUIContent.none, panel);
            GUI.Box(area, GUIContent.none, panel); // 두 겹으로 덜 비치게 (HUD 창과 같은 방식)

            GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 16f, width - 48f, height - 32f));
            DrawTitle(GUILayoutUtility.GetRect(width - 48f, 60f, GUILayout.Height(60f)));
            GUILayout.Space(4f); // 예전 Label의 아래 여백 (리뷰 LOW)
            GUILayout.Label("한 마법사를 두 사람이 나눠 조작한다. A는 이동·방어, B는 공격. 마나는 하나뿐이다.", body);
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
            GUILayout.Label(RecordLine(devMode), body);
            GUILayout.FlexibleSpace();

            if (saved != null)
            {
                string mode = saved.devMode ? "개발자 모드" : "일반 모드";
                if (GUILayout.Button($"이어하기 — 진행도 {saved.progress * 100f:0}% · 레벨 {saved.level} · {SaveData.FormatTime(saved.elapsed)} ({mode})",
                        button, GUILayout.Height(44f)))
                {
                    GameSession.DevMode = devMode; // 메뉴의 토글이 설정값 (런은 저장된 모드로 진행, PlaytestBootstrap.ContinueWorld)
                    SaveData.SaveSettings();
                    GameSession.Started = true;
                    var callback = onContinue;
                    Destroy(this);
                    callback?.Invoke();
                }

                GUILayout.Space(4f);
            }

            string startLabel = saved == null ? "로컬 테스트 시작"
                : confirmNewRun ? "정말 새로 시작? 이어하던 런이 지워진다 (한 번 더 누르기)" : "새로 시작";
            bool startPressed = GUILayout.Button(startLabel, button, GUILayout.Height(48f));
            if (startPressed && saved != null && !confirmNewRun)
            {
                confirmNewRun = true;
            }
            else if (startPressed)
            {
                GameSession.DevMode = devMode;
                SaveData.SaveSettings();
                GameSession.Started = true;
                var callback = onStart;
                Destroy(this);
                callback?.Invoke();
            }

            GUI.enabled = false;
            GUILayout.Button("온라인 (데디케이티드 서버) — 준비 중", button, GUILayout.Height(40f));
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        // 제목: 진한 외곽선 + gold 테두리 + 크림 글자, 위아래로 둥실, 둘레에 반짝이 별 4개가 번갈아 깜빡임.
        private static readonly Vector2[] SparkleSpots = { new Vector2(-0.36f, -0.3f), new Vector2(0.38f, -0.25f), new Vector2(-0.3f, 0.4f), new Vector2(0.33f, 0.42f) };

        private void DrawTitle(Rect rect)
        {
            float time = Time.unscaledTime;
            var bob = new Rect(rect.x, rect.y + Mathf.Sin(time * 2.2f) * 3f, rect.width, rect.height);
            OutlinedLabel.Draw(bob, "I Blame You!", title, OutlinedLabel.Cream, OutlinedLabel.Gold, 2.5f, 2f);

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

        private static string RecordLine(bool dev)
        {
            int level = SaveData.BestLevel(dev);
            string mode = dev ? "개발자 모드" : "일반 모드";
            string line = $"최고 기록 ({mode})  클리어 {SaveData.FormatTime(SaveData.BestClearTime(dev))} · 최고 레벨 {(level > 0 ? level.ToString() : "-")}";
            string recent = SaveData.RecentClearSummary(dev); // 최근 5판 평균 (15분 조정 판단용)
            return string.IsNullOrEmpty(recent) ? line : line + "\n" + recent;
        }

        private const string DevModeHelp =
            "A  이동 A/D · 점프 Space · 달리기 Shift · 대쉬 Left Ctrl · 순간이동 방향키+Enter\n" +
            "A  방어막 상단 1~5 (불/물/바람/땅/무) 즉시 발동\n" +
            "B  평타 마우스 좌클릭 (마우스 쪽으로)\n" +
            "B  공격 스킬 상단 6~9, 0 (불/물/바람/땅/무) 즉시 발동, 마우스 방향\n" +
            "치트  F1 무적 · F2 마나 무한 · F3 방 안의 적 처치 · F4 체력 회복";

        private const string NormalModeHelp =
            "A  이동 A/D · 점프 Space · 달리기 Shift · 대쉬 Left Ctrl · 순간이동 방향키+Enter\n" +
            "A  방어막 넘패드 1~5 → 화면에 뜬 순서대로 W/A/S/D (입력 중 캐릭터 정지, 틀리면 마나만 소모)\n" +
            "B  평타 마우스 좌클릭\n" +
            "B  공격 스킬 상단 1~5 → 영창문을 똑같이 타이핑 → Enter (오타면 마나만 소모, Esc 취소)\n" +
            "지금은 한 키보드를 같이 써서, B가 타이핑하는 동안 A의 키 입력은 막힌다.";
    }
}
