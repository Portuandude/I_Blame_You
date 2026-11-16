using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.Player
{
    // 한 캐릭터를 A(이동)와 B(공격)가 나눠 조작할 때, 서로의 입력을 막거나 캐릭터를 멈추게 하는 상태.
    public class PlayerControlState : MonoBehaviour
    {
        // B가 영창문을 타이핑하는 중.
        public bool ChantActive { get; set; }

        // 방 이동 연출 등으로 조작 전체가 잠긴 상태.
        public bool Locked { get; set; }

        public bool MovementFrozen => Locked;

        // 한 키보드를 같이 쓰는 동안 B가 타이핑하면 A의 키 입력이 영창에 섞이지 않도록 막는다.
        public bool AKeyboardBlocked => Locked || (ChantActive && GameSession.SharedKeyboard);
    }
}
