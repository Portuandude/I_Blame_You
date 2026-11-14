using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 방 격자형 스테이지의 배치 (Docs/LevelDesign.md). 던그리드·아이작처럼 방들이 격자 칸에 놓이고 문으로 이어진다.
    // 스테이지 1 = 가로 5 × 세로 4 격자에 방 13~17개 (2026-11-14 사람 결정 "일반 전투 방 5+ · 퍼즐 방 2+ · 마을 1~2"로 4×3·9~12에서 늘림).
    // 이동 방(기믹만 있는 방)은 없앴다: 기믹(버섯·바람·무너지는 발판·봉인문·움직이는 발판)은 전투 방 앞 구간에 섞이고, 세로 방도 전투 방이다.
    // 시작 방은 가운데 열(x 2), 가운데 두 줄 중 하나,
    // 방들은 나무 모양(고리 없음)으로 이어지고, 시작 방에서 멀수록(방 거리) 어렵다. 가장 먼 방이 보스, 중간 거리에 중간보스.
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
        Puzzle,    // 퍼즐 방: 몹이 나오지 않는다 (2026-11-14). 이중 스위치 발판 하나 + 퍼즐 하나 (등불 순서·거울 룬·방어막 다리) (Docs/PuzzleRoomDesign.md)
        Combat,    // 들어가면 문이 잠기는 전투 방. 좌우 방은 앞 구간에 기믹 지형, 세로 방(탑·굴)은 바닥 전체가 전투 구역
        Elite,     // 정예 전투 방 (유물 상자)
        Rest,      // 숨 고르기 방 (전투 사이 완충. 체크포인트는 2026-11-10 제거)
        MidBoss,   // 중간보스 방 (보스까지 거리의 55% 안팎)
        Boss,      // 마지막 방 + 출구
        Treasure,  // 막다른 곁방: 유물 상자
        Spring,    // 막다른 곁방: 회복 샘
        Village    // 막다른 곁방: NPC 마을 (식당·장비 상점, 적·함정 없음). 스테이지마다 하나, 방을 돌아다니며 찾는다 (2026-11-12 사람 결정)
    }

    public class RoomData
    {
        public int Index;            // Rooms 목록 안의 번호 (주 경로가 먼저, 곁방이 뒤)
        public int X, Y;             // 격자 칸 (오른쪽 +x, 위 +y)
        public RoomKind Kind;
        public ChunkKind Chunk;      // 방 안 지형을 만들 청크 종류
        public float Difficulty;     // 0(시작) ~ 1(끝)
        public DoorDir Doors;        // 이 방에 있는 문
        public DoorDir EntryDoor;    // 주 경로에서 들어오는 문 (시작 방과 곁방은 연결된 쪽)
        public DoorDir ExitDoor;     // 주 경로에서 나가는 문 (보스 방과 곁방은 None)
        public bool MainPath;        // 시작 → 보스 경로 위의 방
        public int PathIndex = -1;   // 주 경로 순번 (곁방은 -1)
        public int ParentIndex = -1; // 곁방: 가장 가까운 주 경로 조상 방의 Index (깊이·"쓰러진 곳" 기준)
        public int TreeParentIndex = -1; // 시작 방 쪽으로 한 칸 (시작 방은 -1)
        public int Distance;         // 시작 방에서 문을 몇 번 지나야 오는지 (방 거리). 난이도 = 거리 / 보스 거리
        // 넓은 방 (2026-11-12 "큰 방"): 주 구간(Chunk) 앞, 들어오는 문 쪽에 이어 붙이는 이동 구간들. 비어 있으면 한 화면 방.
        public readonly List<ChunkKind> Leads = new List<ChunkKind>();
        public int Cols => (1 + Leads.Count) * ChunkData.Width; // 방 가로 칸 수

        // 위/아래 문이 있는 방은 세로로 오르내리는 방(탑·굴)이다.
        // 곁방으로 가는 위/아래 문도 포함한다 (지형을 세로 방으로 만들어야 하므로).
        public bool IsVertical => (Doors & (DoorDir.Up | DoorDir.Down)) != 0;
        // 주 경로가 이 방에서 위/아래로 오르내리는지 (세로 플레이 비중은 이것으로 잰다).
        public bool IsVerticalPassage => MainPath && ((EntryDoor | ExitDoor) & (DoorDir.Up | DoorDir.Down)) != 0;
        public bool IsDeadEnd => !MainPath;
        public bool LocksDoors => Kind == RoomKind.Combat || Kind == RoomKind.Elite || Kind == RoomKind.MidBoss || Kind == RoomKind.Boss;
    }

    public class RoomLayoutData
    {
        public readonly List<RoomData> Rooms = new List<RoomData>();
        public int Seed;
        public int MidBossPathIndex;

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

        public IEnumerable<RoomData> MainPathRooms()
        {
            foreach (var room in Rooms) if (room.MainPath) yield return room;
        }
    }

    public static class RoomLayout
    {
        public const int GridWidth = 5;
        public const int GridHeight = 4;
        public const int MinRooms = 13;
        public const int MaxRooms = 17;
        public const int StartX = 2;                 // 시작 방: 가운데 열, y 1 또는 2
        // 방 구성 하한 (2026-11-14 사람 결정)
        public const int MinCombatRooms = 5;         // 일반 전투 방 (정예·중간보스·보스 제외)
        public const int MinPuzzleRooms = 2;         // 퍼즐 방 (몹 없음)
        public const int MaxVillages = 2;            // 마을 1~2개: 방이 SecondVillageRooms개 이상인 큰 판에서 두 번째 마을
        public const int SecondVillageRooms = 16;
        public const int MinBossDistance = 4;        // 시작 → 보스 문 수 (중간보스 앞뒤에 방이 하나씩은 있게)
        public const float MidBossDepth = 0.55f;  // 중간보스는 보스까지 거리의 55% 안팎 (사람 결정 2026-11-09)
        public const int VillageMinDistance = 2;     // 마을은 시작 방에서 문 두 개 이상 (골드를 조금 모은 뒤 찾게)
        public const int MinRewardRooms = 1;         // 막다른 보상 방 (보물 상자 / 회복 샘)
        public const int MaxRewardRooms = 2;
        public const float MinSeconds = 15f * 60f;   // 스테이지 목표 15~30분 (사람 확정 2026-11-12)
        public const float MaxSeconds = 30f * 60f;
        public const float BacktrackSeconds = 12f;   // 곁가지를 들어갔다 나오는 데 문 하나당 더 드는 시간 (어림)
        private const int MaxAttempts = 400;
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
        public static RoomLayoutData Generate(int seed, float targetSeconds)
        {
            float target = Math.Max(MinSeconds, Math.Min(MaxSeconds, targetSeconds));
            var rng = new Random(seed);
            RoomLayoutData best = null;
            for (int attempt = 0; attempt < MaxAttempts && best == null; attempt++)
                best = TryBuild(seed, rng, target);
            if (best == null) best = Snake(seed, target); // 이론상 오지 않는다 (테스트가 지킨다)
            return best;
        }

        // 방 거리 기준 예상 시간: 방마다 StagePlanner 어림 + 곁가지 왕복(문 하나당 BacktrackSeconds × 2).
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
                if (!room.MainPath) total += 2f * BacktrackSeconds;
            }

            return total;
        }

        private static RoomLayoutData TryBuild(int seed, Random rng, float target)
        {
            // 목표가 길수록 방이 많다 (15분 → 13~14, 30분 → 16~17)
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

            MarkMainPath(layout, boss);
            if (!AssignKinds(layout, rng, target)) return null;
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

        // 시작 방에서 너비 우선으로 거리·부모를 매기고 보스 방을 고른다: 가장 먼 막다른 방 중 좌우 문 하나뿐인 방 (왼쪽 문 우선).
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
                if (room.Distance != far || room.IsVertical || DoorCount(room) != 1) continue;
                if (boss == null || room.Doors == DoorDir.Left) boss = room;
            }

            return boss != null; // 왼쪽 문이면 지형 그대로, 오른쪽 문이면 RoomShaper가 보스 지형을 좌우로 뒤집는다
        }

        // 보스 → 시작으로 거슬러 올라가며 주 경로를 표시한다. 곁가지 방의 ParentIndex는 가장 가까운 주 경로 조상.
        private static void MarkMainPath(RoomLayoutData layout, RoomData boss)
        {
            var room = boss;
            while (room != null)
            {
                room.MainPath = true;
                room.PathIndex = room.Distance;
                if (room.TreeParentIndex < 0) break;
                var up = layout.Rooms[room.TreeParentIndex];
                up.ExitDoor = Opposite(room.EntryDoor);
                room = up;
            }

            foreach (var r in layout.Rooms)
            {
                if (r.MainPath) continue;
                var a = r;
                while (!a.MainPath) a = layout.Rooms[a.TreeParentIndex];
                r.ParentIndex = layout.Rooms.IndexOf(a);
            }

            float bossDistance = boss.Distance;
            foreach (var r in layout.Rooms) r.Difficulty = Math.Min(1f, r.Distance / bossDistance);
        }

        // 방 종류 (2026-11-14 사람 결정): 시작·보스·중간보스 → 마을(1~2)·보상 막다른 방 → 퍼즐 방 2개 → 나머지는 모두 전투 방.
        // 이동 방은 만들지 않는다. 세로 방은 전투·보물·샘만 될 수 있다 (퍼즐·마을·보스는 좌우 지형).
        private static bool AssignKinds(RoomLayoutData layout, Random rng, float target)
        {
            var start = layout.Rooms[0];
            start.Kind = RoomKind.Start;
            start.Chunk = ChunkKind.Start;
            var path = new List<RoomData>();
            foreach (var r in layout.Rooms) if (r.MainPath) path.Add(r);
            path.Sort((a, b) => a.PathIndex.CompareTo(b.PathIndex));
            var boss = path[path.Count - 1];
            boss.Kind = RoomKind.Boss;
            boss.Chunk = ChunkKind.Boss;

            // 중간보스: 주 경로 2 ~ 보스-2 중 좌우 방, 55%에 가장 가까운 곳
            RoomData mid = null;
            double bestGap = double.MaxValue;
            for (int i = 2; i <= path.Count - 3; i++)
            {
                if (path[i].IsVertical) continue;
                double gap = Math.Abs(i / (double)(path.Count - 1) - MidBossDepth);
                if (gap < bestGap - 1e-9)
                {
                    bestGap = gap;
                    mid = path[i];
                }
            }
            if (mid == null || bestGap > 1.0 / (path.Count - 1) + 1e-6) return false; // 55%에서 한 칸 넘게 벗어나면 다시 뽑는다
            mid.Kind = RoomKind.MidBoss;
            mid.Chunk = ChunkKind.EliteArena;
            layout.MidBossPathIndex = mid.PathIndex;

            // 막다른 방 (주 경로 밖, 문 하나)
            var leaves = new List<RoomData>();
            foreach (var r in layout.Rooms) if (!r.MainPath && DoorCount(r) == 1) leaves.Add(r);

            // 마을: 좌우 방(건물 두 채가 설 평지), 시작 방에서 VillageMinDistance칸 이상, 보스 방과 격자로 붙지 않은 곳.
            // 난수를 쓰지 않고 정한다 (가장 먼 곳 → 위쪽 → 왼쪽). 방이 SecondVillageRooms개 이상이면 첫 마을과 떨어진(격자 거리 3+) 두 번째 마을.
            int wantVillages = layout.Rooms.Count >= SecondVillageRooms ? MaxVillages : 1;
            var villages = new List<RoomData>();
            for (int v = 0; v < wantVillages; v++)
            {
                RoomData village = null;
                foreach (var r in leaves)
                {
                    if (r.IsVertical || r.Distance < VillageMinDistance) continue;
                    if (Math.Abs(r.X - boss.X) + Math.Abs(r.Y - boss.Y) <= 1) continue;
                    if (villages.Exists(o => Math.Abs(r.X - o.X) + Math.Abs(r.Y - o.Y) < 3)) continue;
                    if (village == null || r.Distance > village.Distance
                        || (r.Distance == village.Distance && (r.Y > village.Y || (r.Y == village.Y && r.X < village.X))))
                        village = r;
                }
                if (village == null) break;
                village.Kind = RoomKind.Village;
                village.Chunk = ChunkKind.Rest;
                leaves.Remove(village);
                villages.Add(village);
            }
            if (villages.Count == 0) return false;

            // 보상 막다른 방: 세로 막다른 방부터(세로 방은 퍼즐·마을이 될 수 없다), 그다음 먼 곳부터 1~2개 (보물 → 샘)
            if (leaves.Count < 1) return false;
            leaves.Sort((a, b) =>
            {
                if (a.IsVertical != b.IsVertical) return a.IsVertical ? -1 : 1;
                return b.Distance.CompareTo(a.Distance);
            });
            int rewards = Math.Min(layout.Rooms.Count >= MinRooms + 1 ? MaxRewardRooms : MinRewardRooms, leaves.Count);
            for (int i = 0; i < rewards; i++)
            {
                leaves[i].Kind = i % 2 == 0 ? RoomKind.Treasure : RoomKind.Spring;
                leaves[i].Chunk = ChunkKind.Rest;
            }

            var rest = new List<RoomData>();
            foreach (var r in layout.Rooms) if (r.Chunk == ChunkKind.Start && r != start) rest.Add(r); // Chunk 기본값 = Start → 아직 안 정한 방
            rest.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // 퍼즐 방 2개 (몹 없음): 좌우 방 중 깊이 0.5(이중 스위치)와 0.3·0.75에 가까운 곳. 중간보스 바로 뒤 좌우 방이면 그 방을 먼저
            // (전투가 끝난 뒤 숨 고르는 자리 — 옛 숨 고르기 방 자리). 첫 퍼즐은 이중 스위치 발판, 둘째는 등불 순서/거울 룬/방어막 다리 (2026-11-14, 예전 속성 봉인문).
            var puzzles = new List<RoomData>();
            var afterMid = path[mid.PathIndex + 1];
            if (afterMid != boss && !afterMid.IsVertical && rest.Contains(afterMid)) puzzles.Add(afterMid);
            float[] wants = { StagePlanner.SwitchBridgeDepth, 0.3f, 0.75f };
            foreach (float want in wants)
            {
                if (puzzles.Count >= MinPuzzleRooms) break;
                RoomData pick = null;
                foreach (var r in rest)
                {
                    if (r.IsVertical || puzzles.Contains(r)) continue;
                    if (pick == null || PuzzleScore(r, want) < PuzzleScore(pick, want)) pick = r;
                }
                if (pick != null) puzzles.Add(pick);
            }
            if (puzzles.Count < MinPuzzleRooms) return false;
            // 이중 스위치 발판은 더 깊은 쪽(0.5에 가까운 쪽)에, 등불 순서는 다른 쪽에
            puzzles.Sort((a, b) => Math.Abs(a.Difficulty - StagePlanner.SwitchBridgeDepth).CompareTo(Math.Abs(b.Difficulty - StagePlanner.SwitchBridgeDepth)));
            for (int i = 0; i < puzzles.Count; i++)
            {
                puzzles[i].Kind = RoomKind.Puzzle;
                puzzles[i].Chunk = i == 0 ? ChunkKind.SwitchBridge : DeductionPuzzle(layout.Seed); // 둘째부터 추리 퍼즐 (2026-11-14, Docs/PuzzleRoomDesign.md)
                rest.Remove(puzzles[i]);
            }

            // 나머지는 모두 전투 방. 좌우 방 일부(깊이 0.3+, 1/3, 최대 2개)는 정예. 일반 전투 방이 MinCombatRooms개에 못 미치면 정예를 덜 둔다.
            int spare = rest.Count - MinCombatRooms;
            if (spare < 0) return false;
            int elites = 0;
            foreach (var r in rest)
            {
                bool elite = !r.IsVertical && elites < Math.Min(MaxElites, spare) && r.Difficulty >= 0.3f && rng.Next(3) == 0;
                r.Kind = elite ? RoomKind.Elite : RoomKind.Combat;
                r.Chunk = elite ? ChunkKind.EliteArena : ChunkKind.Arena;
                if (elite) elites++;
            }

            return true;
        }

        // 둘째 퍼즐 방 종류: 시드를 3으로 나눈 나머지로 등불 순서 · 거울 룬 · 방어막 다리 (배치 난수열을 쓰지 않아 다른 방은 그대로).
        private static readonly ChunkKind[] SecondPuzzles = { ChunkKind.LanternPuzzle, ChunkKind.MirrorPuzzle, ChunkKind.ShieldBridge };
        public static ChunkKind DeductionPuzzle(int seed) => SecondPuzzles[((seed % 3) + 3) % 3];

        private static double PuzzleScore(RoomData r, float want) =>
            Math.Abs(r.Difficulty - want) + (r.Distance <= 1 ? 1.0 : 0.0);

        // 주 경로가 먼저(PathIndex 순), 곁가지가 뒤(거리 순)인 Rooms 순서로 다시 번호를 매긴다.
        private static void Reindex(RoomLayoutData layout)
        {
            var old = new List<RoomData>(layout.Rooms);
            var sorted = new List<RoomData>(layout.Rooms);
            sorted.Sort((a, b) =>
            {
                if (a.MainPath != b.MainPath) return a.MainPath ? -1 : 1;
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
                if (r.ParentIndex >= 0) r.ParentIndex = map[r.ParentIndex];
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

        private static int DoorCount(RoomData room)
        {
            int n = 0;
            foreach (var dir in Dirs) if ((room.Doors & dir) != 0) n++;
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
                case ChunkKind.ShieldBridge: return RoomKind.Puzzle;
                default: return RoomKind.Travel;
            }
        }

        // 위/아래 문을 가질 수 있는 방: 일반 전투 방(세로 전투 방)과 보물·샘 막다른 방. 퍼즐·마을·정예·보스 방은 좌우 문만 둔다.
        public static bool CanBeVertical(RoomKind kind) =>
            kind == RoomKind.Combat || kind == RoomKind.Treasure || kind == RoomKind.Spring || kind == RoomKind.Travel || kind == RoomKind.Rest;

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
