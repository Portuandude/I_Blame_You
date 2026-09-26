using UnityEngine;

namespace IBlameYou.Systems
{
    // 생성된 챕터 맵 전체를 눈으로 확인할 수 있도록 방마다 타입별 색상의 배경을 배치한다.
    public static class RoomBuilder
    {
        // 방 루트 오브젝트(RoomInstance)를 만들고 그 아래에 배경을 깐다. 바닥/벽/문/적은 이 루트 아래에 붙는다.
        public static RoomInstance CreateRoom(Transform parent, RoomNode node, Vector2 roomSize, Vector3 worldPosition)
        {
            var go = new GameObject($"Room_{node.GridPosition.x}_{node.GridPosition.y}_{node.Type}");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;

            var room = go.AddComponent<RoomInstance>();
            room.Initialize(node, roomSize);
            BuildRoomBackground(go.transform, node, roomSize, worldPosition);
            return room;
        }

        public static Transform BuildRoomBackground(Transform parent, RoomNode room, Vector2 roomSize, Vector3 worldPosition)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.transform.localScale = new Vector3(roomSize.x, roomSize.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateSquare();
            renderer.color = GetRoomColor(room.Type);
            renderer.sortingOrder = -10;

            return go.transform;
        }

        private static Color GetRoomColor(RoomType type)
        {
            switch (type)
            {
                case RoomType.Start: return new Color(0.85f, 0.85f, 0.85f);
                case RoomType.Elite: return new Color(0.9f, 0.55f, 0.2f);
                case RoomType.MiniBoss: return new Color(0.6f, 0.3f, 0.8f);
                case RoomType.ChapterBoss: return new Color(0.8f, 0.2f, 0.2f);
                case RoomType.Puzzle: return new Color(0.2f, 0.7f, 0.8f);
                default: return new Color(0.3f, 0.3f, 0.33f);
            }
        }
    }
}
