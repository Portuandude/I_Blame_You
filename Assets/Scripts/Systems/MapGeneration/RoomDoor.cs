using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 이웃 방으로 가는 문. 플레이어가 트리거에 겹쳐 있으면 RoomManager에 이동을 요청한다.
    [RequireComponent(typeof(BoxCollider2D))]
    public class RoomDoor : MonoBehaviour
    {
        private RoomManager manager;

        public RoomDirection Direction { get; private set; }
        public Vector2Int TargetGrid { get; private set; }

        public void Initialize(RoomDirection direction, Vector2Int targetGrid, RoomManager roomManager)
        {
            Direction = direction;
            TargetGrid = targetGrid;
            manager = roomManager;
        }

        // 도착 직후에도 문 위에 서 있을 수 있으니 Enter가 아니라 Stay로 매 프레임 확인하고, 재사용 대기는 RoomManager가 맡는다.
        private void OnTriggerStay2D(Collider2D other)
        {
            if (manager == null) return;

            var player = other.GetComponentInParent<PlayerMovement>();
            if (player != null) manager.RequestTravel(this, player);
        }
    }
}
