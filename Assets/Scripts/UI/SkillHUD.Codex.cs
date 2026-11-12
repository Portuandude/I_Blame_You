using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.UI
{
    // 게임 중 C키 유물 도감 (2026-11-12 플레이테스트 피드백 22번): 시작 화면과 같은 창(Core/RelicCodexWindow)을 띄운다.
    // 열려 있는 동안 일시정지처럼 Time.timeScale = 0이고 조작이 잠긴다. C·Esc·닫기 버튼으로 닫는다.
    // 다른 창(일시정지·특성·유물 고르기)이 열려 있거나, B가 영창을 치는 중(공용 키보드에서 C는 글자다), A의 방어막 미니게임 중,
    // 다른 이유로 조작이 잠긴 때(출구 진입·붙잡힘), 쓰러짐/클리어 화면에서는 열지 않는다.
    // 열려 있는 동안에는 Esc(일시정지)·Tab(특성 창) 입력을 이 창이 먼저 가져간다 (창 안의 Tab은 일반/개발자 보기 바꾸기).
    public partial class SkillHUD
    {
        private readonly RelicCodexWindow codexWindow = new RelicCodexWindow();
        private bool codexLocked; // 도감 때문에 시간을 멈추고 조작을 잠갔는지 (닫기 버튼으로 닫혀도 되돌리려고)
        private GUIStyle codexBody;
        private GUIStyle codexSmall;

        public bool CodexOpen => codexLocked;

        // true면 이번 프레임 키 입력을 도감이 가져갔다 (일시정지·특성 창 입력을 건너뛴다).
        private bool UpdateCodexInput()
        {
            if (codexLocked)
            {
                if (!codexWindow.IsOpen || Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Escape)) CloseCodex();
                return true;
            }

            if (!Input.GetKeyDown(KeyCode.C)) return false;
            if (paused || traitWindowOpen || relicWindowOpen || deathHandled || stageCleared || health.IsDead) return false;
            if (control.ChantActive || control.ShieldMinigameActive || control.Locked) return false;

            codexWindow.Show(GameSession.DevMode);
            codexLocked = true;
            control.Locked = true;
            Time.timeScale = 0f;
            return true;
        }

        private void CloseCodex()
        {
            codexWindow.Close();
            if (!codexLocked) return;
            codexLocked = false;
            Time.timeScale = 1f;
            if (!deathHandled && !stageCleared) control.Locked = false;
        }

        private void DrawCodexWindow()
        {
            if (!codexLocked || !codexWindow.IsOpen) return;
            if (codexBody == null)
            {
                codexBody = new GUIStyle(label) { fontSize = 16, wordWrap = true, richText = false };
                codexSmall = new GUIStyle(codexBody) { fontSize = 13 };
            }
            codexWindow.Draw(panel, codexBody, codexSmall, menuButton);
        }
    }
}
