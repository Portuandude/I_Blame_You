namespace IBlameYou.Player
{
    // 고르는 메뉴 차례 (2026-11-24 사람 결정, Docs/DesignDecisionsTodo.md·OpenDecisions E2):
    // 유물 상자·장비 카드처럼 고르는 메뉴는 P1(아루, 키보드)과 P2(벨, 마우스)가 한 번씩 번갈아 고른다. 판의 첫 고르기는 P1.
    // 차례인 사람만 조작하고 화면에 누구 차례인지 보인다. 마을·도시 상점은 둘 다 아무나 (X1).
    // 개발자 모드는 한 사람이 A·B를 함께 조작하므로 차례 없이 둘 다 받는다.
    public static class PickTurnRule
    {
        // picksSoFar: 이번 판에 이미 고른 횟수 (유물·장비 합)
        public static bool IsP1Turn(int picksSoFar) => picksSoFar % 2 == 0;

        public static bool KeyboardAllowed(int picksSoFar, bool devMode) => devMode || IsP1Turn(picksSoFar);

        public static bool MouseAllowed(int picksSoFar, bool devMode) => devMode || !IsP1Turn(picksSoFar);

        // 창 위에 띄우는 차례 안내 (개발자 모드는 빈 문자열)
        public static string Label(int picksSoFar, bool devMode) =>
            devMode ? "" : IsP1Turn(picksSoFar) ? "아루(P1) 차례 · 키보드로 고른다" : "벨(P2) 차례 · 마우스로 고른다";
    }
}
