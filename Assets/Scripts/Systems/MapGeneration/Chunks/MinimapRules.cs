using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 미니맵 표시 규칙 (던그리드 방식, 사람 결정 2026-11-10). UnityEngine 없이 돌아서 EditMode 테스트로 고정한다.
    // - 가 본 방(과 지금 방)만 네모 칸으로 그린다. 안 가 본 방은 칸을 그리지 않는다.
    // - 통로 실선: 두 방 모두 가 봤으면 칸과 칸을 잇는 실선(Full), 한쪽만 가 봤으면 가 본 방에서 안 가 본 쪽으로
    //   반 칸쯤 뻗은 실선(Stub)만 그려 "그쪽에 방이 더 있다"는 것만 보이게 한다. 둘 다 안 가 봤으면 그리지 않는다.
    // - 방 종류 아이콘·룬 점·자물쇠는 칸 위에 그리므로 ShowRoom을 따라 가 본 방에만 나온다.
    public enum MapLink
    {
        None,
        Stub,
        Full
    }

    // 방 칸을 어떻게 그릴지. Faint: 탐험가의 나침반 유물로 보이는 안 가 본 방 (흐린 칸 + 흐린 종류 아이콘만).
    public enum RoomLook
    {
        Hidden,
        Faint,
        Full
    }

    public struct MapLinkLine
    {
        public RoomData From;  // 늘 그려지는 방 (Stub이면 가 본 쪽)
        public DoorDir Dir;    // From에서 나가는 문 방향
        public MapLink Kind;
    }

    public static class MinimapRules
    {
        public static bool ShowRoom(bool visited, bool current) => visited || current;

        // compass: 탐험가의 나침반 유물을 가졌는지. 가 본 방(과 지금 방)은 늘 Full, 나침반이 있으면 나머지는 Faint.
        // 통로 실선은 Hidden이 아닌 방을 "보이는 방"으로 쳐서 Links에 넘긴다 (나침반이면 지도 전체가 실선으로 이어진다).
        public static RoomLook Look(bool visited, bool current, bool compass)
        {
            if (ShowRoom(visited, current)) return RoomLook.Full;
            return compass ? RoomLook.Faint : RoomLook.Hidden;
        }

        public static MapLink Link(bool fromShown, bool toShown)
        {
            if (fromShown && toShown) return MapLink.Full;
            return fromShown || toShown ? MapLink.Stub : MapLink.None;
        }

        // 그릴 통로 실선 목록. 문 하나(두 방 사이)는 한 번만 나온다. shown(index) = 그 방을 그리는지.
        public static List<MapLinkLine> Links(RoomLayoutData layout, Func<int, bool> shown)
        {
            var lines = new List<MapLinkLine>();
            foreach (var room in layout.Rooms)
            {
                bool roomShown = shown(room.Index);
                foreach (var dir in new[] { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down })
                {
                    if ((room.Doors & dir) == 0) continue;
                    var other = layout.Neighbor(room, dir);
                    if (other == null)
                    {
                        // 문 너머에 방이 없으면(있어선 안 되지만) 그 방이 보일 때만 짧게
                        if (roomShown) lines.Add(new MapLinkLine { From = room, Dir = dir, Kind = MapLink.Stub });
                        continue;
                    }

                    bool otherShown = shown(other.Index);
                    var kind = Link(roomShown, otherShown);
                    if (kind == MapLink.None) continue;
                    // Full은 오른쪽·위 문에서만 (두 번 그리지 않게), Stub은 보이는 방 쪽에서만
                    if (kind == MapLink.Full && (dir == DoorDir.Left || dir == DoorDir.Down)) continue;
                    if (kind == MapLink.Stub && !roomShown) continue;
                    lines.Add(new MapLinkLine { From = room, Dir = dir, Kind = kind });
                }
            }

            return lines;
        }
    }
}
