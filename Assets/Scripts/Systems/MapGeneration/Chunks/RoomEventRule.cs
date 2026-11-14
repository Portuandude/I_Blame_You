using System;

namespace IBlameYou.Systems
{
    // 방 이벤트가 실제 방 격자의 어느 방에 나오는지 (2026-11-14 자동 실행, 방 이벤트 비율 점검).
    // 4×3 격자에서 좌우 이동 방은 모두 전투·퍼즐·마을·보상 방이 되고, 남는 이동 방은 위아래 문이 있는 세로 방(32칸 폭 통로)뿐이다.
    // 그래서 "가로 이동 방만"이던 보물 고블린·저주받은 상자가 60시드에서 한 번도 나오지 않았다 → 세로 이동 방에도 놓는다.
    // - 보물 고블린: 모든 이동 방 (고블린은 낭떠러지·구멍 앞에서 돌아서므로 아래 문 구멍이 있어도 된다)
    // - 저주받은 상자: 가로 이동 방, 또는 바닥이 막힌(아래 문 없는) 세로 이동 방 중 궁수 탑이 아닌 방.
    //   아래 문 구멍으로 정예가 떨어지면 쓰러지지 않고 치워져 저주가 풀리지 않으므로 뺀다. 궁수 탑 + 정예는 너무 버겁다.
    //   그런 방은 판마다 한두 개뿐이라 확률을 CursedChestRule.ShaftChancePercent로 올린다.
    // 난수를 쓰지 않아 지형·적은 그대로다. ChunkStreamer(놓기·미니맵 점)와 EditMode 테스트가 같은 판정을 쓴다.
    public static class RoomEventRule
    {
        // ChunkStreamer.RoomDepth와 같은 값
        public static float Depth(RoomData room) => Math.Max(0f, Math.Min(1f, room.Difficulty));

        public static bool HasClosedFloor(RoomData room) => (room.Doors & DoorDir.Down) == 0;

        public static bool IsTreasureGoblinRoom(int seed, RoomData room) =>
            room != null && IsEventRoom(room) && TreasureGoblinRule.IsTreasureGoblinRoom(seed, room.Index, Depth(room), true);

        public static bool IsCursedChestRoom(int seed, RoomData room)
        {
            if (room == null || !IsEventRoom(room) || IsTreasureGoblinRoom(seed, room)) return false;
            float depth = Depth(room);
            if (!room.IsVertical) return CursedChestRule.IsCursedChestRoom(seed, room.Index, depth, true);
            if (!HasClosedFloor(room) || RoomShaper.IsArcherTower(room, RoomShaper.PickShaftStyle(seed, room))) return false;
            return CursedChestRule.IsCursedChestShaft(seed, room.Index, depth);
        }

        // 2026-11-14 사람 결정으로 방 격자에 이동 방이 없어져, 방 이벤트는 일반 전투 방에 나온다 (옛 이동 방과 같은 규칙).
        // 좌우 전투 방: 저주 상자는 방 가운데(앞 구간) 바닥이라 전투 구역 밖. 세로 전투 방: 바닥이 전투 구역이라 먼저 전투가 끝난 뒤 상자를 연다.
        public static bool IsEventRoom(RoomData room) => room.Kind == RoomKind.Travel || room.Kind == RoomKind.Combat;
    }
}
