using IBlameYou.Enemies;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 개발자 모드 전용 치트 (기획서 2.9 개발 편의 기능). F1 무적, F2 마나 무한, F3 현재 방의 적 처치, F4 체력 회복.
    public class DevCheats : MonoBehaviour
    {
        private HealthSystem health;
        private ManaSystem mana;
        private RoomManager rooms;

        public bool GodMode { get; private set; }

        public void Initialize(GameObject player, RoomManager roomManager)
        {
            health = player.GetComponent<HealthSystem>();
            mana = player.GetComponent<ManaSystem>();
            rooms = roomManager;
        }

        private void Update()
        {
            if (!GameSession.DevMode || health == null) return;

            if (Input.GetKeyDown(KeyCode.F1))
            {
                GodMode = !GodMode;
                if (!GodMode) health.Invulnerable = false;
            }

            if (Input.GetKeyDown(KeyCode.F2)) mana.Infinite = !mana.Infinite;
            if (Input.GetKeyDown(KeyCode.F3)) KillRoomEnemies();
            if (Input.GetKeyDown(KeyCode.F4)) health.Heal(health.Max);
        }

        private void LateUpdate()
        {
            // 대쉬/경직이 끝나며 무적을 끄더라도 다시 켠다.
            if (GodMode && health != null) health.Invulnerable = true;
        }

        private void KillRoomEnemies()
        {
            if (rooms == null || rooms.CurrentRoom == null) return;
            foreach (var enemy in rooms.CurrentRoom.GetComponentsInChildren<EnemyController>())
            {
                var enemyHealth = enemy.GetComponent<HealthSystem>();
                if (enemyHealth != null && !enemyHealth.IsDead) enemyHealth.TakeDamage(enemyHealth.Max * 10f);
            }
        }
    }
}
