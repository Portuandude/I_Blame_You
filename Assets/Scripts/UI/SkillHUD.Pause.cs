using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // Esc 일시정지 메뉴: 배경음악/효과음 볼륨 + 계속하기 / 다시 시작(새 맵) / 시작 화면. 볼륨은 닫을 때 SaveData에 저장한다.
    // 열려 있는 동안 Time.timeScale = 0이고 조작이 잠긴다 (마우스 클릭이 평타로 나가지 않음).
    // Esc가 이미 다른 뜻으로 쓰이는 때(B의 영창 취소, Tab 특성 창 닫기, A의 방어막 미니게임 중)와
    // 쓰러짐/클리어 화면, 다른 이유로 조작이 잠긴 때(출구 진입, 보스에게 붙잡힘)에는 열지 않는다.
    public partial class SkillHUD
    {
        private bool paused;

        public bool Paused => paused;

        private void UpdatePauseInput()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (paused)
            {
                ClosePauseMenu();
                return;
            }

            if (traitWindowOpen || relicWindowOpen || deathHandled || stageCleared || health.IsDead) return;
            if (control.ChantActive || control.ShieldMinigameActive || control.Locked) return;

            paused = true;
            control.Locked = true;
            Time.timeScale = 0f;
        }

        private void ClosePauseMenu()
        {
            if (!paused) return;
            paused = false;
            Time.timeScale = 1f;
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
            const float height = 350f;
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게

            GUI.Label(new Rect(rect.x, rect.y + 14f, rect.width, 40f), "일시정지", big);

            float x = rect.x + 40f;
            float buttonWidth = rect.width - 80f;

            GameAudio.MusicVolume = PauseVolumeSlider(new Rect(x, rect.y + 64f, buttonWidth, 28f), "배경음악", GameAudio.MusicVolume);
            GameAudio.SfxVolume = PauseVolumeSlider(new Rect(x, rect.y + 96f, buttonWidth, 28f), "효과음", GameAudio.SfxVolume);

            if (GUI.Button(new Rect(x, rect.y + 140f, buttonWidth, 44f), "계속하기 (Esc)", menuButton))
            {
                ClosePauseMenu();
            }
            else if (GUI.Button(new Rect(x, rect.y + 194f, buttonWidth, 44f), "다시 시작 (새 맵)", menuButton))
            {
                ClosePauseMenu();
                restart?.Invoke(false);
            }
            else if (GUI.Button(new Rect(x, rect.y + 248f, buttonWidth, 44f), "시작 화면", menuButton))
            {
                ClosePauseMenu();
                RunSave.UpdateElapsed(RunElapsed); // 이어 할 때 시간이 체크포인트 시점으로 되감기지 않게
                restart?.Invoke(true);
            }

            GUI.Label(new Rect(rect.x + 10f, rect.y + 298f, rect.width - 20f, 50f),
                "볼륨은 저장된다. 진행은 마지막으로 켠 체크포인트까지 저장되어 시작 화면에서 이어 할 수 있다 (다시 시작하면 지워짐)",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            DrawRunSummary(new Rect(rect.xMax + 12f, rect.y, 320f, height));
        }

        // 일시정지 메뉴 옆: 이번 런에서 A/B가 찍은 특성과 고른 유물 (서로 뭘 골랐는지 확인용).
        private void DrawRunSummary(Rect rect)
        {
            if (rect.xMax > Screen.width - 4f) return; // 화면이 좁으면 생략
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);

            var text = new System.Text.StringBuilder();
            text.Append($"<b>이번 런</b>  레벨 {RunLevel} · {SaveData.FormatTime(RunElapsed)}\n\n<b>특성</b>\n");
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

            text.Append("\n\n<b>유물</b>");
            if (RelicCount == 0) text.Append("\n<color=#8a8a8a>없음 (정예 구역 돌파 보상)</color>");

            float x = rect.x + 12f;
            float width = rect.width - 24f;
            float bottom = rect.yMax - 10f;
            var head = new GUIContent(text.ToString());
            float y = rect.y + 10f;
            float headHeight = small.CalcHeight(head, width);
            GUI.Label(new Rect(x, y, width, headHeight), head, small);
            y += headHeight + 2f;

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

        private float PauseVolumeSlider(Rect rect, string name, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, 120f, rect.height), $"{name} {value * 100f:0}%", label);
            return GUI.HorizontalSlider(new Rect(rect.x + 125f, rect.y + 8f, rect.width - 125f, rect.height), value, 0f, 1f);
        }
    }
}
