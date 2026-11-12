using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 방 격자형 스테이지의 배치 (Docs/LevelDesign.md). 던그리드·아이작처럼 방들이 격자 칸에 놓이고 문으로 이어진다.
    // 스테이지 1 = 가로 4 × 세로 3 격자에 방 9~12개 (2026-11-12 사람 확정). 시작 방은 가운데 줄 안쪽 두 칸 중 하나,
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
        Travel,    // 이동 방 (지형·함정·쫄몹). 세로 방도 여기
        Puzzle,    // 봉인문·움직이는 발판
        Combat,    // 들어가면 문이 잠기는 전투 방
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
        public const int GridWidth = 4;
        public const int GridHeight = 3;
        public const int MinRooms = 9;
        public const int MaxRooms = 12;
        public const int MinBossDistance = 4;        // 시작 → 보스 문 수 (중간보스 앞뒤에 방이 하나씩은 있게)
        public const float MidBossDepth = 0.55f;  // 중간보스는 보스까지 거리의 55% 안팎 (사람 결정 2026-11-09)
        public const int VillageMinDistance = 2;     // 마을은 시작 방에서 문 두 개 이상 (골드를 조금 모은 뒤 찾게)
        public const int MinRewardRooms = 2;         // 막다른 보상 곁방 (보물 상자 / 회복 샘)
        public const int MaxRewardRooms = 3;
        public const float MinSeconds = 15f * 60f;   // 스테이지 목표 15~30분 (사람 확정 2026-11-12)
        public const float MaxSeconds = 30f * 60f;
        public const float BacktrackSeconds = 12f;   // 곁가지를 들어갔다 나오는 데 문 하나당 더 드는 시간 (어림)
        private const int MaxAttempts = 400;
        private const int MaxElites = 2;
        public const int MinFights = 1;              // 중간보스·보스 말고 일반/정예 전투 방 최소 수
        public const float MinFightDifficulty = 0.15f; // 시작 방 바로 옆은 보스 거리가 짧을 때(4~5)만 전투 방이 될 수 있다

        // 넓은 방: 좌우 이동·퍼즐·전투·정예·중간보스·보스 방은 앞 구간 2개(3화면), 휴식 방은 1개(2화면). 시작·보물·샘·세로 방은 한 화면.
        // 격자 한 칸의 가로 크기는 가장 넓은 방에 맞춘다 (ChunkStreamer 원점이 칸마다 같은 간격).
        public const int MaxLeads = 2;
        public const int CellCols = (1 + MaxLeads) * ChunkData.Width;

        public static int LeadCount(RoomData room)
        {
            if (room.IsVertical) return 0;
            switch (room.Kind)
            {
                case RoomKind.Travel:
                case RoomKind.Puzzle:
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

        // 앞 구간 종류: 이동 지형만 (봉인문·움직이는 발판 제외 — 보스 방 좌우 뒤집기가 움직이는 발판을 못 다루고, 퍼즐은 방마다 하나).
        // 배치 난수열과 따로 굴려 방 배치·종류가 넓은 방 도입 전과 같게 남는다.
        private static void AssignLeads(RoomLayoutData layout)
        {
            var rng = new Random(unchecked(layout.Seed * 7368787 + 2017));
            foreach (var room in layout.Rooms)
            {
                room.Leads.Clear();
                int count = LeadCount(room);
                var last = room.Chunk;
                for (int i = 0; i < count; i++)
                {
                    var kind = StagePlanner.PickTravel(rng, room.Difficulty, last, false);
                    if (kind == ChunkKind.MovingBridge || kind == ChunkKind.ElementGate) kind = last == ChunkKind.Hills ? ChunkKind.Plains : ChunkKind.Hills;
                    room.Leads.Add(kind);
                    last = kind;
                }
            }
        }

        private static readonly DoorDir[] Dirs = { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down };

        // targetSeconds: 목표 시간 (15~30분으로 자른다). 방 수와 전투 방 수가 이것을 따라간다.
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
            // 목표가 길수록 방이 많다 (15분 → 9~11, 30분 → 12)
            float t = (target - MinSeconds) / (MaxSeconds - MinSeconds);
            int count = Math.Min(MaxRooms, rng.Next(MinRooms, MaxRooms) + (int)Math.Round(t * 3));
            int startX = rng.Next(1, 3);
            var cells = new bool[GridWidth, GridHeight];
            cells[startX, 1] = true;
            var free = new List<(int x, int y)>();
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                    if (!(x == startX && y == 1)) free.Add((x, y));
            Shuffle(free, rng);
            for (int i = 0; i < count - 1; i++) cells[free[i].x, free[i].y] = true;

            var layout = new RoomLayoutData { Seed = seed };
            var byCell = new RoomData[GridWidth, GridHeight];
            var order = new List<(int x, int y)> { (startX, 1) };
            for (int y = 0; y < GridHeight; y++)
                for (int x = 0; x < GridWidth; x++)
                    if (cells[x, y] && !(x == startX && y == 1)) order.Add((x, y));
            foreach (var cell in order)
            {
                var room = new RoomData { X = cell.x, Y = cell.y };
                byCell[cell.x, cell.y] = room;
                layout.Rooms.Add(room);
            }

            if (!BuildTree(layout, byCell, rng)) return null;
            if (!Measure(layout, byCell[startX, 1], out var boss)) return null;

            MarkMainPath(layout, boss);
            if (!AssignKinds(layout, rng, target)) return null;
            Reindex(layout);
            AssignLeads(layout);
            if (EstimatedSeconds(layout) < MinSeconds) return null; // 15분에 못 미치는 배치(방 9개·전투 방 적음)는 버린다
            layout.MinX = 0;
            layout.MaxX = GridWidth - 1;
            layout.MinY = 0;
            layout.MaxY = GridHeight - 1;
            return layout;
        }

        // 나무 잇기 (무작위 크루스칼): 이웃 칸 사이 이음마다 무작위 값을 매기고 작은 것부터 고리가 생기지 않게 잇는다.
        // 위/아래 이음은 값에 VerticalPenalty를 더해 덜 고른다 (세로 방은 전투·퍼즐이 될 수 없어서). 시작 방은 위/아래 문을 갖지 않는다.
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

        // 방 종류: 시작·보스·중간보스(+뒤 휴식)·보상 곁방을 먼저 정하고, 나머지는 거리(난이도)에 따라 이동·퍼즐·전투.
        private static bool AssignKinds(RoomLayoutData layout, Random rng, float target)
        {
            var start = layout.Rooms[0];
            start.Kind = RoomKind.Start;
            start.Chunk = ChunkKind.Start;
            RoomData boss = null;
            var path = new List<RoomData>();
            foreach (var r in layout.Rooms) if (r.MainPath) path.Add(r);
            path.Sort((a, b) => a.PathIndex.CompareTo(b.PathIndex));
            boss = path[path.Count - 1];
            boss.Kind = RoomKind.Boss;
            boss.Chunk = ChunkKind.Boss;

            // 중간보스: 주 경로 2 ~ 보스-2 중 좌우 방, 55%에 가장 가까운 곳. 바로 뒤 방은 숨 고르기
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
            if (mid == null) return false;
            mid.Kind = RoomKind.MidBoss;
            mid.Chunk = ChunkKind.EliteArena;
            layout.MidBossPathIndex = mid.PathIndex;
            var afterMid = path[mid.PathIndex + 1];
            if (afterMid != boss)
            {
                afterMid.Kind = RoomKind.Rest;
                afterMid.Chunk = ChunkKind.Rest;
            }

            // 막다른 곁방 (주 경로 밖, 문 하나)
            var leaves = new List<RoomData>();
            foreach (var r in layout.Rooms) if (!r.MainPath && DoorCount(r) == 1) leaves.Add(r);

            // 마을: 판마다 하나. 좌우 방(건물 두 채가 설 평지), 시작 방에서 VillageMinDistance칸 이상, 보스 방과 격자로 붙지 않은 곳.
            // 난수를 쓰지 않고 정한다 (가장 먼 곳 → 위쪽 → 왼쪽). 후보가 없으면 이 배치는 버린다 (Generate가 다시 뽑음).
            RoomData village = null;
            foreach (var r in leaves)
            {
                if (r.IsVertical || r.Distance < VillageMinDistance) continue;
                if (Math.Abs(r.X - boss.X) + Math.Abs(r.Y - boss.Y) <= 1) continue;
                if (village == null || r.Distance > village.Distance
                    || (r.Distance == village.Distance && (r.Y > village.Y || (r.Y == village.Y && r.X < village.X))))
                    village = r;
            }
            if (village == null) return false;
            village.Kind = RoomKind.Village;
            village.Chunk = ChunkKind.Rest;
            leaves.Remove(village);

            // 보상 곁방: 남은 막다른 방 중 2~3개 (먼 곳부터). 마을이 하나를 가져가 모자라면 보물 하나만 (회복 샘 자리를 마을이 대신),
            // 하나도 없으면 이 배치는 버린다
            if (leaves.Count < 1) return false;
            // 세로 막다른 방부터 (세로 방은 전투·퍼즐이 될 수 없으니 보상으로 쓰는 게 낫다), 그다음 먼 곳부터
            leaves.Sort((a, b) =>
            {
                if (a.IsVertical != b.IsVertical) return a.IsVertical ? -1 : 1;
                return b.Distance.CompareTo(a.Distance);
            });
            int rewards = Math.Min(layout.Rooms.Count >= MaxRooms ? MaxRewardRooms : MinRewardRooms, leaves.Count);
            for (int i = 0; i < rewards; i++)
            {
                leaves[i].Kind = i % 2 == 0 ? RoomKind.Treasure : RoomKind.Spring;
                leaves[i].Chunk = ChunkKind.Rest;
            }

            // 나머지 방 (거리 순서로): 세로 문이 있으면 이동 방, 좌우 방은 이동·퍼즐·전투
            var rest = new List<RoomData>();
            foreach (var r in layout.Rooms) if (r.Chunk == ChunkKind.Start && r != start) rest.Add(r); // Chunk 기본값 = Start → 아직 안 정한 방
            rest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var last = ChunkKind.Start;
            bool gatePlaced = false;
            foreach (var r in rest)
            {
                r.Kind = RoomKind.Travel;
                r.Chunk = StagePlanner.PickTravel(rng, r.Difficulty, last, !gatePlaced && !r.IsVertical);
                if (r.IsVertical && r.Chunk == ChunkKind.ElementGate) r.Chunk = ChunkKind.Plains;
                if (r.Chunk == ChunkKind.ElementGate) gatePlaced = true;
                if (!r.IsVertical && (r.Chunk == ChunkKind.ElementGate || r.Chunk == ChunkKind.MovingBridge)) r.Kind = RoomKind.Puzzle;
                last = r.Chunk;
            }

            // 이중 스위치 발판: 판마다 하나. 깊이(시작 방 거리 / 보스 거리) 0.5에 가장 가까운 좌우 이동·퍼즐 방
            RoomData bridge = null;
            // 주 경로 우선, 시작 방 바로 옆(거리 1)은 전투 방 후보로 남기려고 뒤로 미룬다
            foreach (var r in rest)
            {
                if (r.IsVertical || r.Chunk == ChunkKind.ElementGate) continue;
                if (bridge == null || BridgeScore(r) < BridgeScore(bridge)) bridge = r;
            }
            if (bridge == null) return false;
            bridge.Kind = RoomKind.Puzzle;
            bridge.Chunk = ChunkKind.SwitchBridge;

            // 전투: 깊이 0.15 이후 좌우 이동·퍼즐 방을 예상 시간이 목표에 닿을 때까지 전투 방으로 (멀수록 먼저 → 정예는 30% 이후 먼 곳에 최대 2개).
            // 주 경로에서는 문 잠기는 방이 연달아 오지 않게 한다.
            var candidates = new List<RoomData>();
            foreach (var r in rest)
                if (!r.IsVertical && r != bridge && r.Chunk != ChunkKind.ElementGate && r.Difficulty >= MinFightDifficulty) candidates.Add(r);
            Shuffle(candidates, rng);
            candidates.Sort((a, b) => b.Distance.CompareTo(a.Distance)); // List.Sort는 불안정하지만 난수 섞기 뒤라 같은 거리 안 순서는 시드마다 달라진다
            int elites = 0;
            int fights = 0;
            foreach (var r in candidates)
            {
                if (fights >= MinFights && EstimatedSeconds(layout) >= target) break;
                if (r.MainPath && (Locks(layout, path, r.PathIndex - 1) || Locks(layout, path, r.PathIndex + 1))) continue;
                bool elite = elites < MaxElites && r.Difficulty >= 0.3f && rng.Next(3) == 0;
                r.Kind = elite ? RoomKind.Elite : RoomKind.Combat;
                r.Chunk = elite ? ChunkKind.EliteArena : ChunkKind.Arena;
                if (elite) elites++;
                fights++;
            }

            return fights >= MinFights; // 전투 방이 모자란 배치(세로 방이 너무 많은 경우)는 버린다
        }

        private static double BridgeScore(RoomData r) =>
            Math.Abs(r.Difficulty - StagePlanner.SwitchBridgeDepth) + (r.Distance <= 1 ? 1.0 : 0.0) + (r.MainPath ? 0.0 : 0.6); // 주 경로 우선. 다만 4×3 격자의 주 경로는 대개 세로 방·중간보스·휴식·보스로 차서 곁가지에 놓인다

        private static bool Locks(RoomLayoutData layout, List<RoomData> path, int index) =>
            index >= 0 && index < path.Count && path[index].LocksDoors;

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
                case ChunkKind.SwitchBridge: return RoomKind.Puzzle;
                default: return RoomKind.Travel;
            }
        }

        // 위/아래 문을 가질 수 있는 방: 이동 방과 휴식 방. 전투·퍼즐·보스 방은 가로로 지나가는 지형이라 좌우 문만 둔다.
        public static bool CanBeVertical(RoomKind kind) => kind == RoomKind.Travel || kind == RoomKind.Rest;

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
