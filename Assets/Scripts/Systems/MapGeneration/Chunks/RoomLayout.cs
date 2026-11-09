using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 방 격자형 스테이지의 배치 (Docs/LevelDesign.md). 던그리드·아이작처럼 방들이 격자 칸에 놓이고 문으로 이어진다.
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
        Rest,      // 체크포인트 (회복)
        MidBoss,   // 중간보스 방 (경로의 55%)
        Boss,      // 마지막 방 + 출구
        Treasure,  // 막다른 곁방: 유물 상자
        Spring     // 막다른 곁방: 회복 샘
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
        public int ParentIndex = -1; // 곁방이 붙은 주 경로 방의 Index

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
        public const float MidBossProgress = 0.55f;  // 중간보스는 주 경로의 55% 지점 (사람 결정 2026-11-09)
        public const int MaxRise = 4;                // 시작 방 높이에서 위아래로 이만큼까지만
        public const int MaxVerticalRun = 2;         // 위/아래로 연달아 이어지는 방은 최대 2번
        public const int MinSideRooms = 2;
        public const int MaxSideRooms = 4;
        private const int SideSalt = 0x5eed;

        // 주 경로 방 순서는 StagePlanner의 청크 순서를 그대로 쓰고, 55% 자리에 중간보스 방을 끼운다.
        public static RoomLayoutData Generate(int seed, float targetSeconds)
        {
            var plan = StagePlanner.Plan(seed, targetSeconds);
            var path = new List<(RoomKind kind, ChunkKind chunk, float difficulty)>();
            foreach (var entry in plan) path.Add((KindOf(entry.Key), entry.Key, entry.Value));

            int midIndex = MidBossIndex(path.Count + 1);
            float midDifficulty = path[Math.Min(midIndex, path.Count - 1)].difficulty;
            path.Insert(midIndex, (RoomKind.MidBoss, ChunkKind.EliteArena, midDifficulty));

            var layout = new RoomLayoutData { Seed = seed, MidBossPathIndex = midIndex };
            var rng = new Random(seed);
            PlaceMainPath(layout, path, rng);
            PlaceSideRooms(layout, new Random(seed ^ SideSalt));
            UpdateBounds(layout);
            return layout;
        }

        // 방 count개짜리 주 경로에서 중간보스가 설 순번.
        public static int MidBossIndex(int count) => (int)Math.Round(MidBossProgress * (count - 1), MidpointRounding.AwayFromZero);

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
                case ChunkKind.MovingBridge: return RoomKind.Puzzle;
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

        // 주 경로: 시작 방(0,0)에서 오른쪽으로 나아가며, 이동 방끼리는 가끔 위/아래로 이어진다.
        // x는 줄지 않으므로 오른쪽 칸은 항상 비어 있다 (막다른 길이 생기지 않는다).
        private static void PlaceMainPath(RoomLayoutData layout, List<(RoomKind kind, ChunkKind chunk, float difficulty)> path, Random rng)
        {
            int x = 0, y = 0, verticalRun = 0;
            DoorDir lastVertical = DoorDir.None;
            RoomData previous = null;
            for (int i = 0; i < path.Count; i++)
            {
                var room = new RoomData
                {
                    Index = layout.Rooms.Count, Kind = path[i].kind, Chunk = path[i].chunk, Difficulty = path[i].difficulty,
                    MainPath = true, PathIndex = i
                };

                if (previous != null)
                {
                    var step = DoorDir.Right;
                    bool vertical = CanBeVertical(previous.Kind) && CanBeVertical(room.Kind) && previous.Kind != RoomKind.Start
                        && verticalRun < MaxVerticalRun;
                    if (vertical)
                    {
                        // 오른쪽 3 : 위 1 : 아래 1. 같은 세로 줄에서 방향을 바꾸면 지나온 칸과 겹치므로 같은 방향만 잇는다.
                        bool upOk = y + 1 <= MaxRise && (lastVertical == DoorDir.None || lastVertical == DoorDir.Up) && layout.At(x, y + 1) == null;
                        bool downOk = y - 1 >= -MaxRise && (lastVertical == DoorDir.None || lastVertical == DoorDir.Down) && layout.At(x, y - 1) == null;
                        int roll = rng.Next(5);
                        if (roll == 3 && upOk) step = DoorDir.Up;
                        else if (roll == 4 && downOk) step = DoorDir.Down;
                        else if (roll >= 3 && (upOk || downOk)) step = upOk ? DoorDir.Up : DoorDir.Down;
                    }

                    var (dx, dy) = Offset(step);
                    x += dx;
                    y += dy;
                    if (step == DoorDir.Right)
                    {
                        verticalRun = 0;
                        lastVertical = DoorDir.None;
                    }
                    else
                    {
                        verticalRun++;
                        lastVertical = step;
                    }

                    previous.ExitDoor = step;
                    previous.Doors |= step;
                    room.EntryDoor = Opposite(step);
                    room.Doors |= room.EntryDoor;
                }

                room.X = x;
                room.Y = y;
                layout.Rooms.Add(room);
                previous = room;
            }
        }

        // 막다른 곁방 2~4개: 유물 상자(보물)와 회복 샘을 번갈아. 이동·휴식 방의 빈 이웃 칸에 붙인다 (시작 방 바로 옆과 보스 앞은 제외).
        private static void PlaceSideRooms(RoomLayoutData layout, Random rng)
        {
            var mainRooms = new List<RoomData>(layout.MainPathRooms());
            int want = rng.Next(MinSideRooms, MaxSideRooms + 1);
            var candidates = new List<(RoomData parent, DoorDir dir)>();
            foreach (var room in mainRooms)
            {
                if (!CanBeVertical(room.Kind) || room.PathIndex < 2 || room.PathIndex > mainRooms.Count - 3) continue;
                foreach (var dir in new[] { DoorDir.Up, DoorDir.Down, DoorDir.Left })
                {
                    if ((room.Doors & dir) != 0) continue;
                    int dy = Offset(dir).dy;
                    if (Math.Abs(room.Y + dy) > MaxRise + 1) continue;
                    candidates.Add((room, dir));
                }
            }

            // 진행도 전체에 고르게 퍼지도록 후보를 구간으로 나눠 하나씩 고른다.
            int placed = 0;
            for (int part = 0; part < want && candidates.Count > 0; part++)
            {
                int from = candidates.Count * part / want;
                int to = Math.Max(from + 1, candidates.Count * (part + 1) / want);
                for (int tries = 0; tries < 8; tries++)
                {
                    var (parent, dir) = candidates[rng.Next(from, Math.Min(to, candidates.Count))];
                    var (dx, dy) = Offset(dir);
                    int cx = parent.X + dx, cy = parent.Y + dy;
                    if (layout.At(cx, cy) != null || (parent.Doors & dir) != 0) continue;
                    if (TouchesOtherRoom(layout, cx, cy, parent)) continue; // 미니맵에서 지름길처럼 보이지 않게

                    var side = new RoomData
                    {
                        Index = layout.Rooms.Count, X = cx, Y = cy,
                        Kind = placed % 2 == 0 ? RoomKind.Treasure : RoomKind.Spring,
                        Chunk = ChunkKind.Rest, Difficulty = parent.Difficulty,
                        MainPath = false, ParentIndex = parent.Index,
                        EntryDoor = Opposite(dir), Doors = Opposite(dir)
                    };
                    parent.Doors |= dir;
                    layout.Rooms.Add(side);
                    placed++;
                    break;
                }
            }
        }

        private static bool TouchesOtherRoom(RoomLayoutData layout, int x, int y, RoomData parent)
        {
            foreach (var dir in new[] { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down })
            {
                var (dx, dy) = Offset(dir);
                var other = layout.At(x + dx, y + dy);
                if (other != null && other != parent) return true;
            }

            return false;
        }

        private static void UpdateBounds(RoomLayoutData layout)
        {
            layout.MinX = layout.MinY = int.MaxValue;
            layout.MaxX = layout.MaxY = int.MinValue;
            foreach (var room in layout.Rooms)
            {
                layout.MinX = Math.Min(layout.MinX, room.X);
                layout.MaxX = Math.Max(layout.MaxX, room.X);
                layout.MinY = Math.Min(layout.MinY, room.Y);
                layout.MaxY = Math.Max(layout.MaxY, room.Y);
            }
        }
    }
}
