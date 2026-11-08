using UnityEngine;

namespace IBlameYou.UI
{
    // Esc 일시정지 메뉴: 계속하기 / 다시 시작(새 맵) / 시작 화면.
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

            if (traitWindowOpen || deathHandled || stageCleared || health.IsDead) return;
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
            const float height = 270f;
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게

            GUI.Label(new Rect(rect.x, rect.y + 14f, rect.width, 40f), "일시정지", big);

            var button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            float x = rect.x + 40f;
            float buttonWidth = rect.width - 80f;
            if (GUI.Button(new Rect(x, rect.y + 70f, buttonWidth, 44f), "계속하기 (Esc)", button))
            {
                ClosePauseMenu();
            }
            else if (GUI.Button(new Rect(x, rect.y + 124f, buttonWidth, 44f), "다시 시작 (새 맵)", button))
            {
                ClosePauseMenu();
                restart?.Invoke(false);
            }
            else if (GUI.Button(new Rect(x, rect.y + 178f, buttonWidth, 44f), "시작 화면", button))
            {
                ClosePauseMenu();
                restart?.Invoke(true);
            }

            GUI.Label(new Rect(rect.x, rect.y + 232f, rect.width, 24f), "진행 상황은 저장되지 않는다",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
