using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // Esc 일시정지 메뉴: 배경음악/효과음 볼륨 + 계속하기 / 다시 시작(새 맵) / 시작 화면. 볼륨은 닫을 때 SaveData에 저장한다.
    // 열려 있는 동안 Time.timeScale = 0이고 조작이 잠긴다 (마우스 클릭이 평타로 나가지 않음).
    // Esc가 이미 다른 뜻으로 쓰이는 때(B의 영창 취소, Tab 특성 창 닫기, A의 방어막 미니게임 중)와
    // 쓰러짐/클리어 화면, 다른 이유로 조작이 잠긴 때(출구 진입, 보스에게 붙잡힘)에는 열지 않는다.
    // 키보드: ↑↓·W/S로 버튼을 고르고(고른 버튼에 금색 테두리) Enter·Space로 누른다. 화살표를 누르기 전에도 Enter는 "계속하기".
    // 열 때 창이 PausePopTime(실제 시간) 동안 0.85 → 1.04 → 1로 톡 커지고 "또롱"(PauseOpen), 게임으로 돌아갈 때 "통"(PauseClose).
    public partial class SkillHUD
    {
        private bool paused;
        private float pauseOpenedAt;
        private const float PausePopTime = 0.18f;
        private MinimapUI pauseMap; // 같은 오브젝트의 미니맵 (방 격자 스테이지에만 있다). 처음 쓸 때 찾는다
        private const float PauseSideWidth = 320f;
        private const int PauseButtonCount = 4; // 계속하기 · 다시 시작 · 시작 화면 · 화면 설정
        private int pauseSelection;
        private bool pauseUnlockPending; // Enter·Space로 닫은 프레임: 조작 잠금을 LateUpdate에서 푼다 (같은 키가 순간이동·점프로 새지 않게)
        private bool pauseKeyboard; // 이번에 열고 화살표를 눌렀으면 테두리를 보인다 (마우스만 쓰면 안 보인다)
        private readonly DisplaySettingsWindow displayWindow = new DisplaySettingsWindow(); // 화면 설정 (시작 화면과 같은 창)

        public bool Paused => paused;

        private void UpdatePauseInput()
        {
            if (UpdatePauseKeys()) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (paused && displayWindow.IsOpen)
            {
                if (displayWindow.Applying) return; // "적용됨!" 띠 동안은 Esc도 무시 (곧 저절로 닫힌다)
                displayWindow.Close(true); // Esc는 화면 설정 창만 닫는다 ("통")
                return;
            }

            if (paused)
            {
                ClosePauseMenu(true);
                return;
            }

            if (traitWindowOpen || relicWindowOpen || equipCardOpen || villageOpen || deathHandled || stageCleared || health.IsDead) return;
            if (pauseMap == null) pauseMap = GetComponent<MinimapUI>();
            if (pauseMap != null && pauseMap.Expanded)
            {
                pauseMap.SetExpanded(false); // 펼친 큰 지도가 있으면 Esc는 지도만 접는다
                return;
            }
            if (control.ChantActive || control.ShieldMinigameActive || control.Locked) return;

            paused = true;
            pauseOpenedAt = Time.unscaledTime;
            pauseSelection = 0;
            pauseKeyboard = false;
            control.Locked = true;
            Time.timeScale = 0f;
            GameAudio.PlaySfx("PauseOpen", 0.7f, 0f);
            GameAudio.SetMusicMuffled(true); // 창이 열린 동안 음악이 물속처럼 먹먹하게
        }

        // resumeSound: 게임으로 돌아갈 때(Esc·계속하기)만 "통". 다시 시작·시작 화면·클리어 등으로 닫힐 때는 조용히.
        // holdLock: 키보드(Enter·Space)로 닫을 때. 그 키의 GetKeyDown이 이 프레임 내내 참이라, 잠금은 모든 Update가 끝난 뒤 LateUpdate에서 푼다.
        private void ClosePauseMenu(bool resumeSound = false, bool holdLock = false)
        {
            if (!paused) return;
            if (resumeSound) GameAudio.PlaySfx("PauseClose", 0.7f, 0f);
            paused = false;
            displayWindow.Close();
            Time.timeScale = 1f;
            GameAudio.SetMusicMuffled(false);
            SaveData.SaveVolume();
            if (holdLock) pauseUnlockPending = true;
            else if (!deathHandled && !stageCleared) control.Locked = false;
        }

        private void LateUpdate()
        {
            if (!pauseUnlockPending) return;
            pauseUnlockPending = false;
            if (!paused && !deathHandled && !stageCleared && control != null) control.Locked = false;
        }

        private void DrawPauseMenu()
        {
            if (!paused) return;

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            const float width = 360f;
            const float height = 404f;
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            var previousMatrix = GUI.matrix;
            float pop = PausePopScale();
            if (pop != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * pop, rect.center); // 옆 요약·지도까지 같이 톡
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게

            GUI.Label(new Rect(rect.x, rect.y + 14f, rect.width, 40f), "일시정지", big);

            float x = rect.x + 40f;
            float buttonWidth = rect.width - 80f;

            bool wasEnabled = GUI.enabled;
            GUI.enabled = !displayWindow.IsOpen; // 화면 설정 창 아래 조작(음량 막대 포함)이 클릭을 가로채지 않게
            GameAudio.MusicVolume = PauseVolumeSlider(new Rect(x, rect.y + 64f, buttonWidth, 28f), "배경음악", GameAudio.MusicVolume);
            GameAudio.SfxVolume = PauseVolumeSlider(new Rect(x, rect.y + 96f, buttonWidth, 28f), "효과음", GameAudio.SfxVolume);

            for (int i = 0; i < PauseButtonCount; i++)
            {
                var buttonRect = new Rect(x, rect.y + 140f + 54f * i, buttonWidth, 44f);
                if (pauseKeyboard && i == pauseSelection && !displayWindow.IsOpen) CuteButton.DrawSelection(buttonRect);
                if (CuteButton.Button(buttonRect, PauseButtonLabel(i), menuButton))
                {
                    RunPauseButton(i);
                    break;
                }
            }
            GUI.enabled = wasEnabled;

            GUI.Label(new Rect(rect.x + 10f, rect.y + 352f, rect.width - 20f, 50f),
                "볼륨은 저장된다. 스테이지 1은 15~30분이라 런 진행은 저장되지 않는다 (시작 화면으로 나가면 새로 시작)",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            DrawRunSummary(new Rect(rect.xMax + 12f, rect.y, PauseSideWidth, height));
            DrawPauseMap(new Rect(rect.x - 12f - PauseSideWidth, rect.y, PauseSideWidth, height));
            GUI.matrix = previousMatrix; // 화면 설정 창은 크기 그대로
            displayWindow.Draw(panel, small, menuButton);
        }

        private static string PauseButtonLabel(int index)
        {
            switch (index)
            {
                case 0: return "계속하기 (Esc)";
                case 1: return "다시 시작 (새 맵)";
                case 2: return "시작 화면";
                default: return "화면 설정";
            }
        }

        // 버튼 하나를 누른 것과 같다 (마우스 클릭·키보드 Enter 공용).
        // fromKeyboard: Enter·Space로 눌렀으면 조작 잠금을 이 프레임 끝에 푼다 (ClosePauseMenu holdLock).
        private void RunPauseButton(int index, bool fromKeyboard = false)
        {
            switch (index)
            {
                case 0:
                    ClosePauseMenu(true, fromKeyboard);
                    break;
                case 1:
                    ClosePauseMenu();
                    restart?.Invoke(false);
                    break;
                case 2:
                    ClosePauseMenu();
                    RunSave.UpdateElapsed(RunElapsed); // 이어 할 때 시간이 저장 시점으로 되감기지 않게
                    restart?.Invoke(true);
                    break;
                default:
                    displayWindow.Show();
                    break;
            }
        }

        // 키보드로 버튼 고르기·누르기. 처리했으면 true (그 프레임 Esc 확인은 건너뛴다). 화면 설정 창이 열려 있으면 그 창 몫.
        private bool UpdatePauseKeys()
        {
            if (!paused || displayWindow.IsOpen) return false;
            int step = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) step = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) step = 1;
            if (step != 0)
            {
                // 첫 화살표는 지금 고른 칸(처음엔 계속하기)을 보여 주기만 한다
                if (pauseKeyboard) pauseSelection = (pauseSelection + step + PauseButtonCount) % PauseButtonCount;
                pauseKeyboard = true;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                GameAudio.PlaySfx("UiClick", 0.7f, 0.04f); // 마우스로 누를 때와 같은 소리
                RunPauseButton(pauseSelection, true);
                return true;
            }
            return false;
        }

        // 열린 직후 톡 커지는 배율 (0.85 → 1.04 → 1). 끝났으면 1.
        private float PausePopScale()
        {
            float t = (Time.unscaledTime - pauseOpenedAt) / PausePopTime;
            if (t >= 1f || t < 0f) return 1f;
            return t < 0.6f
                ? Mathf.Lerp(0.85f, 1.04f, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }

        // 일시정지 메뉴 왼쪽: 전체 지도 (MinimapUI.DrawPauseMap). 화면이 좁거나 방 격자 스테이지가 아니면 생략.
        private void DrawPauseMap(Rect rect)
        {
            if (rect.x < 4f) return;
            if (pauseMap == null) pauseMap = GetComponent<MinimapUI>();
            if (pauseMap != null) pauseMap.DrawPauseMap(rect);
        }

        // 일시정지 메뉴 옆: 이번 런에서 A/B가 찍은 특성과 고른 유물 (서로 뭘 골랐는지 확인용).
        private void DrawRunSummary(Rect rect)
        {
            if (rect.xMax > Screen.width - 4f) return; // 화면이 좁으면 생략
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);

            var text = new System.Text.StringBuilder();
            text.Append($"<b>이번 런</b>  레벨 {RunLevel} · {SaveData.FormatTime(RunElapsed)} · <color=#ffd84d>골드 {RunStats.Gold}</color>\n");
            if (mana != null) // 지금까지 날린 마나와 지금 판정 (결과 창 "누구 탓?" 미리보기, Chunks/BlameVerdict)
                text.Append(BlameVerdict.Running(mana.WastedBy(ManaUser.PlayerA), mana.WastedBy(ManaUser.PlayerB))).Append('\n');
            AppendExploreLine(text);
            text.Append("\n<b>특성</b>\n");
            if (progression != null)
            {
                foreach (var trait in IBlameYou.Player.TraitTree.All)
                {
                    int rank = progression.Rank(trait);
                    string color = rank > 0 ? "#ffd84d" : "#8a8a8a";
                    text.Append($"<color={color}>{IBlameYou.Player.TraitTree.Name(trait)} {rank}</color> ({IBlameYou.Player.TraitTree.Owner(trait)})  ");
                }

                if (progression.Points > 0) text.Append($"\n<color=#ffd84d>남은 포인트 {progression.Points} (Tab)</color>");
            }


            float x = rect.x + 12f;
            float width = rect.width - 24f;
            float bottom = rect.yMax - 10f;
            var head = new GUIContent(text.ToString());
            float y = rect.y + 10f;
            float headHeight = Mathf.Min(small.CalcHeight(head, width), bottom - y); // 패널을 넘치지 않게
            GUI.Label(new Rect(x, y, width, headHeight), head, small);
            y += headHeight + 2f;

            y = DrawEquipmentRows(x, y, width, bottom); // 장비 3칸 (SkillHUD.Equipment.cs)

            var relicHead = new GUIContent("<b>유물</b>" + (RelicCount == 0 ? "\n<color=#8a8a8a>없음 (정예 구역 돌파 보상)</color>" : ""));
            float relicHeadHeight = Mathf.Min(small.CalcHeight(relicHead, width), Mathf.Max(0f, bottom - y));
            GUI.Label(new Rect(x, y, width, relicHeadHeight), relicHead, small);
            y += relicHeadHeight + 2f;

            // 유물 줄: 18px 아이콘 + "이름 — 설명" (아이콘이 없으면 글자만 같은 들여쓰기로). 칸을 넘으면 거기서 자른다.
            for (int i = 0; i < RelicCount && y < bottom; i++)
            {
                var id = relics.Owned[i];
                var line = new GUIContent(RelicLine(id));
                float textX = x + RelicListIconSize + 4f;
                float textWidth = width - RelicListIconSize - 4f;
                float lineHeight = Mathf.Min(small.CalcHeight(line, textWidth), bottom - y);
                DrawRelicIcon(id, new Rect(x, y + 1f, RelicListIconSize, RelicListIconSize));
                GUI.Label(new Rect(textX, y, textWidth, lineHeight), line, small);
                y += Mathf.Max(lineHeight, RelicListIconSize + 2f) + 2f;
            }
        }

        // 탐험 줄: "방 N/M 탐험 · 마을 찾음/아직" (방 격자 스테이지만, 마을이 없는 스테이지면 방 수만). 모든 방을 가 봤으면 금색.
        private void AppendExploreLine(System.Text.StringBuilder text)
        {
            string line = ExploreText(false);
            if (line.Length > 0) text.Append(line).Append('\n');
        }

        // 탐험 줄 글 (rich text). 결과 창은 withCoins로 "동전 무더기 a/b"도 붙인다 (다 주웠으면 금색, 무더기가 없는 시드면 생략).
        // 방 격자 스테이지가 아니면 빈 문자열.
        private string ExploreText(bool withCoins)
        {
            if (pauseMap == null) pauseMap = GetComponent<MinimapUI>();
            var streamer = pauseMap != null ? pauseMap.Streamer : null;
            if (streamer == null || !streamer.IsRoomMode) return "";
            int total = streamer.Rooms.Rooms.Count;
            int seen = Mathf.Min(streamer.VisitedCount, total);
            string rooms = seen >= total ? $"<color=#ffd84d>방 {seen}/{total} 탐험</color>" : $"방 {seen}/{total} 탐험";
            string coins = "";
            if (withCoins)
            {
                int piles = streamer.CoinPilesTotal;
                int got = streamer.CoinPilesCollected;
                if (piles > 0) coins = got >= piles ? $" · <color=#ffd84d>동전 무더기 {got}/{piles}</color>" : $" · 동전 무더기 {got}/{piles}";
            }
            var village = streamer.VillageFound;
            string villageText = village == null ? "" : village.Value ? " · <color=#ffa07a>마을 찾음</color>" : " · 마을 아직";
            return rooms + coins + villageText;
        }

        private float PauseVolumeSlider(Rect rect, string name, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, 120f, rect.height), $"{name} {value * 100f:0}%", label);
            return GUI.HorizontalSlider(new Rect(rect.x + 125f, rect.y + 8f, rect.width - 125f, rect.height), value, 0f, 1f);
        }
    }
}
