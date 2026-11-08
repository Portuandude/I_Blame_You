namespace IBlameYou.Core
{
    // 시작 화면에서 고른 설정을 런 동안 들고 있는다.
    public static class GameSession
    {
        // 개발자 모드: 한 사람이 A/B를 동시에 조작하고, 스킬 미니게임을 키보드 상단 숫자키 즉시 발동으로 대체한다 (기획서 2.9).
        public static bool DevMode { get; set; } = true;

        // 지금은 데디케이티드 서버가 없어 A와 B가 항상 한 키보드를 같이 쓴다.
        // 그래서 B가 영창을 타이핑하는 동안에는 A의 키보드 입력을 막는다. 온라인이 붙으면 false가 된다.
        public static bool SharedKeyboard => true;

        public static bool Started { get; set; }
    }
}
