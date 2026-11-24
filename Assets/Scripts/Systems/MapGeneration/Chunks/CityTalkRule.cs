namespace IBlameYou.Systems
{
    // 도시 메뉴 한마디 (Docs/CityDesign.md 6절 5단계, Story.md "마법 도시"): 탑지기·담임 선생님이 도시에 올 때마다 한 줄.
    // 처음 오면 선생님이 상황을 길게(세 줄), 판을 마치고 오면 탑지기가 수정 탑 이야기, 시작 화면에서 오면 아무나 짧게.
    public enum CityArrival { FirstVisit, FromStart, AfterRun }

    public static class CityTalkRule
    {
        public const string Teacher = "담임 선생님";
        public const string Keeper = "탑지기";

        public static readonly string[] FirstVisitLines =
        {
            "역사상 가장 시끄러운 입학생들이군. 한 몸에 둘이라니, 악신도 참 심심했나 보지.",
            "괴물을 물리치면 영혼석이 떨어진다. 쓰러져도 남으니 걱정 말고.",
            "영혼석은 수정 탑에 바치거나 아카데미에서 주문을 배우는 데 쓴다. 자, 성문 밖으로!",
        };

        private static readonly string[] AfterRunLines =
        {
            "수정 핵이 또 새 몸을 빚어 줬구나. 어서 일어나거라.",
            "영혼석이 반짝이는구나. 탑에 바치면 금이 하나 메워진단다.",
            "여신께서 쓰러지기 전, 이 탑은 도시 전체를 감쌌지.",
            "악신이 웃는 소리가 들리는 것 같구나. 다음엔 더 멀리 가 보거라.",
            "둘이 싸우지 말고 숨을 맞추면, 몸도 마법도 훨씬 가벼워진단다.",
        };

        private static readonly string[] FromStartLines =
        {
            "오늘은 조용히 들어오나 했더니, 역시 둘이 투닥거리는군.",
            "배운 주문은 우클릭을 꾹. 속성 다이얼에서 고르면 된다.",
            "성문 밖 초원에 고블린이 또 늘었다더구나.",
            "탑의 금이 다 메워지는 날, 여신의 결계가 돌아올 거란다.",
        };

        public static string Speaker(CityArrival arrival) => arrival == CityArrival.AfterRun ? Keeper : Teacher;

        // 한 줄 고르기. seed는 방문마다 바뀌는 수(시간 등). 처음 방문은 line번째 줄 (0~2).
        public static string Line(CityArrival arrival, int seed)
        {
            var lines = arrival == CityArrival.FirstVisit ? FirstVisitLines
                : arrival == CityArrival.AfterRun ? AfterRunLines : FromStartLines;
            int i = seed % lines.Length;
            if (i < 0) i += lines.Length;
            return lines[i];
        }

        // 수정 탑 금: 처음 20개, 탑 단계 하나마다 하나씩 메워진다 (20단계 = 금 없음).
        public static int Cracks(int totalTowerLevels) => System.Math.Max(0, 20 - System.Math.Max(0, totalTowerLevels));
    }
}
