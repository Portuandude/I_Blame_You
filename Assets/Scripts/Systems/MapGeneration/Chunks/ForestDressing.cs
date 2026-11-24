using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 숲 스테이지(2) 함정 놓기 (2026-11-24 사람 결정 C1·OpenDecisions F2). RoomShaper.Build 끝에서 숲 방에만 부른다.
    // 지형 난수열과 따로 굴려서 같은 시드의 지형·적 자리는 스테이지 1과 같다. 놓는 것은 모두 몸이 없거나(덤불·덩굴·통나무)
    // 보물 앞 덩굴 벽(문과 갈라놓을 수 있을 때만, 보물 방 봉인문 자리)뿐이라 방을 지나가는 길은 막지 않는다.
    //  - 가시 덤불: 전투·정예 방 바닥 1~2곳 (깊이 0.5+ 둘). 대나무 가시는 기존 가시를 ChunkStreamer가 초록으로 칠한다.
    //  - 바닥 덩굴: 전투·정예 방 VineChance 확률로 3칸 한 곳.
    //  - 떨어지는 통나무: 깊이 LogMinDifficulty 이상 전투 방, 바닥 LogHeight칸 위 (그 사이가 비어 있는 곳).
    //  - 세로 덩굴 벽: 상자를 문과 갈라놓을 수 있는 보물 방의 상자 앞 (불 마법으로 태워 연다). 두 속성 봉인문 자리면 절반만 덩굴로 바꾼다.
    public static class ForestDressing
    {
        public const int EdgeMargin = 4;          // 방 양 끝(문 앞)에서 이만큼 안쪽에만
        public const double VineChance = 0.6;
        public const int VineWidth = 3;
        public const float LogMinDifficulty = 0.25f;
        public const int LogHeight = 6;
        public const float SecondThornDifficulty = 0.5f;

        public static bool Applies(RoomData room) => StageRoster.IsForest(room.Stage);

        public static void Dress(int seed, RoomData room, RoomGeometry geometry)
        {
            if (!Applies(room)) return;
            var chunk = geometry.Chunk;
            if (room.Kind == RoomKind.Treasure)
            {
                // 상자를 문과 갈라놓을 수 있는 보물 방: 봉인문이 없으면 덩굴 벽, 있으면(깊은 방) 절반은 덩굴 벽으로 바꾼다
                if (!PairGateFits(room)) return;
                bool hasGate = RoomShaper.HasPairGate(room);
                if (hasGate && !VineReplacesGate(seed, room)) return;
                if (hasGate) chunk.Features.RemoveAll(f => f.Kind == FeatureKind.ElementGate);
                chunk.Features.Add(new ChunkFeature
                {
                    Kind = FeatureKind.VineWall, X = RoomShaper.PairGateX(room),
                    Y = room.IsVertical ? RoomShaper.ShaftGround : chunk.ExitHeight, Width = 1
                });
                return;
            }

            if (room.IsVertical || (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Elite)) return;
            var rng = new Random(unchecked(seed * 31337 + room.Index * 7919 + 101));
            var used = UsedColumns(chunk);

            int thorns = room.Difficulty >= SecondThornDifficulty ? 2 : 1;
            for (int i = 0; i < thorns; i++)
            {
                int width = 1 + rng.Next(2);
                if (TryPick(chunk, used, rng, width, 0, out int x, out int floor))
                    chunk.Features.Add(new ChunkFeature { Kind = FeatureKind.ThornBush, X = x, Y = floor, Width = width });
            }

            if (rng.NextDouble() < VineChance && TryPick(chunk, used, rng, VineWidth, 0, out int vx, out int vfloor))
                chunk.Features.Add(new ChunkFeature { Kind = FeatureKind.VineFloor, X = vx, Y = vfloor, Width = VineWidth });

            if (room.Difficulty >= LogMinDifficulty && TryPick(chunk, used, rng, 1, LogHeight + 1, out int lx, out int lfloor))
                chunk.Features.Add(new ChunkFeature { Kind = FeatureKind.FallingLog, X = lx, Y = lfloor + LogHeight, Width = 1 });
        }

        // 깊은 보물 방의 두 속성 봉인문을 덩굴 벽으로 바꿀지 (반반, 시드·방 번호로 고정)
        public static bool VineReplacesGate(int seed, RoomData room) =>
            new Random(unchecked(seed * 6151 + room.Index * 3571 + 17)).NextDouble() < 0.5;

        // 보물 방 봉인문 자리가 상자를 문들과 갈라놓을 수 있는지 (RoomShaper.HasPairGate에서 깊이 조건만 뺀 것)
        public static bool PairGateFits(RoomData room) =>
            room.IsVertical ? (room.Doors & DoorDir.Left) == 0 : room.DoorCount == 1;

        // x열 바닥 (아래에서부터 처음으로 "막힌 칸 바로 위 빈 칸"), 없으면 -1
        public static int FloorAt(ChunkData chunk, int x)
        {
            for (int y = 1; y < ChunkData.Height; y++)
                if (chunk.IsSolid(x, y - 1) && chunk.Get(x, y) == StageTile.Empty) return y;
            return -1;
        }

        private static bool[] UsedColumns(ChunkData chunk)
        {
            var used = new bool[chunk.Cols];
            foreach (var f in chunk.Features)
            {
                if (f.Kind == FeatureKind.Arena || f.Kind == FeatureKind.BlinkWard) continue;
                int w = Math.Max(1, f.Width);
                if (f.Kind == FeatureKind.MovingPlatform) w += Math.Max(0, f.Direction);
                Mark(used, f.X - 2, f.X + w + 1);
            }
            return used;
        }

        private static void Mark(bool[] used, int x0, int x1)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(used.Length - 1, x1); x++) used[x] = true;
        }

        // width칸이 같은 바닥 높이로 평평하고, 그 위 headroom칸(최소 3)이 비어 있고, 다른 기능과 떨어진 자리
        private static bool TryPick(ChunkData chunk, bool[] used, Random rng, int width, int headroom, out int pickX, out int pickFloor)
        {
            var spots = new List<(int x, int floor)>();
            int room = Math.Max(3, headroom);
            for (int x = EdgeMargin; x + width - 1 < chunk.Cols - EdgeMargin; x++)
            {
                int floor = FloorAt(chunk, x);
                if (floor <= 0 || floor + room >= ChunkData.Height) continue;
                bool ok = true;
                for (int i = -1; i <= width && ok; i++)
                {
                    int cx = x + i;
                    if (FloorAt(chunk, cx) != floor || used[cx]) ok = false;
                    for (int y = floor; y < floor + room && ok; y++)
                        if (chunk.Get(cx, y) != StageTile.Empty) ok = false;
                }
                if (ok) spots.Add((x, floor));
            }

            if (spots.Count == 0) { pickX = pickFloor = -1; return false; }
            var spot = spots[rng.Next(spots.Count)];
            pickX = spot.x;
            pickFloor = spot.floor;
            Mark(used, spot.x - 2, spot.x + width + 1);
            return true;
        }
    }
}
