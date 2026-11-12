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
        private const int ArenaStartX = 2;  // ChunkGenerator.BuildArena의 전투 구역 시작 열 (기사 자리 기준)

        // 세로 방 발판 모양: 지그재그 계단 / 양 갈래 계단 / 지그재그 + 옆 선반(궁수 자리)
        // / 무너지는 계단(난이도 CrumbleStairsMinDifficulty 이상) / 깜빡 계단(BlinkStairsMinDifficulty 이상)
        public enum ShaftStyle { Zigzag, TwinStairs, Shelves, CrumbleStairs, BlinkStairs }

        public const float CrumbleStairsMinDifficulty = 0.3f;
        public const float BlinkStairsMinDifficulty = 0.45f;

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
            if (HasPairGate(room)) AddPairGate(seed, room, geometry.Chunk);
            return geometry;
        }

        // 협동 봉인문 (A 방어막 속성 → B 마법 속성): 깊이 PairGateMinDifficulty 이후의 보물 곁방에서 상자 앞을 막는다.
        // - 좌우 곁방: 늘 오른쪽 문 하나뿐이고(부모의 왼쪽에 붙음) 바닥이 평평한 휴식 지형. 문(31열)과 상자(가운데 16열) 사이 24열.
        // - 세로 곁방: 바닥 왼쪽 8열 (지그재그 10~21, 양 갈래 3~7·9~12 발판 사이, 옆 선반은 4~7로 당김). 상자는 그 왼쪽 ShaftPairChestX열.
        //   위·아래 문(14~16열)과 도착 칸(11열)은 봉인문 오른쪽이다.
        // 문 쪽은 막지 않으므로 못 열어도 갇히지 않는다 (검증기는 봉인문을 열린 것으로 본다).
        public const float PairGateMinDifficulty = 0.25f;
        public const int SidePairGateX = 24;
        public const int ShaftPairGateX = 8;
        public const int ShaftPairChestX = 4;

        public static bool HasPairGate(RoomData room) =>
            room.Kind == RoomKind.Treasure && room.Difficulty >= PairGateMinDifficulty;

        // 좌우 곁방은 4×3 격자에서 문이 왼쪽에 날 수도 있다: 그때는 봉인문을 거울 자리(왼쪽 문과 가운데 상자 사이)에
        public static int PairGateX(RoomData room) =>
            room.IsVertical ? ShaftPairGateX : (room.Doors & DoorDir.Left) != 0 ? W - 1 - SidePairGateX : SidePairGateX;

        private static void AddPairGate(int seed, RoomData room, ChunkData chunk)
        {
            var rng = new Random(unchecked(seed * 92821 + room.Index * 68917 + 3)); // 지형 난수열과 따로
            int count = ChunkGenerator.GateElementCount;
            int shield = rng.Next(count);
            chunk.Features.Add(new ChunkFeature
            {
                Kind = FeatureKind.ElementGate, X = PairGateX(room), Y = room.IsVertical ? ShaftGround : chunk.ExitHeight, Width = 1,
                ElementIndex = shield, HasSecondElement = true,
                SecondElementIndex = (shield + 1 + rng.Next(count - 1)) % count, ShieldFirst = true
            });
        }

        // 중간보스 전투 구역인지 (구역 적에 쌍둥이 기사가 있음). 처치 보상(체력 50% 즉시 회복 + 유물 상자 확정)을 고를 때 쓴다.
        public static bool IsMidBossArena(ChunkData chunk)
        {
            foreach (var f in chunk.Features)
                if (f.Kind == FeatureKind.Enemy && f.Deferred &&
                    (f.Enemy == StageEnemy.SlimeKnightFire || f.Enemy == StageEnemy.SlimeKnightWater))
                    return true;
            return false;
        }

        // 전투 구역 적(나중에 나오는 적)을 모두 빼고 불 기사·물 기사를 구역 양쪽에 둔다. 구역 바닥은 평평하다(BuildArena).
        // 넓은 방에서는 전투 구역이 방 안쪽 끝 구간에 있으므로 기사 자리는 구역 시작 열(한 화면 방에서는 2)만큼 민다.
        private static void ArmMidBoss(ChunkData chunk)
        {
            int ground = chunk.EntryHeight;
            int shift = 0;
            foreach (var f in chunk.Features)
                if (f.Kind == FeatureKind.Arena) { ground = f.Y; shift = f.X - ArenaStartX; break; }
            chunk.Features.RemoveAll(f => f.Kind == FeatureKind.Enemy && f.Deferred);
            chunk.AddEnemy(StageEnemy.SlimeKnightFire, shift + MidBossFireX, ground, true);
            chunk.AddEnemy(StageEnemy.SlimeKnightWater, shift + MidBossWaterX, ground, true);
        }

        // ------------------------------------------------------------------ 좌우 방

        // 넓은 방 (2026-11-12 "큰 방"): 주 구간(room.Chunk) 앞, 들어오는 문 쪽에 앞 구간(room.Leads)을 이어 붙인다.
        // 왼쪽으로 들어오면 [앞 … 앞 | 주], 오른쪽으로 들어오면 [주 | 앞 … 앞] — 전투·보스 구간은 늘 방 안쪽 끝이다.
        // 구간은 왼쪽부터 만들어 앞 구간의 출구 높이를 다음 구간 입구 높이로 넘긴다 (외길 스테이지와 같은 이음매 규칙).
        private const int LeadSeedStep = 7919;

        private static RoomGeometry BuildHorizontal(int seed, RoomData room)
        {
            RoomGeometry result = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                result = Finish(BuildWide(seed, room, attempt, false), room);
                if (result.Valid) return result;
            }

            var fallback = Finish(BuildWide(seed, room, 0, true), room);
            return fallback.Valid ? fallback : result;
        }

        // 주 구간이 오른쪽 끝(왼쪽으로 들어옴)인지. 오른쪽 문으로 들어오는 방(보스 거울 포함)은 주 구간이 왼쪽 끝.
        internal static bool MainOnRight(RoomData room)
        {
            if (room.EntryDoor == DoorDir.Right) return false;
            if (room.EntryDoor == DoorDir.Left) return true;
            return (room.Doors & DoorDir.Right) == 0; // 들어오는 문이 따로 없으면 문 반대쪽 끝
        }

        private static ChunkData BuildWide(int seed, RoomData room, int attempt, bool fallback)
        {
            int count = room.Leads.Count;
            bool mainRight = MainOnRight(room);
            var parts = new List<ChunkData>();
            ChunkData main = null;
            int entry = StageGenerator.StartHeight;
            for (int i = 0; i <= count; i++)
            {
                bool isMain = mainRight ? i == count : i == 0;
                int lead = mainRight ? i : i - 1; // 앞 구간 순번 (주 구간에서 가까울수록 뒤 번호)
                var kind = isMain ? room.Chunk : room.Leads[mainRight ? lead : count - 1 - lead];
                int partSeed = isMain ? seed : unchecked(seed + LeadSeedStep * (1 + (mainRight ? lead : count - 1 - lead)));
                var part = fallback
                    ? ChunkGenerator.Fallback(room.Index, kind, room.Difficulty, entry)
                    : ChunkGenerator.Generate(partSeed, room.Index, kind, room.Difficulty, entry, attempt);
                if (isMain)
                {
                    if (MirrorsBoss(room)) Mirror(part);
                    main = part;
                }
                parts.Add(part);
                entry = part.ExitHeight;
            }

            return count == 0 ? main : ChunkData.Stitch(parts, main);
        }

        // 보스 방이 오른쪽 문으로 들어오면 (4×3 격자에서 보스가 왼쪽 끝에 놓인 경우) 보스 지형을 좌우로 뒤집어
        // 출구가 들어온 문 반대쪽(왼쪽 끝)에 오게 한다.
        public static bool MirrorsBoss(RoomData room) => room.Kind == RoomKind.Boss && (room.Doors & DoorDir.Right) != 0;

        // 청크를 좌우로 뒤집는다: 타일, 기능 위치(폭이 있는 것은 오른쪽 끝이 새 왼쪽 끝), 화살 방향, 입구·출구 높이.
        // 움직이는 발판은 오른쪽으로만 오가므로 보스 방처럼 발판이 없는 청크에만 쓴다.
        internal static void Mirror(ChunkData chunk)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < chunk.Cols / 2; x++)
                {
                    var t = chunk.Tiles[x, y];
                    chunk.Tiles[x, y] = chunk.Tiles[chunk.Cols - 1 - x, y];
                    chunk.Tiles[chunk.Cols - 1 - x, y] = t;
                }

            for (int i = 0; i < chunk.Features.Count; i++)
            {
                var f = chunk.Features[i];
                f.X = chunk.Cols - f.X - Math.Max(1, f.Width);
                if (f.Kind == FeatureKind.ArrowTrap) f.Direction = -f.Direction;
                chunk.Features[i] = f;
            }

            int entry = chunk.EntryHeight;
            chunk.EntryHeight = chunk.ExitHeight;
            chunk.ExitHeight = entry;
        }

        private static RoomGeometry Finish(ChunkData chunk, RoomData room)
        {
            var geometry = new RoomGeometry { Chunk = chunk };
            WallWithDoor(chunk, 0, (room.Doors & DoorDir.Left) != 0, chunk.EntryHeight);
            WallWithDoor(chunk, chunk.Cols - 1, (room.Doors & DoorDir.Right) != 0, chunk.ExitHeight);
            if ((room.Doors & DoorDir.Left) != 0) geometry.Arrivals[DoorDir.Left] = geometry.Exits[DoorDir.Left] = (0, chunk.EntryHeight);
            if ((room.Doors & DoorDir.Right) != 0) geometry.Arrivals[DoorDir.Right] = geometry.Exits[DoorDir.Right] = (chunk.Cols - 1, chunk.ExitHeight);
            return Check(geometry);
        }

        // ------------------------------------------------------------------ 세로 방 (탑·굴)

        // 위 문이 있는 방만 발판이 있다. 시작 쪽(깊이 0.15 전)은 익숙한 지그재그만.
        public static ShaftStyle PickShaftStyle(int seed, RoomData room)
        {
            if ((room.Doors & DoorDir.Up) == 0 || room.Difficulty < 0.15f) return ShaftStyle.Zigzag;
            var rng = new Random(unchecked(seed * 31 + room.Index * 7919 + 11)); // 지형 난수열과 따로 (지그재그 방 모양이 그대로 남게)
            int count = room.Difficulty >= BlinkStairsMinDifficulty ? 5 : room.Difficulty >= CrumbleStairsMinDifficulty ? 4 : 3;
            return (ShaftStyle)rng.Next(count);
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
                if (style == ShaftStyle.CrumbleStairs || style == ShaftStyle.BlinkStairs)
                    MakeStairsUnsteady(chunk, g, style == ShaftStyle.BlinkStairs);
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

            if (room.Kind == RoomKind.Travel) PlaceShaftEnemies(chunk, g, rng);
            if (room.Kind == RoomKind.Travel && room.Difficulty >= TrapMinDifficulty) PlaceShaftTraps(seed, room, geometry);
            if (!up && room.Kind == RoomKind.Travel) AddTeleportShelf(seed, room, chunk);
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

        // 무너지는/깜빡 계단: 지그재그 계단에서 맨 위 판(위 문 아래)과 맨 아래 판(바닥에서 뛰어오르는 첫 판)을 뺀 판들을
        // 타일 대신 무너지는 발판(Crumble) 또는 깜빡 발판(BlinkPlatform)으로 바꾼다. 둘 다 아래에서 뚫고 오르는 한쪽 발판이고
        // 검증기는 발판으로 보므로 길은 지그재그와 같다. 깜빡 계단은 아래 판부터 박자 순번 0, 1, 2…라 물결이 위로 올라간다.
        private static void MakeStairsUnsteady(ChunkData chunk, int g, bool blink)
        {
            int beat = 0;
            for (int row = LowestStairRow(g) + 2; row < TopPlatformRow; row += 2)
            {
                int x0 = -1, x1 = -1;
                for (int x = 0; x < W; x++)
                {
                    if (chunk.Tiles[x, row] != StageTile.Platform) continue;
                    if (x0 < 0) x0 = x;
                    x1 = x;
                }

                if (x0 < 0) continue;
                for (int x = x0; x <= x1; x++) chunk.Tiles[x, row] = StageTile.Empty;
                if (blink) chunk.AddFeature(FeatureKind.BlinkPlatform, x0, row, x1 - x0 + 1, beat++);
                else chunk.AddFeature(FeatureKind.Crumble, x0, row, x1 - x0 + 1);
            }
        }

        // 계단 맨 아래 판의 행 (TopPlatformRow에서 2칸씩 내려가 바닥 g보다 위인 마지막 행).
        internal static int LowestStairRow(int g)
        {
            int row = TopPlatformRow;
            while (row - 2 > g) row -= 2;
            return row;
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
            int leftX0 = HasPairGate(room) ? 4 : 5; // 협동 봉인문(8열)과 겹치지 않게
            chunk.SetRow(leftX0, leftX0 + 3, leftHigh ? ShelfRowHigh : ShelfRowLow, StageTile.Platform);
            chunk.SetRow(23, 26, leftHigh ? ShelfRowLow : ShelfRowHigh, StageTile.Platform);
            if (room.Kind == RoomKind.Travel && room.Difficulty >= 0.35f)
                chunk.AddEnemy(StageEnemy.GoblinArcher, leftHigh ? 6 : 25, ShelfRowHigh + 1);
            // 높은 선반 안쪽 끝에 동전 무더기 (궁수가 지키는 자리, 위험한 길 보상). 난수를 쓰지 않아 방 모양은 그대로다
            if (room.Kind == RoomKind.Travel && room.Difficulty >= ShelfCoinMinDifficulty)
                chunk.AddFeature(FeatureKind.CoinPile, leftHigh ? leftX0 + 3 : 23, ShelfRowHigh + 1);
        }

        public const float ShelfCoinMinDifficulty = 0.3f;

        // 순간이동 지름길 선반 (Docs/LevelDesign.md "순간이동은 지름길"): 위 문이 없는 세로 방(바닥만 있는 굴) 벽 쪽 높은 곳에
        // 회복 구슬을 올린 선반. 바닥에서 서는 칸 기준 5칸 위라 점프(약 2.5칸)만으로는 못 오르고, 점프 + 위 순간이동(4칸)으로 오른다.
        // 선반 바로 아래에서 위로 순간이동하면 발판 아래면에 막히므로 옆(문 쪽 안쪽)에서 솟아 옆으로 내려앉는다.
        // 필수 경로가 아니라서 검증기에는 닿지 않는 칸으로 남는다 (갇히는 칸 검사는 닿을 수 있는 칸만 본다). 지형 난수열과 따로 굴린다.
        public const int TeleportShelfRise = 5;
        public const float TeleportShelfChance = 0.5f;

        private static void AddTeleportShelf(int seed, RoomData room, ChunkData chunk)
        {
            var rng = new Random(unchecked(seed * 92821 + room.Index * 68917 + 29));
            if (rng.NextDouble() >= TeleportShelfChance) return;
            bool left = rng.Next(2) == 0;
            int row = ShaftGround + TeleportShelfRise - 1; // 발판 행 (윗면에 서는 칸 = row + 1)
            int x0 = left ? 2 : W - 6;
            chunk.SetRow(x0, x0 + 3, row, StageTile.Platform);
            chunk.AddFeature(FeatureKind.Reward, x0 + 1, row + 1);
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

        // 세로 방 함정: 이동 방 바닥에만 가시 줄(2~3칸)과 불기둥(어려운 방). 도착·출발 칸, 구멍 옆,
        // 바닥에서 뛰어오르는 맨 아래 발판 밑, 적이 선 칸에서는 두 칸씩 떨어뜨린다. 계단 판·선반 위에는 놓지 않는다.
        // 지형 난수열과 따로 굴려서 함정이 없는 방의 모양·적 자리는 그대로다. 가시·불기둥은 길을 막지 않아 검증 결과는 같다.
        public const float TrapMinDifficulty = 0.25f;
        private const int TrapMargin = 2;

        private static void PlaceShaftTraps(int seed, RoomData room, RoomGeometry geometry)
        {
            var chunk = geometry.Chunk;
            int g = ShaftGround;
            var rng = new Random(unchecked(seed * 92821 + room.Index * 68917 + 23));
            bool[] safe = SafeTrapColumns(geometry);

            int width = room.Difficulty >= 0.5f && rng.NextDouble() < 0.5 ? 3 : 2;
            var starts = new List<int>();
            for (int x = 0; x + width <= W; x++)
            {
                bool ok = true;
                for (int i = 0; i < width; i++) ok &= safe[x + i];
                if (ok) starts.Add(x);
            }

            if (starts.Count > 0)
            {
                int x = starts[rng.Next(starts.Count)];
                chunk.AddFeature(FeatureKind.Spikes, x, g, width);
                for (int i = -TrapMargin; i < width + TrapMargin; i++)
                    if (x + i >= 0 && x + i < W) safe[x + i] = false;
            }

            if (room.Difficulty < 0.5f) return;
            var jets = new List<int>();
            for (int x = 1; x < W - 1; x++)
                if (safe[x - 1] && safe[x] && safe[x + 1]) jets.Add(x);
            if (jets.Count > 0) chunk.AddFeature(FeatureKind.FlameJet, jets[rng.Next(jets.Count)], g);
        }

        // 바닥(서는 칸 g)에서 함정을 놓아도 되는 열.
        internal static bool[] SafeTrapColumns(RoomGeometry geometry)
        {
            var chunk = geometry.Chunk;
            int g = ShaftGround;
            var safe = new bool[W];
            for (int x = 0; x < W; x++)
                safe[x] = chunk.IsSolid(x, g - 1) && !chunk.IsSolid(x, g) && !chunk.IsSolid(x, g + 1);

            // 문 앞: 좌우 문으로 들어오면 한 칸 안쪽(1, W-2)에 선다 (ChunkStreamer.PlaceAtArrival)
            Block(safe, 0, 1);
            Block(safe, W - 2, W - 1);
            foreach (var spot in geometry.Arrivals.Values) if (spot.y == g) Block(safe, spot.x, spot.x);
            foreach (var spot in geometry.Exits.Values) if (spot.y == g) Block(safe, spot.x, spot.x);
            // Floor(6, g)는 검증용 칸이라 막지 않는다 (막으면 왼쪽에 자리가 남지 않아 가시가 늘 오른쪽 통로에 몰림)
            Block(safe, HoleX0 - 1, HoleX1 + 1); // 구멍 옆과 위 문으로 내려올 때 떨어지는 자리

            // 바닥에서 바로 뛰어오르는 발판(서는 칸에서 3칸 위까지) 밑
            for (int y = g + 1; y <= g + 3 && y < H; y++)
                for (int x = 0; x < W; x++)
                    if (chunk.Get(x, y) == StageTile.Platform) Block(safe, x, x);
            foreach (var f in chunk.Features)
            {
                if (f.Kind != FeatureKind.Crumble && f.Kind != FeatureKind.BlinkPlatform) continue;
                if (f.Y >= g + 1 && f.Y <= g + 3) Block(safe, f.X, f.X + f.Width - 1);
            }

            foreach (var f in chunk.Features)
                if (f.Kind == FeatureKind.Enemy && f.Y == g) Block(safe, f.X, f.X);
            return safe;
        }

        private static void Block(bool[] safe, int x0, int x1)
        {
            for (int x = Math.Max(0, x0 - TrapMargin); x <= Math.Min(W - 1, x1 + TrapMargin); x++) safe[x] = false;
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
