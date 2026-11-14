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
        // / 버섯 탑(MushroomTowerMinDifficulty 이상 이동 방에서 지그재그 대신 절반 확률, 2026-11-14)
        public enum ShaftStyle { Zigzag, TwinStairs, Shelves, CrumbleStairs, BlinkStairs, MushroomTower }

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
            var style = (ShaftStyle)rng.Next(count);
            // 버섯 탑은 지그재그로 뽑힌 이동 방 일부만 바꾼다 (다른 모양을 고르는 난수는 그대로). 따로 굴리는 난수
            if (style == ShaftStyle.Zigzag && room.Kind == RoomKind.Travel && room.Difficulty >= MushroomTowerMinDifficulty)
            {
                var towerRng = new Random(unchecked(seed * 73 + room.Index * 104729 + 41));
                if (towerRng.NextDouble() < MushroomTowerChance) style = ShaftStyle.MushroomTower;
            }
            return style;
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
                else if (style == ShaftStyle.MushroomTower) AddMushroomTower(room, chunk, g, rng);
                else AddStairs(chunk, g, rng);
                if (style == ShaftStyle.Shelves) AddShelves(chunk, room, rng);
                if (style == ShaftStyle.CrumbleStairs || style == ShaftStyle.BlinkStairs)
                    MakeStairsUnsteady(chunk, g, style == ShaftStyle.BlinkStairs);
                if (style != ShaftStyle.TwinStairs && style != ShaftStyle.Shelves && style != ShaftStyle.MushroomTower) AddWindShortcut(room, chunk);
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
            if (up && style == ShaftStyle.MushroomTower) ClearTowerFloor(room, chunk, g);
            if (up && style == ShaftStyle.MushroomTower && TowerHasSniper(room)) AddTowerSniper(room, chunk, g);
            // 위 문 없는 굴: 순간이동 선반이 없으면 통통 버섯 선반. 버섯 열을 함정이 피하도록 함정보다 먼저 둔다
            // (순간이동 선반은 따로 굴리는 난수라 순서를 앞당겨도 결과가 같고, 바닥 + 4행이라 함정 자리 검사와 무관하다)
            if (!up && room.Kind == RoomKind.Travel && !AddTeleportShelf(seed, room, chunk)) AddMushroomShelf(room, chunk);
            if (room.Kind == RoomKind.Travel && room.Difficulty >= TrapMinDifficulty) PlaceShaftTraps(seed, room, geometry);
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

        // 버섯 탑 (2026-11-14 "세로 방 다양화"): 계단 대신 통통 버섯 두 개로 오른다. 바닥 버섯(M1) → 튕겨 바닥 + 7 선반(P1)에 서고,
        // P1 위 버섯(M2) → 튕겨 다시 7칸 위 선반(P2)에 서고, P2에서 점프(위로 2칸)로 위 문 아래 맨 위 판에 오른다.
        // 버섯은 "길 버섯"(Direction = 1)이라 검증기가 버섯 열 위로 TowerMushroomReach칸까지 오르는 길로 본다.
        // 방 번호 짝·홀로 왼쪽/오른쪽 탑 (지형 난수 하나를 지그재그 계단처럼 써서 적 자리 난수열 위치가 같게).
        public const float MushroomTowerMinDifficulty = 0.25f;
        public const float MushroomTowerChance = 0.5f;
        public const int TowerStep = 7;             // 버섯 하나로 오르는 높이 (튕김 7.5 + 갓 0.95 ≈ 8.45 아래로 넉넉히)
        public const int TowerMushroom1X = 6;       // 바닥 버섯 열 (왼쪽 탑 기준, 오른쪽이면 TowerCol 거울 자리)
        public const int TowerMushroom2X = 8;       // P1 위 버섯 열
        public const int TowerShelf1X0 = 4, TowerShelf1X1 = 9;   // P1 (행 = 바닥 + TowerStep - 1)
        public const int TowerShelf2X0 = 6, TowerShelf2X1 = 11;  // P2 (행 = P1 서는 칸 + TowerStep - 1)

        public static bool MushroomTowerOnLeft(RoomData room) => (room.Index & 1) == 0;

        private static void AddMushroomTower(RoomData room, ChunkData chunk, int g, Random rng)
        {
            rng.Next(2); // 지그재그 AddStairs와 같은 수만큼 난수를 써서 뒤의 적 자리 난수열을 맞춘다
            bool left = MushroomTowerOnLeft(room);
            int stand1 = g + TowerStep;
            int stand2 = stand1 + TowerStep;
            chunk.SetRow(HoleX0 - 1, HoleX1 + 1, TopPlatformRow, StageTile.Platform);
            SetRowEither(chunk, TowerCol(left, TowerShelf1X0), TowerCol(left, TowerShelf1X1), stand1 - 1);
            int p2a = Math.Min(TowerCol(left, TowerShelf2X0), TowerCol(left, TowerShelf2X1));
            int p2b = Math.Max(TowerCol(left, TowerShelf2X0), TowerCol(left, TowerShelf2X1));
            // 무너지는 탑 (CrumbleStairsMinDifficulty 이상): P2가 무너지는 발판이라 내려앉으면 0.55초 안에 맨 위 판으로 뛰어야 한다.
            // 떨어져도 P1 위 버섯(M2) 열과 겹쳐 다시 튕겨 오를 수 있다. 검증기는 무너지는 발판을 발판으로 보므로 길은 같다
            if (TowerCrumbles(room)) chunk.AddFeature(FeatureKind.Crumble, p2a, stand2 - 1, p2b - p2a + 1);
            else chunk.SetRow(p2a, p2b, stand2 - 1, StageTile.Platform);
            chunk.AddFeature(FeatureKind.BounceMushroom, TowerCol(left, TowerMushroom1X), g, 1, 1);
            chunk.AddFeature(FeatureKind.BounceMushroom, TowerCol(left, TowerMushroom2X), stand1, 1, 1);
            // 두 번 튕겨 오른 P2 벽 쪽 끝에 동전 무더기 (선반 세로 방과 같은 문턱). 위 문으로 가는 길(안쪽 끝)은 비워 둔다
            if (room.Difficulty >= ShelfCoinMinDifficulty)
                chunk.AddFeature(FeatureKind.CoinPile, TowerCol(left, TowerShelf2X0), stand2);
        }

        public static bool TowerCrumbles(RoomData room) => room.Difficulty >= CrumbleStairsMinDifficulty;

        // 버섯 탑 저격 선반 (TowerSniperMinDifficulty 이상): 탑 반대쪽 벽에 P1 높이 4칸 발판 + 고블린 궁수 하나가
        // 버섯을 타고 오르는 플레이어를 쏜다. 지형 난수를 쓰지 않아 다른 적 자리·함정은 그대로다. 오르는 길(버섯 열·선반)과 겹치지 않는다
        public const float TowerSniperMinDifficulty = 0.45f;
        public const int TowerSniperX0 = 19, TowerSniperX1 = 22; // 왼쪽 탑 기준 (오른쪽 탑이면 TowerCol 거울 자리 8~11). 궁수 감지 14칸 안에 P2가 들도록 가운데 가까이

        public static bool TowerHasSniper(RoomData room) => room.Difficulty >= TowerSniperMinDifficulty;

        private static void AddTowerSniper(RoomData room, ChunkData chunk, int g)
        {
            bool left = MushroomTowerOnLeft(room);
            int row = g + TowerStep - 1;
            int a = Math.Min(TowerCol(left, TowerSniperX0), TowerCol(left, TowerSniperX1));
            int b = Math.Max(TowerCol(left, TowerSniperX0), TowerCol(left, TowerSniperX1));
            chunk.SetRow(a, b, row, StageTile.Platform);
            // 벽 쪽 끝에 서서 선반 가장자리에서 떨어지지 않게
            chunk.AddEnemy(StageEnemy.GoblinArcher, left ? b : a, row + 1);
        }

        // 오른쪽 탑은 위 구멍 가운데(15열) 기준 거울 자리라 P2 → 맨 위 판 간격이 왼쪽과 같은 1칸이다
        public static int TowerCol(bool left, int x) => left ? x : (HoleX0 + HoleX1) - x;

        // 버섯 탑 바닥 버섯 ±2칸에 선 쫄몹은 방 안쪽, 버섯 열 + 3칸부터 빈 열로 옮긴다 (적 난수는 그대로. 내려앉아 튕길 자리를 비움)
        private static void ClearTowerFloor(RoomData room, ChunkData chunk, int g)
        {
            bool left = MushroomTowerOnLeft(room);
            int m = TowerCol(left, TowerMushroom1X);
            int inward = left ? 1 : -1;
            for (int i = 0; i < chunk.Features.Count; i++)
            {
                var f = chunk.Features[i];
                if (f.Kind != FeatureKind.Enemy || f.Y != g || Math.Abs(f.X - m) > 2) continue;
                int to = m + inward * 3;
                while (chunk.Features.Exists(o => o.Kind == FeatureKind.Enemy && o.Y == g && o.X == to)) to += inward;
                f.X = to;
                chunk.Features[i] = f;
            }
        }

        private static void SetRowEither(ChunkData chunk, int a, int b, int row) =>
            chunk.SetRow(Math.Min(a, b), Math.Max(a, b), row, StageTile.Platform);

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

        // 바람 지름길 (2026-11-14 레벨 재설계 2차 "세로 방 다양화"): 위 문이 있는 이동 방에서 문이 없는 쪽 벽 가까이
        // 바닥 환풍구(WindVent, 2칸 폭)가 바람 기둥을 올리고, 기둥 꼭대기 옆 높은 선반에 동전 무더기를 둔다.
        // 바람을 타고 오르다 벽 쪽으로 붙으면 선반에 내려앉는다. 선반은 계단 판에서 5칸 떨어져 걷기 점프로는 닿지 않는다.
        // 양 갈래 계단(벽 쪽 판)·옆 선반 모양, 협동 봉인문 방, 양쪽 다 문인 방에는 두지 않는다. 난수를 쓰지 않아 방 모양은 그대로다.
        // 검증기는 바람을 모르므로 덤 길이다 (계단으로 오르는 길은 그대로).
        public const float WindMinDifficulty = 0.2f;
        public const int WindLedgeRow = 17;       // 선반 발판 행 (서는 칸 18, 계단 판이 없는 홀수 행). 지그재그 계단 판(10~15, 16~21)과 5칸 떨어진 벽 쪽
        public const int WindLedgeWidth = 4;      // 벽 바로 옆 1~4열 (오른쪽이면 W-5 ~ W-2)
        public const int WindVentWidth = 2;       // 선반 바로 안쪽 5~6열 (오른쪽이면 W-7 ~ W-6)
        public const int WindTopRise = 3;         // 기둥 꼭대기 = 선반 서는 칸 + 3 (몸이 선반 위로 넉넉히 떠오르게)

        public static bool WantsWindShortcut(RoomData room) =>
            room.Kind == RoomKind.Travel && room.Difficulty >= WindMinDifficulty && !HasPairGate(room) &&
            (room.Doors & DoorDir.Up) != 0 && ((room.Doors & DoorDir.Left) == 0 || (room.Doors & DoorDir.Right) == 0);

        private static void AddWindShortcut(RoomData room, ChunkData chunk)
        {
            if (!WantsWindShortcut(room)) return;
            bool left = (room.Doors & DoorDir.Left) == 0;
            int g = ShaftGround;
            int ledgeX0 = left ? 1 : W - 1 - WindLedgeWidth;
            int ventX0 = left ? ledgeX0 + WindLedgeWidth : ledgeX0 - WindVentWidth;
            chunk.SetRow(ledgeX0, ledgeX0 + WindLedgeWidth - 1, WindLedgeRow, StageTile.Platform);
            chunk.AddFeature(FeatureKind.WindVent, ventX0, g, WindVentWidth, WindLedgeRow + 1 + WindTopRise - g);
            chunk.AddFeature(FeatureKind.CoinPile, left ? ledgeX0 + 1 : ledgeX0 + WindLedgeWidth - 2, WindLedgeRow + 1);
        }

        // 순간이동 지름길 선반 (Docs/LevelDesign.md "순간이동은 지름길"): 위 문이 없는 세로 방(바닥만 있는 굴) 벽 쪽 높은 곳에
        // 회복 구슬을 올린 선반. 바닥에서 서는 칸 기준 5칸 위라 점프(약 2.5칸)만으로는 못 오르고, 점프 + 위 순간이동(4칸)으로 오른다.
        // 선반 바로 아래에서 위로 순간이동하면 발판 아래면에 막히므로 옆(문 쪽 안쪽)에서 솟아 옆으로 내려앉는다.
        // 필수 경로가 아니라서 검증기에는 닿지 않는 칸으로 남는다 (갇히는 칸 검사는 닿을 수 있는 칸만 본다). 지형 난수열과 따로 굴린다.
        public const int TeleportShelfRise = 5;
        public const float TeleportShelfChance = 0.5f;

        private static bool AddTeleportShelf(int seed, RoomData room, ChunkData chunk)
        {
            var rng = new Random(unchecked(seed * 92821 + room.Index * 68917 + 29));
            if (rng.NextDouble() >= TeleportShelfChance) return false;
            bool left = rng.Next(2) == 0;
            int row = ShaftGround + TeleportShelfRise - 1; // 발판 행 (윗면에 서는 칸 = row + 1)
            int x0 = left ? 2 : W - 6;
            chunk.SetRow(x0, x0 + 3, row, StageTile.Platform);
            chunk.AddFeature(FeatureKind.Reward, x0 + 1, row + 1);
            return true;
        }

        // 버섯 굴 선반 (2026-11-14 "세로 방 다양화"): 위 문 없는 이동 세로 방(굴)에 순간이동 선반이 없으면, 벽 쪽 바닥에
        // 통통 버섯 하나와 그 바로 위 바닥 + MushroomShelfRise 높이에 5칸 발판 선반, 선반 위에 동전 무더기를 둔다.
        // 점프해서 버섯 갓에 내려앉으면 튕겨 올라 한쪽 발판을 아래에서 뚫고 선반에 선다 (평지 버섯 선반과 같은 높이).
        // 방 번호 짝·홀로 왼쪽·오른쪽을 고르고 난수는 쓰지 않는다. 덤 길이라 검증기에는 닿지 않는 칸으로 남는다.
        public const int MushroomShelfRise = ChunkGenerator.BounceLedgeRise; // 선반 서는 칸 = 바닥 + 6
        public const int MushroomShelfX = 5;   // 버섯 열 (왼쪽 기준. 오른쪽이면 W-1-5). 좌우 문 도착 칸(1)과 그 옆 함정 여유(2칸)에서 떨어뜨림
        public const int MushroomShelfHalf = ChunkGenerator.BounceLedgeHalf;

        public const int MushroomEnemyShift = 4; // 옮긴 적이 버섯 열 ±2(선반 아래)를 벗어나고, 아래 문 도착 칸(11, 20)과는 2칸 떨어지게

        public static bool MushroomShelfOnLeft(RoomData room) => (room.Index & 1) == 0;

        private static void AddMushroomShelf(RoomData room, ChunkData chunk)
        {
            if (room.Difficulty < ChunkGenerator.BounceMinDifficulty) return;
            int g = ShaftGround;
            int x = MushroomShelfOnLeft(room) ? MushroomShelfX : W - 1 - MushroomShelfX;
            int stand = g + MushroomShelfRise;
            chunk.SetRow(x - MushroomShelfHalf, x + MushroomShelfHalf, stand - 1, StageTile.Platform);
            chunk.AddFeature(FeatureKind.BounceMushroom, x, g);
            // 쫄몹 자리(5, 26)와 버섯 열이 겹치면 그 적만 방 안쪽으로 MushroomEnemyShift칸 옮긴다 (적 난수는 그대로)
            int inward = MushroomShelfOnLeft(room) ? 1 : -1;
            for (int i = 0; i < chunk.Features.Count; i++)
            {
                var f = chunk.Features[i];
                if (f.Kind != FeatureKind.Enemy || f.Y != g || f.X != x) continue;
                int to = x + inward * MushroomEnemyShift;
                while (chunk.Features.Exists(o => o.Kind == FeatureKind.Enemy && o.Y == g && o.X == to)) to += inward;
                f.X = to;
                chunk.Features[i] = f;
            }
            chunk.AddFeature(FeatureKind.CoinPile, MushroomShelfOnLeft(room) ? x - 1 : x + 1, stand); // 벽 쪽 (버섯 바로 위를 비워 내려앉을 자리)
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
            {
                if (f.Kind == FeatureKind.Enemy && f.Y == g) Block(safe, f.X, f.X);
                if (f.Kind == FeatureKind.WindVent && f.Y == g) Block(safe, f.X, f.X + f.Width - 1); // 바람에 실려 가시·불기둥 위로 떨어지지 않게
                if (f.Kind == FeatureKind.BounceMushroom && f.Y == g) Block(safe, f.X, f.X); // 버섯에서 튕겨 내려오는 자리
            }
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
