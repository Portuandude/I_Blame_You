using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 청크를 플레이어가 실제로 지나갈 수 있는지 확인한다.
    // 서 있을 수 있는 칸(발밑이 땅/발판이고 몸 두 칸이 비어 있는 칸)을 노드로 보고, 걷기/떨어지기/점프로 이어지는 그래프를 만든다.
    // 1) 입구(왼쪽 끝)에서 출구(오른쪽 끝, 다음 청크와 같은 높이)까지 길이 있어야 하고,
    // 2) 입구에서 갈 수 있는 모든 칸에서 다시 출구로 갈 수 있어야 한다 (빠지면 못 나오는 구덩이 금지. 낭떠러지는 떨어지면 복귀하니 괜찮다).
    public static class StageValidator
    {
        private const int W = ChunkData.Width;
        private const int H = ChunkData.Height;

        // 위로 dy칸 오르며(음수면 내려가며) 건널 수 있는 최대 가로 칸 수 (걷기 속도 기준, 여유를 둔 값).
        private static int JumpReach(int dy)
        {
            if (dy >= 2) return 3;
            if (dy == 1) return 3;
            if (dy >= -2) return 4;
            return 5;
        }

        public static bool Validate(ChunkData chunk, out string reason)
        {
            var support = BuildSupport(chunk);

            int startX = 0;
            int startY = chunk.EntryHeight;
            int goalX = W - 1;
            int goalY = chunk.ExitHeight;

            foreach (var feature in chunk.Features)
            {
                if (feature.Kind == FeatureKind.PlayerStart) { startX = feature.X; startY = feature.Y; }
                if (feature.Kind == FeatureKind.Exit) { goalX = feature.X; goalY = feature.Y; }
            }

            if (!Standable(chunk, support, startX, startY)) { reason = $"입구({startX},{startY})에 설 수 없음"; return false; }
            if (!Standable(chunk, support, goalX, goalY)) { reason = $"출구({goalX},{goalY})에 설 수 없음"; return false; }

            var edges = new List<int>[W * H];
            for (int x = 0; x < W; x++)
            {
                for (int y = 0; y < H; y++)
                {
                    if (!Standable(chunk, support, x, y)) continue;
                    edges[Id(x, y)] = Neighbors(chunk, support, x, y);
                }
            }

            var reachable = Search(Id(startX, startY), edges);
            if (!reachable[Id(goalX, goalY)]) { reason = "출구까지 가는 길이 없음"; return false; }

            // 거꾸로 된 그래프에서 출구로부터 탐색 = 출구로 갈 수 있는 칸들.
            var reverse = new List<int>[W * H];
            for (int from = 0; from < edges.Length; from++)
            {
                if (edges[from] == null) continue;
                foreach (int to in edges[from])
                {
                    if (reverse[to] == null) reverse[to] = new List<int>();
                    reverse[to].Add(from);
                }
            }

            var canFinish = Search(Id(goalX, goalY), reverse);
            for (int i = 0; i < reachable.Length; i++)
            {
                if (reachable[i] && !canFinish[i]) { reason = $"갇히는 칸 ({i / H},{i % H})"; return false; }
            }

            reason = null;
            return true;
        }

        private static int Id(int x, int y) => x * H + y;

        // 발을 디딜 수 있는 칸: 땅, 발판, 무너지는 발판.
        private static bool[,] BuildSupport(ChunkData chunk)
        {
            var support = new bool[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    support[x, y] = chunk.Tiles[x, y] != StageTile.Empty;

            foreach (var feature in chunk.Features)
            {
                if (feature.Kind != FeatureKind.Crumble) continue;
                for (int i = 0; i < feature.Width; i++)
                {
                    int x = feature.X + i;
                    if (x >= 0 && x < W && feature.Y >= 0 && feature.Y < H) support[x, feature.Y] = true;
                }
            }

            return support;
        }

        // 몸이 지나갈 수 있는 칸 (발판은 뚫고 지나간다).
        private static bool Free(ChunkData chunk, int x, int y)
        {
            if (x < 0 || x >= W) return false;
            if (y >= H) return true;
            if (y < 0) return true;
            return chunk.Tiles[x, y] != StageTile.Solid;
        }

        private static bool Standable(ChunkData chunk, bool[,] support, int x, int y)
        {
            if (x < 0 || x >= W || y < 1 || y >= H - 1) return false;
            return support[x, y - 1] && Free(chunk, x, y) && Free(chunk, x, y + 1);
        }

        private static List<int> Neighbors(ChunkData chunk, bool[,] support, int x, int y)
        {
            var result = new List<int>();
            int maxUp = Free(chunk, x, y + 2) ? (Free(chunk, x, y + 3) ? 2 : 1) : 0;

            for (int dir = -1; dir <= 1; dir += 2)
            {
                int nx = x + dir;

                // 걷기 / 걸어서 떨어지기
                if (Free(chunk, nx, y) && Free(chunk, nx, y + 1))
                {
                    int landing = FallLanding(chunk, support, nx, y);
                    if (landing >= 0) result.Add(Id(nx, landing));
                }

                // 점프: 가로 dx칸, 세로 dy칸
                for (int dx = 1; dx <= 5; dx++)
                {
                    int tx = x + dir * dx;
                    if (tx < 0 || tx >= W) break;

                    for (int dy = maxUp; dy >= -H; dy--)
                    {
                        int ty = y + dy;
                        if (ty < 1) break;
                        if (dx > JumpReach(dy)) continue;
                        if (!Standable(chunk, support, tx, ty)) continue;
                        if (!ArcClear(chunk, x, y, dir, dx, ty)) continue;
                        result.Add(Id(tx, ty));
                    }
                }
            }

            return result;
        }

        // 점프 궤적이 땅에 막히지 않는지 (대략): 지나는 열마다 출발/도착 중 높은 쪽 기준 몸 두 칸이 비어 있어야 한다.
        private static bool ArcClear(ChunkData chunk, int x, int y, int dir, int dx, int ty)
        {
            int top = Math.Max(y, ty);
            for (int i = 1; i <= dx; i++)
            {
                int cx = x + dir * i;
                if (!Free(chunk, cx, top) || !Free(chunk, cx, top + 1)) return false;
            }

            return true;
        }

        private static int FallLanding(ChunkData chunk, bool[,] support, int x, int fromY)
        {
            for (int y = fromY; y >= 1; y--)
            {
                if (!Free(chunk, x, y)) return -1;
                if (Standable(chunk, support, x, y)) return y;
            }

            return -1; // 낭떠러지
        }

        private static bool[] Search(int start, List<int>[] edges)
        {
            var visited = new bool[W * H];
            var queue = new Queue<int>();
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (edges[current] == null) continue;
                foreach (int next in edges[current])
                {
                    if (visited[next]) continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            return visited;
        }
    }
}
