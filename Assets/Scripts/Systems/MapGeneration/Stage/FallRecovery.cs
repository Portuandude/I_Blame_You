using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 낭떠러지에 떨어지면 체력을 조금 잃고 마지막으로 안전하게 서 있던 땅으로 돌아온다.
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

        private PlayerMovement movement;
        private Rigidbody2D rb;
        private HealthSystem health;
        private Vector3 safePoint;
        private float nextRecord;
        private float invulnerableUntil;
        private bool grantedInvulnerability;
        private int groundMask;

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
            if (grantedInvulnerability && Time.time >= invulnerableUntil)
            {
                grantedInvulnerability = false;
                if (!movement.IsDashing) health.Invulnerable = false;
            }

            if (health.IsDead) return;

            if (transform.position.y < FloorY + KillY)
            {
                Recover();
                return;
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

            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, HazardRadius))
            {
                if (hit.GetComponent<SpikeTrap>() != null || hit.GetComponent<FlameJetTrap>() != null) return false;
            }

            return true;
        }

        private void Recover()
        {
            bool free = FreeFalls > 0;
            if (free) FreeFalls--;
            else health.TakeDamage(health.Max * DamageRatio); // 무적(대쉬/치트) 중이면 피해 없이 돌아오기만 한다
            if (health.IsDead) return;

            rb.linearVelocity = Vector2.zero;
            rb.position = safePoint;
            transform.position = safePoint;
            var movement = GetComponent<PlayerMovement>();
            if (movement != null) movement.ResetAirborne();
            health.Invulnerable = true;
            grantedInvulnerability = true;
            invulnerableUntil = Time.time + InvulnerableTime;
            StageEvents.PostNotice(free
                ? $"모래시계가 시간을 되감았다! 피해 없음 (남은 {FreeFalls}번)"
                : $"낭떠러지! 체력 -{DamageRatio * 100f:0}%");

            var follow = Camera.main != null ? Camera.main.GetComponent<Core.CameraFollow>() : null;
            if (follow != null) follow.SnapToTarget();
        }
    }
}
