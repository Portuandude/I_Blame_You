using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // Esc 일시정지 메뉴: 배경음악/효과음 볼륨 + 계속하기 / 다시 시작(새 맵) / 시작 화면. 볼륨은 닫을 때 SaveData에 저장한다.
    // 열려 있는 동안 Time.timeScale = 0이고 조작이 잠긴다 (마우스 클릭이 평타로 나가지 않음).
    // Esc가 이미 다른 뜻으로 쓰이는 때(B의 영창 취소, Tab 특성 창 닫기, A의 방어막 미니게임 중)와
    // 쓰러짐/클리어 화면, 다른 이유로 조작이 잠긴 때(출구 진입, 보스에게 붙잡힘)에는 열지 않는다.
    // 열 때 창이 PausePopTime(실제 시간) 동안 0.85 → 1.04 → 1로 톡 커지고 "또롱"(PauseOpen), 게임으로 돌아갈 때 "통"(PauseClose).
    public partial class SkillHUD
    {
        private bool paused;
        private float pauseOpenedAt;
        private const float PausePopTime = 0.18f;
        private MinimapUI pauseMap; // 같은 오브젝트의 미니맵 (방 격자 스테이지에만 있다). 처음 쓸 때 찾는다
        private const float PauseSideWidth = 320f;
        private readonly DisplaySettingsWindow displayWindow = new DisplaySettingsWindow(); // 화면 설정 (시작 화면과 같은 창)

        public bool Paused => paused;

        private void UpdatePauseInput()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (paused && displayWindow.IsOpen)
            {
                displayWindow.Close(); // Esc는 화면 설정 창만 닫는다
                return;
            }

            if (paused)
            {
                ClosePauseMenu(true);
                return;
            }

            if (traitWindowOpen || relicWindowOpen || equipCardOpen || villageOpen || deathHandled || stageCleared || health.IsDead) return;
            if (control.ChantActive || control.ShieldMinigameActive || control.Locked) return;

            paused = true;
            pauseOpenedAt = Time.unscaledTime;
            control.Locked = true;
            Time.timeScale = 0f;
            GameAudio.PlaySfx("PauseOpen", 0.7f, 0f);
            GameAudio.SetMusicMuffled(true); // 창이 열린 동안 음악이 물속처럼 먹먹하게
        }

        // resumeSound: 게임으로 돌아갈 때(Esc·계속하기)만 "통". 다시 시작·시작 화면·클리어 등으로 닫힐 때는 조용히.
        private void ClosePauseMenu(bool resumeSound = false)
        {
            if (!paused) return;
            if (resumeSound) GameAudio.PlaySfx("PauseClose", 0.7f, 0f);
            paused = false;
            displayWindow.Close();
            Time.timeScale = 1f;
            GameAudio.SetMusicMuffled(false);
            SaveData.SaveVolume();
            if (!deathHandled && !stageCleared) control.Locked = false;
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

            if (CuteButton.Button(new Rect(x, rect.y + 140f, buttonWidth, 44f), "계속하기 (Esc)", menuButton))
            {
                ClosePauseMenu(true);
            }
            else if (CuteButton.Button(new Rect(x, rect.y + 194f, buttonWidth, 44f), "다시 시작 (새 맵)", menuButton))
            {
                ClosePauseMenu();
                restart?.Invoke(false);
            }
            else if (CuteButton.Button(new Rect(x, rect.y + 248f, buttonWidth, 44f), "시작 화면", menuButton))
            {
                ClosePauseMenu();
                RunSave.UpdateElapsed(RunElapsed); // 이어 할 때 시간이 저장 시점으로 되감기지 않게
                restart?.Invoke(true);
            }
            else if (CuteButton.Button(new Rect(x, rect.y + 302f, buttonWidth, 44f), "화면 설정", menuButton))
            {
                displayWindow.Show();
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
            if (pauseMap == null) pauseMap = GetComponent<MinimapUI>();
            var streamer = pauseMap != null ? pauseMap.Streamer : null;
            if (streamer == null || !streamer.IsRoomMode) return;
            int total = streamer.Rooms.Rooms.Count;
            int seen = Mathf.Min(streamer.VisitedCount, total);
            string rooms = seen >= total ? $"<color=#ffd84d>방 {seen}/{total} 탐험</color>" : $"방 {seen}/{total} 탐험";
            var village = streamer.VillageFound;
            string villageText = village == null ? "" : village.Value ? " · <color=#ffa07a>마을 찾음</color>" : " · 마을 아직";
            text.Append(rooms).Append(villageText).Append('\n');
        }

        private float PauseVolumeSlider(Rect rect, string name, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, 120f, rect.height), $"{name} {value * 100f:0}%", label);
            return GUI.HorizontalSlider(new Rect(rect.x + 125f, rect.y + 8f, rect.width - 125f, rect.height), value, 0f, 1f);
        }
    }
}
