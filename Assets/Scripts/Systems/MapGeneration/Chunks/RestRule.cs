namespace IBlameYou.Systems
{
    // 스테이지 사이 쉼터 (2026-11-24 사람 결정): 샘(한 번, 체력 가득), 떠돌이 상인(장비), NPC 한마디, 다음 스테이지 / 저장하고 나가기.
    // 저장은 쉼터에서만 한다 (들어오는 순간 자동 저장, 샘·상인 뒤 나갈 때 다시). 스테이지 도중 저장은 없다.
    public static class RestRule
    {
        public const int SpringUses = 1;

        // 샘: 체력을 가득 채운 값 (최대치를 모르면 그대로)
        public static float SpringHeal(float current, float max) => max > 0f ? max : current;

        // 쉼터 상인 진열 시드 (EquipmentShop(seed, room)의 room 자리에 넣을 값). 마을 상점 방 번호(0~19)와 겹치지 않게.
        public static int ShopRoom(int stage) => 1000 + stage;

        private static readonly string[] Lines =
        {
            "모닥불 앞 떠돌이 상인: \"둘이서 한 몸이라고? 값은 한 사람 몫만 받지.\"",
            "모닥불 앞 떠돌이 상인: \"다음 길은 더 깊어. 신발 끈부터 묶고 가.\"",
            "모닥불 앞 떠돌이 상인: \"싸우는 소리가 여기까지 들리더군. 서로 탓하는 소리도.\"",
            "모닥불 앞 떠돌이 상인: \"샘물은 한 모금이면 충분해. 욕심내면 배탈 난다고.\"",
            "모닥불 앞 떠돌이 상인: \"숲 너머에서 온 손님은 오랜만이야. 살아서 왔으니 단골이지.\"",
        };

        public static string Line(int runSeed, int stage)
        {
            unchecked
            {
                uint h = (uint)runSeed * 2654435761u ^ (uint)stage * 40503u;
                return Lines[(int)(h % (uint)Lines.Length)];
            }
        }
    }
}
