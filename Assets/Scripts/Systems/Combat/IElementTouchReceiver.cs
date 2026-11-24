namespace IBlameYou.Systems
{
    // 플레이어 마법 투사체가 지나가며 속성만 알리는 트리거 (숲 바닥 덩굴: 불이면 탄다). IElementReceiver와 달리 투사체를 멈추지 않는다.
    // Projectile이 겹친 트리거에서 찾아 부른다. 같은 투사체가 여러 번 부를 수 있으니 받는 쪽이 한 번만 반응한다.
    public interface IElementTouchReceiver
    {
        void TouchElement(Element element);
    }
}
