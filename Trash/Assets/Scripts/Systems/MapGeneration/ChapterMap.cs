using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    public class ChapterMap
    {
        public Dictionary<Vector2Int, RoomNode> Rooms { get; }
        public Vector2Int StartPosition { get; }

        public ChapterMap(Dictionary<Vector2Int, RoomNode> rooms, Vector2Int startPosition)
        {
            Rooms = rooms;
            StartPosition = startPosition;
        }
    }
}
