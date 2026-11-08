using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 낭떠러지에 떨어지면 체력을 조금 잃고 마지막으로 안전하게 서 있던 땅으로 돌아온다.
    // 안전한 땅: 바닥에 서 있고, 무너지는 발판이 아니고, 가까이에 함정이 없는 곳.
    [RequireComponent(typeof(PlayerMovement))]
    public class FallRecovery : MonoBehaviour
    {
        public const float KillY = -3f;
        private const float DamageRatio = 0.15f;
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

            if (transform.position.y < KillY)
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
            health.TakeDamage(health.Max * DamageRatio); // 무적(대쉬/치트) 중이면 피해 없이 돌아오기만 한다
            if (health.IsDead) return;

            rb.linearVelocity = Vector2.zero;
            rb.position = safePoint;
            transform.position = safePoint;
            health.Invulnerable = true;
            grantedInvulnerability = true;
            invulnerableUntil = Time.time + InvulnerableTime;
            StageEvents.PostNotice("낭떠러지! 체력 -15%");

            var follow = Camera.main != null ? Camera.main.GetComponent<Core.CameraFollow>() : null;
            if (follow != null) follow.SnapToTarget();
        }
    }
}
