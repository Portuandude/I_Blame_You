using UnityEngine;

namespace IBlameYou.Systems
{
    public class RoomNode
    {
        public Vector2Int GridPosition { get; }
        public RoomType Type { get; set; }

        public RoomNode(Vector2Int gridPosition, RoomType type)
        {
            GridPosition = gridPosition;
            Type = type;
        }
    }
}
