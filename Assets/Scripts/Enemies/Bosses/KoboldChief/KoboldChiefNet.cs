using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 코볼트 족장의 그물 던지기 (KoboldChiefRules): 돌격 중이 아니고 플레이어가 그물 거리 안이면 NetInterval초마다 그물(NetShot).
    // 던지기 전 0.4초 예고 (웅크림 + 반짝). 묶이면 A는 못 움직이니 B가 와르그·족장을 막아야 한다.
    public class KoboldChiefNet : MonoBehaviour
    {
        private const float WindupTime = 0.4f;
        private const float HandHeight = 1.3f;

        private KoboldController mover;
        private HealthSystem health;
        private CombatStats stats;
        private Transform target;
        private float nextNetTime;
        private float windupLeft = -1f;
        private float nextSearchTime;

        private void Awake()
        {
            mover = GetComponent<KoboldController>();
            health = GetComponent<HealthSystem>();
            stats = GetComponent<CombatStats>();
        }

        private void Start() => nextNetTime = Time.time + 2.5f;

        private void Update()
        {
            if (health == null || health.IsDead || Time.timeScale <= 0f) return;
            if (mover != null && mover.Stunned) // 맞아서 경직이면 예고 중이던 그물도 취소 (리뷰 MEDIUM)
            {
                if (windupLeft >= 0f) { windupLeft = -1f; EnemySquash.For(gameObject).Cancel(); }
                return;
            }
            if (target == null)
            {
                if (Time.time < nextSearchTime) return; // 플레이어가 없는 동안 매 프레임 씬 전체를 찾지 않게 (버그 점검 2026-11-24)
                nextSearchTime = Time.time + 0.5f;
                var player = FindFirstObjectByType<IBlameYou.Player.PlayerMovement>();
                if (player == null) return;
                target = player.transform;
            }

            if (windupLeft >= 0f)
            {
                windupLeft -= Time.deltaTime;
                if (mover != null && mover.Busy) { windupLeft = -1f; EnemySquash.For(gameObject).Cancel(); return; } // 그사이 돌격을 시작했으면 그물은 다음에
                if (windupLeft < 0f) Throw();
                return;
            }

            if (Time.time < nextNetTime || (mover != null && mover.Busy)) return;
            float distance = Vector2.Distance(transform.position, target.position);
            if (!KoboldChiefRules.InNetRange(distance) || IBlameYou.Systems.NetSnare.IsSnared(target.gameObject)) return;
            var targetHealth = target.GetComponent<HealthSystem>();
            if (targetHealth != null && targetHealth.IsDead) return; // 쓰러진 플레이어에게는 안 던진다
            if (Physics2D.Linecast(Hand(), (Vector2)target.position + Vector2.up * 0.5f, EnemySpawner.GroundMask())) return; // 벽·바닥 너머로는 예고도 하지 않는다 (그물은 땅에 막힘, 버그 점검 2026-11-24)
            windupLeft = WindupTime;
            EnemySquash.For(gameObject).Windup(WindupTime);
            VfxPlayer.AimGlint(Hand());
        }

        private Vector2 Hand() => (Vector2)transform.position + new Vector2(mover != null ? mover.Facing * 0.5f : 0f, HandHeight);

        private void Throw()
        {
            windupLeft = -1f;
            EnemySquash.For(gameObject).Release();
            float fraction = health.Max > 0f ? health.Current / health.Max : 1f;
            nextNetTime = Time.time + KoboldChiefRules.NetIntervalFor(fraction);
            if (target == null) return;
            var targetHealth = target.GetComponent<HealthSystem>();
            if (targetHealth != null && targetHealth.IsDead) return; // 예고 중에 쓰러졌으면 안 던진다 (버그 점검 2026-11-24)
            Vector2 from = Hand();
            var net = NetShot.Launch(stats, from, (Vector2)target.position + Vector2.up * 0.5f - from, KoboldChiefRules.NetMaxRange * 1.5f);
            if (net != null && transform.parent != null) net.transform.SetParent(transform.parent, true); // 방이 치워지면 날던 그물도 같이
        }
    }
}
