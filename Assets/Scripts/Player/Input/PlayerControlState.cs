using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.Player
{
    // 한 캐릭터를 A(이동)와 B(공격)가 나눠 조작할 때, 서로의 입력을 막거나 캐릭터를 멈추게 하는 상태.
    public class PlayerControlState : MonoBehaviour
    {
        // B가 마법진을 그리는 중 (예전 이름 그대로: 영창). 평타·창 열기를 막는다. P1 키보드는 막지 않는다.
        public bool ChantActive { get; set; }

        // 방 이동 연출 등으로 조작 전체가 잠긴 상태.
        public bool Locked { get; set; }

        public bool MovementFrozen => Locked;

        // A 키보드가 막히는 때. 예전에는 B가 영창을 타이핑하는 동안에도 막았지만, 마법진은 마우스로 그리므로 이제는 조작 잠금 때만 (2026-11-16).
        public bool AKeyboardBlocked => Locked;
    }
}
