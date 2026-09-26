using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 씬에 실제로 만들어진 방 하나. 위치/크기/이웃 방으로 가는 문을 들고 있다.
    public class RoomInstance : MonoBehaviour
    {
        private readonly Dictionary<RoomDirection, RoomDoor> doors = new Dictionary<RoomDirection, RoomDoor>();

        public RoomNode Node { get; private set; }
        public Vector2 Size { get; private set; }
        public Vector3 Center => transform.position;
        public Rect WorldBounds => new Rect((Vector2)transform.position - Size / 2f, Size);
        public IEnumerable<RoomDoor> Doors => doors.Values;

        public void Initialize(RoomNode node, Vector2 size)
        {
            Node = node;
            Size = size;
        }

        public void AddDoor(RoomDoor door)
        {
            doors[door.Direction] = door;
        }
    }
}
