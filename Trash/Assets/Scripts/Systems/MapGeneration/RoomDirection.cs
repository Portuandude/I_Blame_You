using UnityEngine;

namespace IBlameYou.Systems
{
    public enum RoomDirection
    {
        North,
        East,
        South,
        West
    }

    public static class RoomDirectionExtensions
    {
        public static Vector2Int ToOffset(this RoomDirection direction)
        {
            switch (direction)
            {
                case RoomDirection.North: return Vector2Int.up;
                case RoomDirection.East: return Vector2Int.right;
                case RoomDirection.South: return Vector2Int.down;
                default: return Vector2Int.left;
            }
        }

        public static RoomDirection Opposite(this RoomDirection direction)
        {
            switch (direction)
            {
                case RoomDirection.North: return RoomDirection.South;
                case RoomDirection.East: return RoomDirection.West;
                case RoomDirection.South: return RoomDirection.North;
                default: return RoomDirection.East;
            }
        }
    }
}
