using UnityEngine;

namespace IBlameYou.Systems
{
    // 플레이어/적 몸체끼리는 서로 밀어내지 않도록 레이어 충돌을 끈다.
    // (접촉 데미지와 공격 판정은 겹침 조회로 처리하므로 충돌이 꺼져도 동작한다.)
    // "Player"/"Enemy" 레이어가 프로젝트에 없으면 아무것도 하지 않는다.
    public static class CharacterLayers
    {
        public const string PlayerLayerName = "Player";
        public const string EnemyLayerName = "Enemy";

        public static void Assign(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) go.layer = layer;
        }

        public static void EnsureCollisionRules()
        {
            int player = LayerMask.NameToLayer(PlayerLayerName);
            int enemy = LayerMask.NameToLayer(EnemyLayerName);
            if (player < 0 || enemy < 0) return;

            Physics2D.IgnoreLayerCollision(player, enemy, true);
            Physics2D.IgnoreLayerCollision(enemy, enemy, true);
        }
    }
}
