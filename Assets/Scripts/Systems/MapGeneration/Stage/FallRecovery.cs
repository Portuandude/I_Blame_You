using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 낭떠러지에 떨어지면 체력을 조금 잃고 마지막으로 안전하게 서 있던 땅으로 돌아온다.
    // 떨어질 때 "휘이이잉", 돌아오면 빛 고리·"뿅"·무적 동안 깜빡임 (FallRecoveryFx, 그림·소리만).
    // 안전한 땅: 바닥에 서 있고, 무너지는 발판이 아니고, 가까이에 함정이 없는 곳.
    [RequireComponent(typeof(PlayerMovement))]
    public class FallRecovery : MonoBehaviour
    {
        public const float KillY = -3f;    // 지금 바닥(FloorY)에서 이만큼 아래로 떨어지면 복귀
        // 지금 스테이지(방)의 바닥 월드 높이. 외길 모드는 0, 방 격자 모드는 ChunkStreamer가 방을 옮길 때마다 바꾼다.
        public static float FloorY;
        public const float DefaultDamageRatio = 0.15f;
        private const float RecordInterval = 0.2f;
        private const float HazardRadius = 1.6f;
        private const float InvulnerableTime = 1f;
        private const float FallSoundY = -1.6f; // 바닥에서 이만큼 아래로 떨어지면 추락 소리 (KillY보다 얕게)

        private PlayerMovement movement;
        private Rigidbody2D rb;
        private HealthSystem health;
        private Vector3 safePoint;
        private float nextRecord;
        private int groundMask;
        private bool fallSoundPlayed; // 이번 추락에서 "휘이이잉"을 냈는지 (복귀하거나 땅에 서면 다시)

        // 떨어졌을 때 잃는 체력 비율 (최대 체력 기준). 유물 깃털 부적이 낮춘다.
        public float DamageRatio { get; set; } = DefaultDamageRatio;

        // 피해 없이 돌아오는 남은 횟수 (유물 되감기 모래시계).
        public int FreeFalls { get; set; }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<HealthSystem>();
            safePoint = transform.position;
            groundMask = 1 << TrapCommon.GroundLayer;
        }

        public void SetSafePoint(Vector3 point) => safePoint = point;

        private void Update()
        {
            if (health.IsDead) return;

            if (transform.position.y < FloorY + KillY)
            {
                Recover();
                return;
            }

            // 연출: 바닥 아래로 떨어지기 시작하면 한 번 "휘이이잉" (아래 방 구멍은 바닥 -0.5에서 이미 넘어가므로 그보다 깊게 본다)
            if (movement.IsGrounded) fallSoundPlayed = false;
            else if (!fallSoundPlayed && transform.position.y < FloorY + FallSoundY && rb.linearVelocity.y < -3f)
            {
                fallSoundPlayed = true;
                FallRecoveryFx.Falling();
            }

            if (Time.time >= nextRecord && movement.IsGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.1f && IsSafeHere())
            {
                safePoint = transform.position + Vector3.up * 0.1f;
                nextRecord = Time.time + RecordInterval;
            }
        }

        private bool IsSafeHere()
        {
            var below = Physics2D.Raycast(transform.position, Vector2.down, 1.5f, groundMask);
            if (below.collider == null || below.collider.GetComponentInParent<UnsafeGround>() != null) return false;
            // 전투 구역 문·천장/바닥 봉인은 돌파하면 치워지므로 복귀 자리로 쓰지 않는다 (구멍 위 허공으로 돌아오지 않게).
            // UnsafeGround를 붙이면 순간이동이 발판으로 보고 봉인을 뚫으므로 여기서 따로 본다
            if (below.collider.GetComponentInParent<ArenaZone>() != null) return false;

            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, HazardRadius))
            {
                if (hit.GetComponent<SpikeTrap>() != null || hit.GetComponent<FlameJetTrap>() != null) return false;
            }

            return true;
        }

        private void Recover()
        {
            bool shielded = health.IsInvulnerable; // 무적(대쉬·경직·직전 복귀·치트) 중이면 피해도 모래시계도 쓰지 않는다 (버그 점검 2026-11-24)
            bool free = !shielded && FreeFalls > 0;
            if (free) FreeFalls--;
            else if (!shielded) health.TakeDamage(health.Max * DamageRatio);
            if (health.IsDead) return;

            rb.linearVelocity = Vector2.zero;
            rb.position = safePoint;
            transform.position = safePoint;
            var movement = GetComponent<PlayerMovement>();
            if (movement != null) movement.ResetAirborne();
            // 시간 무적으로 준다: 공유 Invulnerable을 켜고 끄면 경직·대쉬·순간이동이 끝나며 이 1초를 먼저 꺼 버린다 (버그 점검 2026-11-22)
            health.GrantGrace(InvulnerableTime);
            StageEvents.PostNotice(shielded ? "낭떠러지! 무적이라 피해 없음"
                : free ? $"모래시계가 시간을 되감았다! 피해 없음 (남은 {FreeFalls}번)"
                : $"낭떠러지! 체력 -{DamageRatio * 100f:0}%");

            var follow = Camera.main != null ? Camera.main.GetComponent<Core.CameraFollow>() : null;
            if (follow != null) follow.SnapToTarget();

            // 연출 (그림·소리만): 화면 어둡게→밝게, 복귀 자리 빛 고리·반짝·먼지 + "뿅", 무적 동안 깜빡임 (FallRecoveryFx)
            fallSoundPlayed = false;
            FallRecoveryFx.Returned(transform, safePoint - Vector3.up * 0.1f, free, InvulnerableTime);
        }
    }
}
