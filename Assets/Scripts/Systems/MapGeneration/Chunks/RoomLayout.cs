using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 방 격자형 스테이지의 배치 (Docs/LevelDesign.md). 던그리드·아이작처럼 방들이 격자 칸에 놓이고 문으로 이어진다.
    // 스테이지 1 = 가로 5 × 세로 4 격자 (2026-11-14 23:30·23:37 사람 결정, PR #109의 13~17방을 바꿈):
    //   방 14~18개(시작·보스 방 포함) = 일반 전투 5+ · 퍼즐 2+(몹 없음) · 마을 1 고정 · 중간보스 1 · 보물·샘 보상 방 1~2.
    //   시작·보스를 빼고 14~18로 세면 격자 칸 16~20개라 예상 시간이 30분을 넘어 판이 만들어지지 않아서 시작·보스를 포함해 센다.
    //   정예 방은 전투 방의 한 종류로 센다. 중간보스는 후보 중 하나를 시드로 고른다 (MidBossFor).
    // 이동 방(기믹만 있는 방)은 없앴다: 기믹(버섯·바람·무너지는 발판·봉인문·움직이는 발판)은 전투 방 앞 구간에 섞이고, 세로 방도 전투 방이다.
    // 시작 방은 가운데 열(x 2), 가운데 두 줄 중 하나 (b3/c3). 격자에서 안 쓰는 칸에는 방이 없다 (던그리드).
    // 방들은 나무 모양(고리 없음)으로 이어지고, 시작 방에서 멀수록(방 거리) 어렵다.
    // 주 경로·곁방·막다른 방 개념은 없다 (2026-11-24 사람 결정): 보스 방 = 시작에서 가장 먼 방, 중간보스 방 = 그다음으로 먼 방
    // (보스 방으로 가는 길목은 빼서 중간보스가 보스 앞 관문이 되지 않게). 마을·보물·샘·퍼즐 방은 문 모양만 맞으면 어디든 놓인다.
    // 방 하나 = 격자 한 칸 = 화면 하나. 방 안의 지형은 청크 생성기(ChunkGenerator)가 만들고, 여기서는 어느 칸에 어떤 방이
    // 어느 문으로 이어지는지만 정한다. UnityEngine 없이 돌아서 EditMode 테스트와 Tools/StageGen에서 그대로 검증한다.

    [Flags]
    public enum DoorDir
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8
    }

    public enum RoomKind
    {
        Start,
        Travel,    // 이동 방 (지형·함정·쫄몹). 2026-11-14 사람 결정으로 방 격자에서는 더 나오지 않는다 (기능 테스트·옛 외길 스테이지용)
        Puzzle,    // 퍼즐 방: 몹이 나오지 않는다 (2026-11-14). 이중 스위치 발판 하나 + 퍼즐 하나 (등불 순서·거울 룬·바람 상자) (Docs/PuzzleRoomDesign.md)
        Combat,    // 들어가면 문이 잠기는 전투 방. 좌우 방은 앞 구간에 기믹 지형, 세로 방(탑·굴)은 바닥 전체가 전투 구역
        Elite,     // 정예 전투 방 (유물 상자)
        Rest,      // 숨 고르기 방 (전투 사이 완충. 체크포인트는 2026-11-10 제거)
        MidBoss,   // 중간보스 방 (보스 다음으로 먼 방)
        Boss,      // 마지막 방 + 출구
        Treasure,  // 보물 방: 유물 상자
        Spring,    // 샘 방: 회복 샘
        Village,   // NPC 마을 (식당·장비 상점, 적·함정 없음). 스테이지마다 하나, 방을 돌아다니며 찾는다 (2026-11-12 사람 결정)
        ManaSpring // 마나 샘 방 (2026-11-24): 처음 들어가면 3분 마나 재생 +10% (ManaSpringRule). 판마다 0~1개, 적 없음
    }

    // 중간보스 후보 (2026-11-14 23:30 사람 결정: "중간 보스는 둘 중 하나만 랜덤으로").
    // 쌍둥이 슬라임 기사(후보 C)와 프리즘 젤리(후보 D, 2026-11-16 "종족은 슬라임으로 하나 더")가 시드로 반반.
    // 후보를 더하면 여기와 RoomLayout.MidBossPool에 더한다. 0번(기사)이 기본값이라 직접 만든 RoomData는 기사 방이 된다.
    public enum MidBossKind
    {
        SlimeKnights,
        PrismJelly
    }

    public class RoomData
    {
        public int Index;            // Rooms 목록 안의 번호 (시작 방에서 가까운 순)
        public int X, Y;             // 격자 칸 (오른쪽 +x, 위 +y)
        public RoomKind Kind;
        public ChunkKind Chunk;      // 방 안 지형을 만들 청크 종류
        public float Difficulty;     // 0(시작) ~ 1(끝)
        public DoorDir Doors;        // 이 방에 있는 문
        public DoorDir EntryDoor;    // 시작 방 쪽에서 들어오는 문 (시작 방은 None)
        public int TreeParentIndex = -1; // 시작 방 쪽으로 한 칸 (시작 방은 -1)
        public int Distance;         // 시작 방에서 문을 몇 번 지나야 오는지 (방 거리). 난이도 = 거리 / 보스 거리
        public MidBossKind MidBoss;  // 중간보스 방일 때 나올 중간보스 (RoomLayoutData.MidBoss와 같음, RoomShaper가 본다)
        public int Stage = 1;        // 스테이지 번호 (RoomLayoutData.Stage와 같음). 방 배치는 같고 적·함정 구성만 바뀐다 (StageRoster)
        // 넓은 방 (2026-11-12 "큰 방"): 주 구간(Chunk) 앞, 들어오는 문 쪽에 이어 붙이는 이동 구간들. 비어 있으면 한 화면 방.
        public readonly List<ChunkKind> Leads = new List<ChunkKind>();
        public int Cols => (1 + Leads.Count) * ChunkData.Width; // 방 가로 칸 수

        // 위/아래 문이 있는 방은 세로로 오르내리는 방(탑·굴)이다.
        public bool IsVertical => (Doors & (DoorDir.Up | DoorDir.Down)) != 0;
        public int DoorCount => RoomLayout.CountDoors(Doors);
        public bool LocksDoors => Kind == RoomKind.Combat || Kind == RoomKind.Elite || Kind == RoomKind.MidBoss || Kind == RoomKind.Boss;
    }

    public class RoomLayoutData
    {
        public readonly List<RoomData> Rooms = new List<RoomData>();
        public int Seed;
        public MidBossKind MidBoss;  // 이번 판 중간보스 (후보 중 시드로 하나)
        public int Stage = 1;        // 스테이지 번호 (1 = 슬라임·고블린, 2 = 숲)

        public int MinX, MaxX, MinY, MaxY;

        public RoomData At(int x, int y)
        {
            foreach (var room in Rooms) if (room.X == x && room.Y == y) return room;
            return null;
        }

        public RoomData Neighbor(RoomData room, DoorDir dir)
        {
            var (dx, dy) = RoomLayout.Offset(dir);
            return At(room.X + dx, room.Y + dy);
        }

        public RoomData Find(RoomKind kind)
        {
            foreach (var room in Rooms) if (room.Kind == kind) return room;
            return null;
        }
    }

    public static class RoomLayout
    {
        public const int GridWidth = 5;
        public const int GridHeight = 4;
        public const int MinRooms = 14;              // 시작·보스 방 포함
        public const int MaxRooms = 18;
        public const int StartX = 2;                 // 시작 방: 가운데 열, y 1 또는 2
        // 방 구성 하한 (2026-11-14 사람 결정)
        public const int MinCombatRooms = 5;         // 일반 전투 방 (정예·중간보스·보스 제외)
        // 좌우 일반 전투 방 (앞 구간에 기믹 지형이 붙는 방) 최소 수 (2026-11-16 기본안: 세로 전투 방만 이어지면 단조롭다).
        // 5×4 나무 모양에서는 퍼즐·마을·중간보스·보스가 좌우 자리를 먼저 차지해 판마다 1개 안팎이었다. 못 맞추면 다른 배치로 다시 뽑는다.
        public const int MinSideCombatRooms = 2;
        public const int MinPuzzleRooms = 2;         // 퍼즐 방 (몹 없음)
        public const int Villages = 1;               // 마을 1개 고정 (2026-11-14 23:30)
        public const int MinRewardRooms = 1;         // 보상 방 (보물 상자 / 회복 샘) (2026-11-14 23:37 다시 넣음)
        public const int MaxRewardRooms = 2;
        public const int MinBossDistance = 4;        // 시작 → 보스 문 수
        public const int MinMidBossDistance = 2;     // 중간보스는 시작 방에서 문 두 개 이상
        public const int VillageMinDistance = 2;     // 마을은 시작 방에서 문 두 개 이상 (골드를 조금 모은 뒤 찾게)
        public const float MinSeconds = 15f * 60f;   // 스테이지 목표 15~30분 (사람 확정 2026-11-12)
        public const float MaxSeconds = 30f * 60f;
        public const float BacktrackSeconds = 12f;   // 왔던 방으로 되돌아 나오는 데 문 하나당 더 드는 시간 (어림)
        private const int MaxAttempts = 800; // 좌우 전투 방 2개 보장(2026-11-16)으로 다시 뽑기가 늘어 여유를 둔다 (시드 56000판 최대 365회)
        private const int MaxElites = 2;
        public const int MinFights = MinCombatRooms; // 중간보스·보스 말고 일반 전투 방 최소 수 (옛 이름)

        // 넓은 방: 좌우 전투·정예·중간보스·보스 방은 앞 구간 2개(3화면). 퍼즐 방(몹 없음)·시작·보물·샘·마을·세로 방은 한 화면.
        // 격자 한 칸의 가로 크기는 가장 넓은 방에 맞춘다 (ChunkStreamer 원점이 칸마다 같은 간격).
        public const int MaxLeads = 2;
        public const int CellCols = (1 + MaxLeads) * ChunkData.Width;

        public static int LeadCount(RoomData room)
        {
            if (room.IsVertical) return 0;
            switch (room.Kind)
            {
                case RoomKind.Travel:
                case RoomKind.Combat:
                case RoomKind.Elite:
                    return MaxLeads;
                case RoomKind.MidBoss:
                case RoomKind.Boss:
                    return MaxLeads;
                case RoomKind.Rest:
                    return 1; // 숨 고르기 방: 앞에 이동 구간 하나 + 빈 평지
                default:
                    return 0;
            }
        }

        // 앞 구간 종류: 기믹 지형 (2026-11-14 사람 결정 "기믹은 일반 전투 방에 섞는다"). 일반·정예 전투 방은 봉인문·움직이는 발판도
        // 앞 구간에 하나까지 (봉인문 열림 기록이 방 번호 하나에 하나라서). 중간보스·보스 방은 이동 지형만 (보스 방 좌우 뒤집기가 움직이는 발판을 못 다룸).
        // 배치 난수열과 따로 굴린다.
        public static bool AllowsGimmickLead(RoomData room) => room.Kind == RoomKind.Combat || room.Kind == RoomKind.Elite;

        private static bool IsPuzzleChunk(ChunkKind kind) => kind == ChunkKind.MovingBridge || kind == ChunkKind.ElementGate;

        private static void AssignLeads(RoomLayoutData layout)
        {
            var rng = new Random(unchecked(layout.Seed * 7368787 + 2017));
            foreach (var room in layout.Rooms)
            {
                room.Leads.Clear();
                int count = LeadCount(room);
                var last = room.Chunk;
                bool gimmick = AllowsGimmickLead(room);
                for (int i = 0; i < count; i++)
                {
                    var kind = StagePlanner.PickTravel(rng, room.Difficulty, last, gimmick);
                    if (IsPuzzleChunk(kind))
                    {
                        if (gimmick) gimmick = false; // 방마다 하나
                        else kind = last == ChunkKind.Hills ? ChunkKind.Plains : ChunkKind.Hills;
                    }
                    room.Leads.Add(kind);
                    last = kind;
                }
            }
        }

        private static readonly DoorDir[] Dirs = { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down };

        // targetSeconds: 목표 시간 (15~30분으로 자른다). 방 수가 이것을 따라간다.
        // stage: 스테이지 번호 (StageRoster). 방 배치 자체는 스테이지와 상관없이 시드로만 정한다.
        public static RoomLayoutData Generate(int seed, float targetSeconds, int stage = 1)
        {
            float target = Math.Max(MinSeconds, Math.Min(MaxSeconds, targetSeconds));
            var rng = new Random(seed);
            RoomLayoutData best = null;
            for (int attempt = 0; attempt < MaxAttempts && best == null; attempt++)
                best = TryBuild(seed, rng, target);
            if (best == null) best = Snake(seed, target); // 이론상 오지 않는다 (테스트가 지킨다)
            best.Stage = StageRoster.Clamp(stage);
            foreach (var room in best.Rooms) room.Stage = best.Stage;
            return best;
        }

        // 방 거리 기준 예상 시간: 방마다 StagePlanner 어림 + 되돌아오기. 모든 방을 다 돌면 보스로 가는 문(보스 거리)만 빼고
        // 나머지 문은 갔다가 되돌아와야 하므로 (방 수 - 1 - 보스 거리) × 2 × BacktrackSeconds.
        public static float EstimatedSeconds(RoomLayoutData layout)
        {
            float total = 0f;
            foreach (var room in layout.Rooms)
            {
                total += room.Kind == RoomKind.MidBoss ? StagePlanner.EstimatedSeconds(ChunkKind.EliteArena) : StagePlanner.EstimatedSeconds(room.Chunk);
                if (room.Kind == RoomKind.Combat && room.IsVertical) total += StagePlanner.EstimatedSeconds(ChunkKind.Climb); // 오르내리는 몫
                // 앞 구간: 정해졌으면 그 종류대로, 아직이면(종류 배정 중) 평지 어림
                if (room.Leads.Count > 0)
                    foreach (var lead in room.Leads) total += StagePlanner.EstimatedSeconds(lead);
                else
                    total += LeadCount(room) * StagePlanner.EstimatedSeconds(ChunkKind.Plains);
            }

            int far = 0;
            foreach (var room in layout.Rooms) far = Math.Max(far, room.Distance);
            total += 2f * BacktrackSeconds * Math.Max(0, layout.Rooms.Count - 1 - far);
            return total;
        }

        private static RoomLayoutData TryBuild(int seed, Random rng, float target)
        {
            // 목표가 길수록 방이 많다 (15분 → 14~15, 30분 → 17~18)
            float t = (target - MinSeconds) / (MaxSeconds - MinSeconds);
            int count = Math.Min(MaxRooms, MinRooms + rng.Next(2) + (int)Math.Round(t * 3));
            int startX = StartX;
            int startY = rng.Next(1, 3);
            var cells = new bool[GridWidth, GridHeight];
            cells[startX, startY] = true;
            var free = new List<(int x, int y)>();
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                    if (!(x == startX && y == startY)) free.Add((x, y));
            Shuffle(free, rng);
            for (int i = 0; i < count - 1; i++) cells[free[i].x, free[i].y] = true;

            var layout = new RoomLayoutData { Seed = seed };
            var byCell = new RoomData[GridWidth, GridHeight];
            var order = new List<(int x, int y)> { (startX, startY) };
            for (int y = 0; y < GridHeight; y++)
                for (int x = 0; x < GridWidth; x++)
                    if (cells[x, y] && !(x == startX && y == startY)) order.Add((x, y));
            foreach (var cell in order)
            {
                var room = new RoomData { X = cell.x, Y = cell.y };
                byCell[cell.x, cell.y] = room;
                layout.Rooms.Add(room);
            }

            if (!BuildTree(layout, byCell, rng)) return null;
            if (!Measure(layout, byCell[startX, startY], out var boss)) return null;

            if (!AssignKinds(layout, boss, rng)) return null;
            Reindex(layout);
            AssignLeads(layout);
            float estimate = EstimatedSeconds(layout);
            if (estimate < MinSeconds || estimate > MaxSeconds) return null; // 15~30분을 벗어나는 배치는 버린다
            layout.MinX = 0;
            layout.MaxX = GridWidth - 1;
            layout.MinY = 0;
            layout.MaxY = GridHeight - 1;
            return layout;
        }

        // 나무 잇기 (무작위 크루스칼): 이웃 칸 사이 이음마다 무작위 값을 매기고 작은 것부터 고리가 생기지 않게 잇는다.
        // 위/아래 이음은 값에 VerticalPenalty를 더해 덜 고른다 (세로 방은 퍼즐·마을·정예가 될 수 없어서). 시작 방은 위/아래 문을 갖지 않는다.
        public const double VerticalPenalty = 0.45;

        private static bool BuildTree(RoomLayoutData layout, RoomData[,] byCell, Random rng)
        {
            var start = layout.Rooms[0];
            var edges = new List<(double key, RoomData a, RoomData b, DoorDir dir)>();
            for (int y = 0; y < GridHeight; y++)
                for (int x = 0; x < GridWidth; x++)
                {
                    var a = byCell[x, y];
                    if (a == null) continue;
                    if (x + 1 < GridWidth && byCell[x + 1, y] != null)
                        edges.Add((rng.NextDouble(), a, byCell[x + 1, y], DoorDir.Right));
                    if (y + 1 < GridHeight && byCell[x, y + 1] != null && a != start && byCell[x, y + 1] != start)
                        edges.Add((rng.NextDouble() + VerticalPenalty, a, byCell[x, y + 1], DoorDir.Up));
                }
            edges.Sort((p, q) => p.key.CompareTo(q.key));

            var parent = new Dictionary<RoomData, RoomData>();
            foreach (var room in layout.Rooms) parent[room] = room;
            Func<RoomData, RoomData> Find = r =>
            {
                while (parent[r] != r) r = parent[r] = parent[parent[r]];
                return r;
            };

            int linked = 0;
            foreach (var e in edges)
            {
                if (Find(e.a) == Find(e.b)) continue;
                Link(e.a, e.b, e.dir);
                parent[Find(e.a)] = Find(e.b);
                linked++;
            }

            return linked == layout.Rooms.Count - 1;
        }

        private static bool IsVerticalRoom(RoomData room) => (room.Doors & (DoorDir.Up | DoorDir.Down)) != 0;

        // a에서 dir 쪽 이웃 b와 문으로 잇는다.
        private static void Link(RoomData a, RoomData b, DoorDir dir)
        {
            a.Doors |= dir;
            b.Doors |= Opposite(dir);
        }

        // 시작 방에서 너비 우선으로 거리·부모를 매기고 보스 방을 고른다: 가장 먼 방 중 좌우 문 하나뿐인 방 (왼쪽 문 우선).
        // 나무에서 가장 먼 방은 늘 문이 하나다. 가장 먼 방이 모두 세로 방이면 다시 뽑는다.
        private static bool Measure(RoomLayoutData layout, RoomData start, out RoomData boss)
        {
            boss = null;
            foreach (var room in layout.Rooms) room.Distance = -1;
            start.Distance = 0;
            var queue = new Queue<RoomData>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var dir in Dirs)
                {
                    if ((room.Doors & dir) == 0) continue;
                    var next = layout.Neighbor(room, dir);
                    if (next == null || next.Distance >= 0) continue;
                    next.Distance = room.Distance + 1;
                    next.TreeParentIndex = layout.Rooms.IndexOf(room);
                    next.EntryDoor = Opposite(dir);
                    queue.Enqueue(next);
                }
            }

            int far = 0;
            foreach (var room in layout.Rooms)
            {
                if (room.Distance < 0) return false;
                far = Math.Max(far, room.Distance);
            }
            if (far < MinBossDistance) return false;

            foreach (var room in layout.Rooms)
            {
                if (room.Distance != far || room.IsVertical || room.DoorCount != 1) continue;
                if (boss == null || room.Doors == DoorDir.Left) boss = room;
            }

            if (boss == null) return false;
            float bossDistance = boss.Distance;
            foreach (var r in layout.Rooms) r.Difficulty = Math.Min(1f, r.Distance / bossDistance);
            return true; // 왼쪽 문이면 지형 그대로, 오른쪽 문이면 RoomShaper가 보스 지형을 좌우로 뒤집는다
        }

        // 방 종류 (2026-11-14 23:30·23:37, 2026-11-24 사람 결정): 시작·보스·중간보스 → 마을 1 → 퍼즐 2 → 보상 1~2 → 나머지는 전투 방(일부 정예).
        // 주 경로·막다른 방 개념 없이 문 모양만 본다 (RoomShaper가 그 모양을 안전하게 만들 수 있는 방만):
        //   마을·퍼즐·중간보스·보스 = 좌우 방 (평지 지형). 이중 스위치 발판 퍼즐 = 왼쪽·오른쪽 문이 둘 다 있는 방 (건너가는 퍼즐이라).
        //   보물·샘·전투 = 세로 방도 된다. 이동 방은 만들지 않는다.
        private static bool AssignKinds(RoomLayoutData layout, RoomData boss, Random rng)
        {
            var start = layout.Rooms[0];
            start.Kind = RoomKind.Start;
            start.Chunk = ChunkKind.Start;
            boss.Kind = RoomKind.Boss;
            boss.Chunk = ChunkKind.Boss;

            // 중간보스: 보스 다음으로 먼 좌우 방. 보스 방으로 가는 길목(보스의 조상)은 빼서 중간보스가 보스 바로 앞 관문이 되지 않게 한다.
            // 같은 거리면 위쪽 → 왼쪽 (난수 없음).
            var bossLine = new HashSet<RoomData>();
            for (var r = boss; r.TreeParentIndex >= 0;)
            {
                r = layout.Rooms[r.TreeParentIndex];
                bossLine.Add(r);
            }
            RoomData mid = null;
            foreach (var r in layout.Rooms)
            {
                if (r == boss || r == start || bossLine.Contains(r) || r.IsVertical || r.Distance < MinMidBossDistance) continue;
                if (mid == null || r.Distance > mid.Distance
                    || (r.Distance == mid.Distance && (r.Y > mid.Y || (r.Y == mid.Y && r.X < mid.X))))
                    mid = r;
            }
            if (mid == null) return false;
            mid.Kind = RoomKind.MidBoss;
            mid.Chunk = ChunkKind.EliteArena;
            layout.MidBoss = MidBossFor(seed: layout.Seed);
            mid.MidBoss = layout.MidBoss;

            var open = new List<RoomData>();
            foreach (var r in layout.Rooms) if (r != start && r != boss && r != mid) open.Add(r);
            open.Sort((a, b) =>
            {
                int c = a.Distance.CompareTo(b.Distance);
                if (c != 0) return c;
                c = a.Y.CompareTo(b.Y);
                return c != 0 ? c : a.X.CompareTo(b.X); // 칸이 겹치지 않으니 완전한 순서 (난수로 고르는 마을이 런타임마다 같게)
            });

            // 마을 1개: 좌우 방(건물 두 채가 설 평지), 시작 방에서 VillageMinDistance칸 이상, 보스 방과 격자로 붙지 않은 곳 중 무작위.
            var villages = new List<RoomData>();
            foreach (var r in open)
            {
                if (r.IsVertical || r.Distance < VillageMinDistance) continue;
                if (Math.Abs(r.X - boss.X) + Math.Abs(r.Y - boss.Y) <= 1) continue;
                villages.Add(r);
            }
            if (villages.Count == 0) return false;
            var village = villages[rng.Next(villages.Count)];
            village.Kind = RoomKind.Village;
            village.Chunk = ChunkKind.Rest;
            open.Remove(village);

            // 퍼즐 방 2개 (몹 없음): 첫째 = 이중 스위치 발판 (왼쪽·오른쪽 문이 다 있는 방, 깊이 0.5에 가까운 곳),
            // 둘째 = 등불 순서/거울 룬/바람 상자 (좌우 방, 깊이 0.3에 가까운 곳). 시작 방 바로 옆은 되도록 피한다.
            RoomData bridge = null, deduction = null;
            foreach (var r in open)
            {
                if (r.Doors != (DoorDir.Left | DoorDir.Right)) continue;
                if (bridge == null || PuzzleScore(r, StagePlanner.SwitchBridgeDepth) < PuzzleScore(bridge, StagePlanner.SwitchBridgeDepth)) bridge = r;
            }
            if (bridge == null) return false;
            foreach (var r in open)
            {
                if (r == bridge || r.IsVertical) continue;
                if (deduction == null || PuzzleScore(r, SecondPuzzleDepth) < PuzzleScore(deduction, SecondPuzzleDepth)) deduction = r;
            }
            if (deduction == null) return false;
            bridge.Kind = RoomKind.Puzzle;
            bridge.Chunk = ChunkKind.SwitchBridge;
            deduction.Kind = RoomKind.Puzzle;
            deduction.Chunk = DeductionPuzzle(layout.Seed); // 둘째 퍼즐은 추리 퍼즐 (2026-11-14, Docs/PuzzleRoomDesign.md)
            open.Remove(bridge);
            open.Remove(deduction);

            // 보상 방 1~2개 (보물 → 샘): 세로 방부터(세로 방은 퍼즐·마을이 될 수 없다), 그다음 먼 곳부터.
            // 일반 전투 방 MinCombatRooms개는 남긴다.
            int rewards = Math.Min(MaxRewardRooms, open.Count - MinCombatRooms);
            if (rewards < MinRewardRooms) return false;
            var byReward = new List<RoomData>(open);
            byReward.Sort((a, b) =>
            {
                if (a.IsVertical != b.IsVertical) return a.IsVertical ? -1 : 1;
                int c = b.Distance.CompareTo(a.Distance);
                if (c != 0) return c;
                c = b.Y.CompareTo(a.Y);
                return c != 0 ? c : a.X.CompareTo(b.X);
            });
            for (int i = 0; i < rewards; i++)
            {
                byReward[i].Kind = i % 2 == 0 ? RoomKind.Treasure : RoomKind.Spring;
                byReward[i].Chunk = ChunkKind.Rest;
                open.Remove(byReward[i]);
            }

            // 마나 샘 방 0~1개 (2026-11-24, 1/ManaSpringRule.Chance): 일반 전투 방 MinCombatRooms개와 좌우 전투 방 MinSideCombatRooms개는 남긴다.
            // 세로 방부터 (좌우 전투 방 자리를 덜 뺏게), 같은 쪽에서는 무작위. 배치 난수(rng)와 따로 뽑아 정예 자리는 예전 판과 같게 둔다.
            var springRng = new Random(unchecked(layout.Seed * 31 + 7));
            if (open.Count > MinCombatRooms && springRng.Next(ManaSpringRule.Chance) == 0)
            {
                int side = 0;
                foreach (var r in open) if (!r.IsVertical) side++;
                var candidates = open.FindAll(r => r.IsVertical);
                if (candidates.Count == 0 && side > MinSideCombatRooms) candidates = open.FindAll(r => !r.IsVertical);
                if (candidates.Count > 0)
                {
                    var spring = candidates[springRng.Next(candidates.Count)];
                    spring.Kind = RoomKind.ManaSpring;
                    spring.Chunk = ChunkKind.Rest;
                    open.Remove(spring);
                }
            }

            // 나머지는 모두 전투 방. 좌우 방 일부(깊이 0.3+, 1/3, 최대 2개)는 정예. 일반 전투 방이 MinCombatRooms개에 못 미치면 정예를 덜 둔다.
            // 좌우 일반 전투 방이 MinSideCombatRooms개 아래로 내려가게 하는 정예도 두지 않는다. 좌우 자리가 모자라면 다시 뽑는다.
            int spare = open.Count - MinCombatRooms;
            if (spare < 0) return false;
            int sideSpare = -MinSideCombatRooms;
            foreach (var r in open) if (!r.IsVertical) sideSpare++;
            if (sideSpare < 0) return false;
            int elites = 0;
            foreach (var r in open)
            {
                bool elite = !r.IsVertical && elites < Math.Min(MaxElites, Math.Min(spare, sideSpare)) && r.Difficulty >= 0.3f && rng.Next(3) == 0;
                r.Kind = elite ? RoomKind.Elite : RoomKind.Combat;
                r.Chunk = elite ? ChunkKind.EliteArena : ChunkKind.Arena;
                if (elite) elites++;
            }

            return true;
        }

        public const float SecondPuzzleDepth = 0.3f;

        // 중간보스 고르기: 시드로 후보 중 하나 (배치 난수열을 쓰지 않아 다른 방은 그대로).
        public static readonly MidBossKind[] MidBossPool = { MidBossKind.SlimeKnights, MidBossKind.PrismJelly };
        public static MidBossKind MidBossFor(int seed) => MidBossPool[(int)(unchecked((uint)seed * 2654435761u) % (uint)MidBossPool.Length)];

        // 둘째 퍼즐 방 종류: 시드를 3으로 나눈 나머지로 등불 순서 · 거울 룬 · 바람 상자 (배치 난수열을 쓰지 않아 다른 방은 그대로).
        // 바람 상자는 2026-11-22에 더했다 (예전 %2 순환이라 시드마다 고르는 퍼즐은 바뀐다).
        public static readonly ChunkKind[] SecondPuzzles = { ChunkKind.LanternPuzzle, ChunkKind.MirrorPuzzle, ChunkKind.WindBoxPuzzle };
        public static ChunkKind DeductionPuzzle(int seed) => SecondPuzzles[((seed % SecondPuzzles.Length) + SecondPuzzles.Length) % SecondPuzzles.Length];

        private static double PuzzleScore(RoomData r, float want) =>
            Math.Abs(r.Difficulty - want) + (r.Distance <= 1 ? 1.0 : 0.0);

        // 시작 방에서 가까운 순(거리 → 아래 → 왼쪽)으로 다시 번호를 매긴다. 시작 방이 0번.
        private static void Reindex(RoomLayoutData layout)
        {
            var old = new List<RoomData>(layout.Rooms);
            var sorted = new List<RoomData>(layout.Rooms);
            sorted.Sort((a, b) =>
            {
                int c = a.Distance.CompareTo(b.Distance);
                if (c != 0) return c;
                c = a.Y.CompareTo(b.Y);
                return c != 0 ? c : a.X.CompareTo(b.X);
            });
            var map = new Dictionary<int, int>();
            for (int i = 0; i < sorted.Count; i++) map[old.IndexOf(sorted[i])] = i;
            layout.Rooms.Clear();
            for (int i = 0; i < sorted.Count; i++)
            {
                var r = sorted[i];
                r.Index = i;
                if (r.TreeParentIndex >= 0) r.TreeParentIndex = map[r.TreeParentIndex];
                layout.Rooms.Add(r);
            }
        }

        // 비상용: 다른 난수열로 TryBuild를 더 오래 돌린다 (시드 0~2000 × 목표 10가지에서 한 번도 오지 않음)
        private static RoomLayoutData Snake(int seed, float target)
        {
            var rng = new Random(seed ^ 0x51ab);
            for (int i = 0; i < 10000; i++)
            {
                var layout = TryBuild(seed, rng, target);
                if (layout != null) return layout;
            }

            throw new InvalidOperationException($"방 격자를 만들지 못함 (seed {seed})");
        }

        public static int CountDoors(DoorDir doors)
        {
            int n = 0;
            foreach (var dir in Dirs) if ((doors & dir) != 0) n++;
            return n;
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        public static RoomKind KindOf(ChunkKind chunk)
        {
            switch (chunk)
            {
                case ChunkKind.Start: return RoomKind.Start;
                case ChunkKind.Arena: return RoomKind.Combat;
                case ChunkKind.EliteArena: return RoomKind.Elite;
                case ChunkKind.Rest: return RoomKind.Rest;
                case ChunkKind.Boss: return RoomKind.Boss;
                case ChunkKind.ElementGate:
                case ChunkKind.MovingBridge:
                case ChunkKind.SwitchBridge:
                case ChunkKind.LanternPuzzle:
                case ChunkKind.MirrorPuzzle:
                case ChunkKind.WindBoxPuzzle: return RoomKind.Puzzle;
                default: return RoomKind.Travel;
            }
        }

        // 위/아래 문을 가질 수 있는 방: 일반 전투 방(세로 전투 방)과 보물·샘·마나 샘 방. 퍼즐·마을·정예·보스 방은 좌우 문만 둔다.
        public static bool CanBeVertical(RoomKind kind) =>
            kind == RoomKind.Combat || kind == RoomKind.Treasure || kind == RoomKind.Spring || kind == RoomKind.ManaSpring || kind == RoomKind.Travel || kind == RoomKind.Rest;

        public static (int dx, int dy) Offset(DoorDir dir)
        {
            switch (dir)
            {
                case DoorDir.Left: return (-1, 0);
                case DoorDir.Right: return (1, 0);
                case DoorDir.Up: return (0, 1);
                case DoorDir.Down: return (0, -1);
                default: return (0, 0);
            }
        }

        public static DoorDir Opposite(DoorDir dir)
        {
            switch (dir)
            {
                case DoorDir.Left: return DoorDir.Right;
                case DoorDir.Right: return DoorDir.Left;
                case DoorDir.Up: return DoorDir.Down;
                case DoorDir.Down: return DoorDir.Up;
                default: return DoorDir.None;
            }
        }
    }
}
