namespace IBlameYou.Systems
{
    // 바람 상자 규칙 (순수 C#, EditMode 테스트 대상). 씬의 상자(Puzzles/WindBox)는 칸 번호만 이 규칙으로 정하고 미끄러지는 연출만 한다.
    // - 상자는 1×1칸, 늘 바닥 행(서는 칸 y)에 있다. 바람 마법에 맞으면 날아온 쪽의 반대쪽으로 한 칸 간다 (자유 물리 없음).
    // - 다음 칸이 땅이거나, 그 칸 아래에 디딜 곳이 없거나(구멍), 벽에서 WallGap칸 안이면 움직이지 않는다 ("쿵").
    //   벽에 딱 붙으면 반대쪽에서 쏠 자리가 없어 끼므로, 벽과 상자 사이에 늘 서서 쏠 칸을 남긴다 (레버 없이 끼지 않음).
    // - 선반 아래(한쪽 발판 밑)나 선반 바로 옆 칸이면 A가 상자 위에서 뛰어 선반에 오를 수 있다 (IsStep).
    public static class WindBoxRules
    {
        public const int WallGap = 3; // 0열(벽)과 상자 사이에 비워 두는 칸 수 - 1 (상자는 WallGap ~ Cols-1-WallGap 열)

        public static int MinX => WallGap;
        public static int MaxX(ChunkData chunk) => chunk.Cols - 1 - WallGap;

        // 상자를 dir(-1 왼쪽 / 1 오른쪽) 쪽으로 한 칸 밀었을 때의 열. 못 가면 x 그대로.
        public static int Push(ChunkData chunk, int x, int y, int dir)
        {
            if (dir == 0) return x;
            int next = x + (dir > 0 ? 1 : -1);
            if (next < MinX || next > MaxX(chunk)) return x;
            if (chunk.IsSolid(next, y)) return x;
            var below = chunk.Get(next, y - 1);
            if (below != StageTile.Solid && below != StageTile.Platform) return x;
            return next;
        }

        // 날아온 마법의 가로 속도(부호)로 미는 방향: 오른쪽으로 날아온 바람은 상자를 오른쪽으로.
        public static int DirectionFrom(float velocityX) => velocityX > 0f ? 1 : velocityX < 0f ? -1 : 0;

        // 상자가 이 열에 있으면 그 위에서 뛰어 선반(ledgeX0부터 width칸)에 오를 수 있는지: 선반 아래이거나 바로 옆.
        public static bool IsStep(int x, int ledgeX0, int width) => x >= ledgeX0 - 1 && x <= ledgeX0 + width;

        // 처음 자리에서 디딤 자리까지 최소 몇 번 밀어야 하는지 (같은 쪽으로만 밀면 되는 평지 기준). 못 가면 -1.
        public static int PushesToStep(ChunkData chunk, int x, int y, int ledgeX0, int width)
        {
            if (IsStep(x, ledgeX0, width)) return 0;
            int dir = x < ledgeX0 ? 1 : -1;
            for (int count = 1; count <= chunk.Cols; count++)
            {
                int next = Push(chunk, x, y, dir);
                if (next == x) return -1;
                x = next;
                if (IsStep(x, ledgeX0, width)) return count;
            }
            return -1;
        }
    }
}
