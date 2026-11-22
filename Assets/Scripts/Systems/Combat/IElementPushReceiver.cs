using UnityEngine;

namespace IBlameYou.Systems
{
    // 플레이어 마법 투사체가 벽(Ground 레이어)에 맞았을 때 속성과 날아온 방향을 함께 받는 오브젝트 (바람 상자 퍼즐, 2026-11-22).
    // Projectile이 IElementReceiver보다 먼저 찾아 부른다. direction은 투사체 속도 (크기는 뜻 없음).
    public interface IElementPushReceiver
    {
        void ReceiveElement(Element element, Vector2 direction);
    }
}
