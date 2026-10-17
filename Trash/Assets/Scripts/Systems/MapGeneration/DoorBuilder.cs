using UnityEngine;

namespace IBlameYou.Systems
{
    // 방 안에 문 오브젝트를 배치한다. 문은 전부 바닥 위에 서 있고(플랫포머라 천장/바닥 통로는 쓰지 않음),
    // 동/서 문은 좌우 벽 안쪽에, 북/남 문은 바닥 중간 오른쪽/왼쪽에 놓는다.
    public static class DoorBuilder
    {
        public static readonly Vector2 DoorSize = new Vector2(3f, 5f);

        private const float ArrivalInset = 6f;   // 도착할 때 문에서 방 안쪽으로 떨어뜨릴 거리 (바로 다시 문에 닿지 않게)
        private const float ArrivalHeight = 1.2f; // 바닥 위로 살짝 띄워서 내려놓는다

        public static RoomDoor Build(RoomInstance room, RoomDirection direction, Vector2Int targetGrid, RoomManager manager)
        {
            var go = new GameObject($"Door_{direction}");
            go.transform.SetParent(room.transform, false);
            go.transform.localPosition = GetDoorLocalPosition(room.Size, direction);

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = DoorSize;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(DoorSize.x, DoorSize.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateSquare();
            renderer.color = GetColor(direction);
            renderer.sortingOrder = -1; // 배경(-10)보다 앞, 플레이어/바닥보다 뒤

            var door = go.AddComponent<RoomDoor>();
            door.Initialize(direction, targetGrid, manager);
            room.AddDoor(door);
            return door;
        }

        public static Vector3 GetDoorLocalPosition(Vector2 roomSize, RoomDirection direction)
        {
            float y = PlatformSpawner.FloorTopY(roomSize) + DoorSize.y / 2f;
            float sideX = roomSize.x / 2f - PlatformSpawner.WallThickness - DoorSize.x / 2f - 1f;
            float midX = roomSize.x * 0.16f;

            switch (direction)
            {
                case RoomDirection.East: return new Vector3(sideX, y, 0f);
                case RoomDirection.West: return new Vector3(-sideX, y, 0f);
                case RoomDirection.North: return new Vector3(midX, y, 0f);
                default: return new Vector3(-midX, y, 0f);
            }
        }

        // 해당 방향의 문 앞(방 안쪽)에 플레이어가 나타날 위치.
        public static Vector3 GetArrivalLocalPosition(Vector2 roomSize, RoomDirection doorDirection)
        {
            var door = GetDoorLocalPosition(roomSize, doorDirection);
            float towardCenter = door.x > 0f ? -ArrivalInset : ArrivalInset;
            return new Vector3(door.x + towardCenter, PlatformSpawner.FloorTopY(roomSize) + ArrivalHeight, 0f);
        }

        private static Color GetColor(RoomDirection direction)
        {
            switch (direction)
            {
                case RoomDirection.North: return new Color(0.25f, 0.6f, 1f);
                case RoomDirection.South: return new Color(0.7f, 0.35f, 0.9f);
                default: return new Color(0.95f, 0.6f, 0.2f);
            }
        }
    }
}
