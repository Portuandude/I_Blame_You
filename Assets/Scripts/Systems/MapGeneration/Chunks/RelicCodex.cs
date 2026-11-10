namespace IBlameYou.Systems
{
    // 유물 도감: 한 번이라도 고른 유물을 비트 마스크(int)로 기억한다 (SaveData, 모드별). UnityEngine 없이 EditMode 테스트로 고정한다.
    // 유물 번호는 RelicId 순서 (0부터). int라서 31종까지 담는다 — 넘으면 Has/With가 무시한다.
    public static class RelicCodex
    {
        public const int MaxRelics = 31;

        public static bool Has(int mask, int id) => id >= 0 && id < MaxRelics && (mask & (1 << id)) != 0;

        public static int With(int mask, int id) => id >= 0 && id < MaxRelics ? mask | (1 << id) : mask;

        // total(전체 유물 수)보다 작은 번호만 센다.
        public static int Count(int mask, int total)
        {
            int count = 0;
            for (int i = 0; i < total && i < MaxRelics; i++)
                if ((mask & (1 << i)) != 0) count++;
            return count;
        }

        // 전체 유물(번호 0..total-1)을 다 모았는가. 31종을 넘으면 담을 수 없으니 완성도 없다.
        public static bool Complete(int mask, int total) => total > 0 && total <= MaxRelics && Count(mask, total) == total;

        // 시작 화면 한 줄 조각: "유물 도감 7/18", 다 모으면 끝에 별 "유물 도감 18/18 ★"
        public static string Summary(int mask, int total) => $"유물 도감 {Count(mask, total)}/{total}" + (Complete(mask, total) ? " ★" : "");
    }
}
