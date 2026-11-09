namespace IBlameYou.Systems
{
    // 플레이어 마법 투사체가 벽(Ground 레이어)에 맞았을 때 그 속성을 받는 오브젝트 (봉인문 퍼즐 등).
    // Projectile이 벽 콜라이더의 부모 쪽에서 찾아 부른다.
    public interface IElementReceiver
    {
        void ReceiveElement(Element element);
    }
}
