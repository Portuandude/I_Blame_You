namespace IBlameYou.Core
{
    // 시작 화면에서 고른 설정을 런 동안 들고 있는다.
    public static class GameSession
    {
        // 개발자 모드: 한 사람이 A/B를 동시에 조작하고, 스킬 미니게임을 키보드 상단 숫자키 즉시 발동으로 대체한다 (기획서 2.9).
        public static bool DevMode { get; set; } = true;

        // 한 컴퓨터로 하면 A와 B가 한 키보드를 같이 쓴다. 그래서 B가 영창을 타이핑하는 동안에는 A의 키보드 입력을 막는다.
        // 온라인(친구가 자기 컴퓨터로 B)이면 키보드가 둘이라 막지 않는다 (Net/NetSession).
        public static bool SharedKeyboard => !Net.NetSession.RemoteP2Active;

        public static bool Started { get; set; }

        // 기능 테스트 모드: 적 없는 시험용 맵(FeatureTestStage)에서 몬스터·유물·협동 기능을 키로 불러 시험한다.
        // 기록(최고 기록·최근 판·유물 도감)은 남기지 않는다 (SaveData.Record*). 시작 화면 버튼이 정한다.
        public static bool FeatureTest { get; set; }
    }
}
