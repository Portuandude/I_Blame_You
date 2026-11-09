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
            SaveData.SaveSettings();
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

            var button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            float x = rect.x + 40f;
            float buttonWidth = rect.width - 80f;

            GameAudio.MusicVolume = PauseVolumeSlider(new Rect(x, rect.y + 64f, buttonWidth, 28f), "배경음악", GameAudio.MusicVolume);
            GameAudio.SfxVolume = PauseVolumeSlider(new Rect(x, rect.y + 96f, buttonWidth, 28f), "효과음", GameAudio.SfxVolume);

            if (GUI.Button(new Rect(x, rect.y + 140f, buttonWidth, 44f), "계속하기 (Esc)", button))
            {
                ClosePauseMenu();
            }
            else if (GUI.Button(new Rect(x, rect.y + 194f, buttonWidth, 44f), "다시 시작 (새 맵)", button))
            {
                ClosePauseMenu();
                restart?.Invoke(false);
            }
            else if (GUI.Button(new Rect(x, rect.y + 248f, buttonWidth, 44f), "시작 화면", button))
            {
                ClosePauseMenu();
                restart?.Invoke(true);
            }

            GUI.Label(new Rect(rect.x, rect.y + 308f, rect.width, 24f), "볼륨은 저장되고, 진행 상황은 저장되지 않는다",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }

        private float PauseVolumeSlider(Rect rect, string name, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, 120f, rect.height), $"{name} {value * 100f:0}%", label);
            return GUI.HorizontalSlider(new Rect(rect.x + 125f, rect.y + 8f, rect.width - 125f, rect.height), value, 0f, 1f);
        }
    }
}
