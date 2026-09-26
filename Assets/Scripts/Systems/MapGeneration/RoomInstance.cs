using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 씬에 실제로 만들어진 방 하나. 위치/크기/이웃 방으로 가는 문을 들고 있고,
    // 등록된 적이 전부 죽으면 클리어되어 문이 열린다 (그 전까지 문은 반투명 + 이동 불가).
    public class RoomInstance : MonoBehaviour
    {
        private readonly Dictionary<RoomDirection, RoomDoor> doors = new Dictionary<RoomDirection, RoomDoor>();
        private readonly HashSet<HealthSystem> aliveEnemies = new HashSet<HealthSystem>();

        public RoomNode Node { get; private set; }
        public Vector2 Size { get; private set; }
        public Vector3 Center => transform.position;
        public Rect WorldBounds => new Rect((Vector2)transform.position - Size / 2f, Size);
        public IEnumerable<RoomDoor> Doors => doors.Values;
        public bool IsCleared => aliveEnemies.Count == 0;

        public void Initialize(RoomNode node, Vector2 size)
        {
            Node = node;
            Size = size;
        }

        public void AddDoor(RoomDoor door)
        {
            doors[door.Direction] = door;
            door.SetLocked(!IsCleared);
        }

        // 이 방의 클리어 조건에 포함될 적을 등록한다. 등록된 적이 모두 죽어야 클리어.
        public void RegisterEnemy(HealthSystem enemy)
        {
            if (enemy == null || enemy.IsDead) return;

            aliveEnemies.Add(enemy);
            enemy.HealthChanged += (current, max) => OnEnemyHealthChanged(enemy, current);
            RefreshDoors();
        }

        private void OnEnemyHealthChanged(HealthSystem enemy, float current)
        {
            if (current > 0f) return;

            aliveEnemies.Remove(enemy);
            RefreshDoors();
        }

        private void RefreshDoors()
        {
            foreach (var door in doors.Values) door.SetLocked(!IsCleared);
        }
    }
}
