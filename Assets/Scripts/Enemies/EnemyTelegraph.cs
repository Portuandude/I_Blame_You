using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 적 공격 예고 공통 (웅크리기 시작하는 순간 한 번 부른다): 화면 안이면 작은 "슉" 소리(EnemyWindup),
    // 정예면 몸 둘레에 보라 고리가 퍼진다. 공격 시간·피해·사거리는 바꾸지 않는다 (그림·소리만).
    public static class EnemyTelegraph
    {
        private const float SfxCooldown = 0.12f;     // 여러 마리가 한꺼번에 예고해도 소리는 한 번
        private const float SfxVolume = 0.45f;
        private const float EliteRingBaseDiameter = 1.2f; // ManaRing size 1일 때 보이는 지름
        private static readonly Color EliteRingColor = new Color(0.72f, 0.42f, 1f, 0.9f); // 정예 "!!"와 같은 보라 계열

        private static float lastSfxTime = -10f;

        public static void Windup(EnemyController enemy, Bounds body)
        {
            if (enemy == null) return;

            if (OnScreen(body.center) && Time.unscaledTime - lastSfxTime >= SfxCooldown)
            {
                lastSfxTime = Time.unscaledTime;
                GameAudio.PlaySfx("EnemyWindup", SfxVolume);
            }

            if (RunGold.RankOf(enemy) == EnemyRank.Elite)
            {
                var t = enemy.transform;
                var offset = new Vector3(0f, body.center.y - t.position.y, 0f);
                float size = Mathf.Max(1f, Mathf.Max(body.size.x, body.size.y) * 1.3f / EliteRingBaseDiameter);
                VfxPlayer.ManaRing(t, offset, EliteRingColor, size);
            }
        }

        private static bool OnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var vp = cam.WorldToViewportPoint(world);
            return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        }
    }
}
