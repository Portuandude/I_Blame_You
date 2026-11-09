using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 방 격자형 스테이지에서 방 하나의 지형을 만든다 (Docs/LevelDesign.md "방 크기와 문 위치").
    // - 좌우 문만 있는 방: 지금의 청크 생성기로 지형을 만들고 양쪽 벽을 세워 문 자리만 뚫는다.
    // - 위/아래 문이 있는 방(탑·굴): 평평한 바닥 + 가운데로 지그재그 발판 계단 + 천장/바닥 구멍.
    // 모든 방은 StageValidator.ValidateRoom으로 "어느 문으로 들어와도 모든 문으로 나갈 수 있고 갇히는 칸이 없음"을 확인한다.
    public class RoomGeometry
    {
        public ChunkData Chunk;
        public readonly Dictionary<DoorDir, (int x, int y)> Arrivals = new Dictionary<DoorDir, (int x, int y)>(); // 그 문으로 들어오면 서는 칸
        public readonly Dictionary<DoorDir, (int x, int y)> Exits = new Dictionary<DoorDir, (int x, int y)>();    // 그 문으로 나가려고 서는 칸
        public bool Valid;
        public string Reason;
        public (int x, int y)? Floor; // 세로 방: 문이 아니어도 오갈 수 있어야 하는 바닥 칸
    }

    public static class RoomShaper
    {
        public const int DoorHeight = 4;        // 좌우 문 구멍 높이 (몸 2칸 + 점프 여유)
        public const int HoleX0 = 14;           // 위/아래 문 구멍 (가로 3칸: 4칸이면 걸어서 못 건너 좌우 문끼리 끊긴다)
        public const int HoleX1 = 16;
        public const int ShaftGround = 5;       // 세로 방 바닥 높이 (서는 칸 y). 발판 계단(20, 18, …, 6행)과 짝이 맞는 값
        // 위 문 아래 발판 행. 천장(Height-1)과 2칸 띄워(서는 칸 = Height - 3) 키 1.08인 몸이 발판 위에서 좌우로 걸어 내려갈 수 있게 한다.
        public const int TopPlatformRow = ChunkData.Height - 4;
        private const int MaxAttempts = 24;

        private const int W = ChunkData.Width;
        private const int H = ChunkData.Height;

        // 중간보스 방: 정예 전투 구역 지형에 정예 대신 쌍둥이 슬라임 기사 둘 (Docs/MidBossDraft.md 후보 C)
        public const int MidBossFireX = 9;
        public const int MidBossWaterX = 22;

        // 세로 방 발판 모양: 지그재그 계단 / 양 갈래 계단 / 지그재그 + 옆 선반(궁수 자리)
        public enum ShaftStyle { Zigzag, TwinStairs, Shelves }

        public static RoomGeometry Build(int seed, RoomData room)
        {
            RoomGeometry geometry;
            if (room.IsVertical)
            {
                var style = PickShaftStyle(seed, room);
                geometry = BuildShaft(seed, room, style);
                if (!geometry.Valid && style != ShaftStyle.Zigzag) geometry = BuildShaft(seed, room, ShaftStyle.Zigzag);
            }
            else
            {
                geometry = BuildHorizontal(seed, room);
            }

            if (room.Kind == RoomKind.MidBoss) ArmMidBoss(geometry.Chunk);
            return geometry;
        }

        // 전투 구역 적(나중에 나오는 적)을 모두 빼고 불 기사·물 기사를 구역 양쪽에 둔다. 구역 바닥은 평평하다(BuildArena).
        private static void ArmMidBoss(ChunkData chunk)
        {
            int ground = chunk.EntryHeight;
            foreach (var f in chunk.Features)
                if (f.Kind == FeatureKind.Arena) { ground = f.Y; break; }
            chunk.Features.RemoveAll(f => f.Kind == FeatureKind.Enemy && f.Deferred);
            chunk.AddEnemy(StageEnemy.SlimeKnightFire, MidBossFireX, ground, true);
            chunk.AddEnemy(StageEnemy.SlimeKnightWater, MidBossWaterX, ground, true);
        }

        // ------------------------------------------------------------------ 좌우 방

        private static RoomGeometry BuildHorizontal(int seed, RoomData room)
        {
            RoomGeometry result = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var chunk = ChunkGenerator.Generate(seed, room.Index, room.Chunk, room.Difficulty, StageGenerator.StartHeight, attempt);
                result = Finish(chunk, room);
                if (result.Valid) return result;
            }

            var fallback = Finish(ChunkGenerator.Fallback(room.Index, room.Chunk, room.Difficulty, StageGenerator.StartHeight), room);
            return fallback.Valid ? fallback : result;
        }

        private static RoomGeometry Finish(ChunkData chunk, RoomData room)
        {
            var geometry = new RoomGeometry { Chunk = chunk };
            WallWithDoor(chunk, 0, (room.Doors & DoorDir.Left) != 0, chunk.EntryHeight);
            WallWithDoor(chunk, W - 1, (room.Doors & DoorDir.Right) != 0, chunk.ExitHeight);
            if ((room.Doors & DoorDir.Left) != 0) geometry.Arrivals[DoorDir.Left] = geometry.Exits[DoorDir.Left] = (0, chunk.EntryHeight);
            if ((room.Doors & DoorDir.Right) != 0) geometry.Arrivals[DoorDir.Right] = geometry.Exits[DoorDir.Right] = (W - 1, chunk.ExitHeight);
            return Check(geometry);
        }

        // ------------------------------------------------------------------ 세로 방 (탑·굴)

        // 위 문이 있는 방만 발판이 있다. 시작 쪽(진행도 15% 전)은 익숙한 지그재그만.
        public static ShaftStyle PickShaftStyle(int seed, RoomData room)
        {
            if ((room.Doors & DoorDir.Up) == 0 || room.Difficulty < 0.15f) return ShaftStyle.Zigzag;
            var rng = new Random(unchecked(seed * 31 + room.Index * 7919 + 11)); // 지형 난수열과 따로 (지그재그 방 모양이 그대로 남게)
            return (ShaftStyle)rng.Next(3);
        }

        internal static RoomGeometry BuildShaft(int seed, RoomData room, ShaftStyle style)
        {
            var rng = new Random(unchecked(seed * 486187739 + room.Index * 16777619 + 7));
            int g = ShaftGround;
            var chunk = new ChunkData
            {
                Index = room.Index, Kind = room.Chunk, Difficulty = room.Difficulty, EntryHeight = g, ExitHeight = g
            };

            bool up = (room.Doors & DoorDir.Up) != 0;
            bool down = (room.Doors & DoorDir.Down) != 0;
            for (int x = 0; x < W; x++)
            {
                bool hole = down && x >= HoleX0 && x <= HoleX1;
                chunk.FillColumn(x, hole ? 0 : g);
            }

            // 천장 (위 문 자리만 뚫림)
            for (int x = 0; x < W; x++)
            {
                if (up && x >= HoleX0 && x <= HoleX1) continue;
                chunk.Tiles[x, H - 1] = StageTile.Solid;
            }

            WallWithDoor(chunk, 0, (room.Doors & DoorDir.Left) != 0, g);
            WallWithDoor(chunk, W - 1, (room.Doors & DoorDir.Right) != 0, g);

            if (up)
            {
                if (style == ShaftStyle.TwinStairs) AddTwinStairs(chunk, g);
                else AddStairs(chunk, g, rng);
                if (style == ShaftStyle.Shelves) AddShelves(chunk, room, rng);
            }

            var geometry = new RoomGeometry { Chunk = chunk };
            if ((room.Doors & DoorDir.Left) != 0) geometry.Arrivals[DoorDir.Left] = geometry.Exits[DoorDir.Left] = (0, g);
            if ((room.Doors & DoorDir.Right) != 0) geometry.Arrivals[DoorDir.Right] = geometry.Exits[DoorDir.Right] = (W - 1, g);
            if (up) geometry.Arrivals[DoorDir.Up] = geometry.Exits[DoorDir.Up] = ((HoleX0 + HoleX1 + 1) / 2, TopPlatformRow + 1);
            // 바닥도 어느 문에서든 오갈 수 있어야 한다 (위 문 하나뿐인 곁방도 바닥까지 내려갔다 돌아올 수 있게)
            geometry.Floor = (6, g);
            if (down)
            {
                geometry.Arrivals[DoorDir.Down] = (HoleX0 - 3, g); // 아래 방에서 올라오면 구멍 옆 바닥에 선다
                geometry.Exits[DoorDir.Down] = (HoleX0 - 1, g);    // 구멍 바로 옆에서 걸어 들어가 떨어진다
            }

            if (room.Kind == RoomKind.Rest) chunk.AddFeature(FeatureKind.Checkpoint, 6, g);
            if (room.Kind == RoomKind.Travel) PlaceShaftEnemies(chunk, g, rng);
            return Check(geometry);
        }

        // 바닥(서는 칸 g)에서 천장 구멍 아래(서는 칸 TopPlatformRow+1)까지 2칸씩 오르는 지그재그 발판.
        // 왼쪽 판(10~15)과 오른쪽 판(16~21)을 번갈아 두어 한 칸 옆으로 비스듬히 뛰어오른다. 맨 위 판은 구멍 아래(13~18).
        private static void AddStairs(ChunkData chunk, int g, Random rng)
        {
            bool left = rng.Next(2) == 0;
            for (int row = TopPlatformRow; row > g; row -= 2)
            {
                if (row == TopPlatformRow) chunk.SetRow(HoleX0 - 1, HoleX1 + 1, row, StageTile.Platform);
                else if (left) chunk.SetRow(10, 15, row, StageTile.Platform);
                else chunk.SetRow(16, 21, row, StageTile.Platform);
                left = !left;
            }
        }

        // 양 갈래 계단: 양쪽 벽 가까이(3~7, 24~28)와 안쪽(9~12, 19~22)을 2칸마다 번갈아 두어 왼쪽·오른쪽 어느 길로도 오른다.
        // 맨 위 판(구멍 아래 13~17)은 안쪽 판에서 한 칸 옆으로 뛰어 닿는다.
        private static void AddTwinStairs(ChunkData chunk, int g)
        {
            bool inner = true; // 맨 위 판 바로 아래 줄은 안쪽
            for (int row = TopPlatformRow; row > g; row -= 2)
            {
                if (row == TopPlatformRow)
                {
                    chunk.SetRow(HoleX0 - 1, HoleX1 + 1, row, StageTile.Platform);
                    continue;
                }

                if (inner)
                {
                    chunk.SetRow(9, 12, row, StageTile.Platform);
                    chunk.SetRow(19, 22, row, StageTile.Platform);
                }
                else
                {
                    chunk.SetRow(3, 7, row, StageTile.Platform);
                    chunk.SetRow(24, 28, row, StageTile.Platform);
                }
                inner = !inner;
            }
        }

        // 옆 선반: 지그재그 계단 옆 벽 쪽에 선반 두 개(5~8, 23~26). 이동 방이면 높은 선반 하나에 고블린 궁수가 선다.
        public const int ShelfRowLow = 11;
        public const int ShelfRowHigh = 15;

        private static void AddShelves(ChunkData chunk, RoomData room, Random rng)
        {
            bool leftHigh = rng.Next(2) == 0;
            chunk.SetRow(5, 8, leftHigh ? ShelfRowHigh : ShelfRowLow, StageTile.Platform);
            chunk.SetRow(23, 26, leftHigh ? ShelfRowLow : ShelfRowHigh, StageTile.Platform);
            if (room.Kind == RoomKind.Travel && room.Difficulty >= 0.35f)
                chunk.AddEnemy(StageEnemy.GoblinArcher, leftHigh ? 6 : 25, ShelfRowHigh + 1);
        }

        // 세로 방의 쫄몹: 구멍과 문에서 떨어진 바닥에 1~3마리.
        private static void PlaceShaftEnemies(ChunkData chunk, int g, Random rng)
        {
            int count = 1 + (rng.NextDouble() < chunk.Difficulty ? 1 : 0) + (chunk.Difficulty > 0.6f && rng.NextDouble() < 0.5 ? 1 : 0);
            var spots = new List<int> { 5, 8, 23, 26 };
            for (int i = 0; i < count && spots.Count > 0; i++)
            {
                int pick = rng.Next(spots.Count);
                chunk.AddEnemy(ChunkGenerator.PickEnemy(rng, chunk.Difficulty), spots[pick], g);
                spots.RemoveAt(pick);
            }
        }

        // ------------------------------------------------------------------ 공통

        // x열을 천장까지 벽으로 막고, 문이 있으면 바닥(floor = 서는 칸)부터 DoorHeight칸만 비운다.
        private static void WallWithDoor(ChunkData chunk, int x, bool door, int floor)
        {
            for (int y = 0; y < H; y++)
            {
                bool opening = door && y >= floor && y < floor + DoorHeight;
                if (y < floor || !opening) chunk.Tiles[x, y] = StageTile.Solid;
                else chunk.Tiles[x, y] = StageTile.Empty;
            }
        }

        private static RoomGeometry Check(RoomGeometry geometry)
        {
            var arrivals = new List<(int x, int y)>(geometry.Arrivals.Values);
            var exits = new List<(int x, int y)>(geometry.Exits.Values);
            foreach (var feature in geometry.Chunk.Features)
                if (feature.Kind == FeatureKind.PlayerStart) arrivals.Add((feature.X, feature.Y));
            if (geometry.Floor.HasValue)
            {
                arrivals.Add(geometry.Floor.Value);
                exits.Add(geometry.Floor.Value);
            }

            if (exits.Count == 0)
            {
                geometry.Valid = false;
                geometry.Reason = "문이 없음";
                return geometry;
            }

            geometry.Valid = StageValidator.ValidateRoom(geometry.Chunk, arrivals, exits, out geometry.Reason);
            return geometry;
        }
    }
}
