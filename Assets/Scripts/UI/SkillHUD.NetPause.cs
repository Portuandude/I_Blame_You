using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 온라인 2인: 판 도중 친구(P2) 연결이 끊기면 호스트 게임을 일시정지 메뉴로 멈춘다 (Net/NetPeerLossPause가 부른다).
    // 친구가 다시 들어올 때까지 기다리거나, "계속하기"로 혼자(이 컴퓨터 마우스가 P2) 이어 할 수 있다.
    // Esc로 여는 일시정지와 같은 창이다. 다만 Esc를 막는 때(영창·마법진 중, 다른 창, 조작 잠금)에는 지금 열지 않고 false —
    // 부르는 쪽이 다음 프레임에 다시 시도한다.
    public partial class SkillHUD
    {
        // 지금 일시정지 메뉴로 멈췄으면 true (이미 열려 있어도 true). 쓰러짐·클리어 뒤에는 열 일이 없으니 GiveUp도 true.
        internal bool TryPauseForLostPeer(out bool giveUp)
        {
            giveUp = deathHandled || stageCleared || health == null || health.IsDead;
            if (giveUp) return false;
            if (paused) return true;
            if (control == null || control.ChantActive || control.MovementFrozen) return false; // MovementFrozen = 잠금 + (develop의) 방어막 미니게임
            if (traitWindowOpen || relicWindowOpen || equipCardOpen || villageOpen) return false;

            paused = true;
            pauseOpenedAt = Time.unscaledTime;
            pauseSelection = 0;
            pauseKeyboard = false;
            control.Locked = true;
            Time.timeScale = 0f;
            GameAudio.PlaySfx("PauseOpen", 0.7f, 0f);
            GameAudio.SetMusicMuffled(true);
            return true;
        }
    }
}
