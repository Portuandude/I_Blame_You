using System.Collections.Generic;
using IBlameYou.Core;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 불 기사의 돌진: 같은 높이에 플레이어가 있으면 주황으로 깜빡이며 웅크렸다가(예고) 그쪽으로 땅을 빠르게 미끄러진다.
    // 몸에 닿으면 불 속성 피해 + 화상 시도. 맞아서 경직되면 돌진이 끊긴다. 광폭화하면 재사용 대기가 짧아진다.
    [RequireComponent(typeof(SlimeController))]
    public class KnightCharge : MonoBehaviour
    {
        private const float FirstDelay = 2.5f;
        private const float Cooldown = 4.5f;
        private const float Range = 12f;
        private const float MaxHeightGap = 2f;
        private const float WindupTime = 0.6f;
        private const float DashTime = 0.6f;
        private const float RecoverTime = 0.5f;
        private const float DashSpeed = 10f;
        private const float DamageMultiplier = 1.2f;
        private const float WallCheck = 0.4f;
        private const float TrailGap = 0.08f;      // 돌진 중 발밑 먼지 간격 (그림만)
        private const float BumpShake = 0.15f;     // 벽에 부딪혀 멈출 때 카메라 흔들림 세기
        private static readonly Color WarnColor = new Color(1f, 0.55f, 0.15f);

        private enum Phase { None, Windup, Dash, Recover }

        private readonly HashSet<HealthSystem> struck = new HashSet<HealthSystem>();
        private SlimeController slime;
        private SlimeKnight knight;
        private Rigidbody2D rb;
        private HealthSystem health;
        private HitStun stun;
        private CombatStats stats;
        private Collider2D body;
        private LayerMask ground;
        private Transform player;
        private Phase phase;
        private float phaseEnd;
        private float nextTime;
        private float direction = 1f;
        private AimLineFx dashLine; // 예고: 돌진할 쪽 바닥 높이 주황 점선 (돌진 거리만큼, 벽에서 끊김)
        private float nextTrail;

        private void Awake()
        {
            slime = GetComponent<SlimeController>();
            knight = GetComponent<SlimeKnight>();
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<HealthSystem>();
            stun = GetComponent<HitStun>();
            stats = GetComponent<CombatStats>();
            body = GetComponent<Collider2D>();
            ground = EnemySpawner.GroundMask();
        }

        private void Start()
        {
            nextTime = Time.time + FirstDelay;
        }

        private void Update()
        {
            if (health.IsDead)
            {
                if (phase != Phase.None) End();
                return;
            }

            if (phase != Phase.None && stun != null && stun.IsStunned)
            {
                End();
                return;
            }

            switch (phase)
            {
                case Phase.None:
                    if (Time.time >= nextTime && !slime.Attacking && (stun == null || !stun.IsStunned) && Grounded() && FindTarget())
                        Begin();
                    break;
                case Phase.Windup:
                    if (Time.time >= phaseEnd)
                    {
                        RemoveDashLine();
                        phase = Phase.Dash;
                        phaseEnd = Time.time + DashTime;
                        nextTrail = Time.time;
                        struck.Clear();
                        EffectLibrary.PlayCast(Element.Fire, transform.position + Vector3.up * 0.4f, direction < 0f, 0.7f);
                        GameAudio.PlaySfx("Dash", 0.6f);
                        EnemySquash.For(gameObject).Release(); // 웅크렸다가 쭉 늘어나며 출발
                    }
                    else
                    {
                        UpdateDashLine(1f - (phaseEnd - Time.time) / WindupTime);
                    }
                    break;
                case Phase.Dash:
                    if (Time.time >= nextTrail)
                    {
                        nextTrail = Time.time + TrailGap;
                        var b = body.bounds;
                        VfxPlayer.Dust(new Vector3(b.center.x - direction * b.extents.x * 0.5f, b.min.y, 0f), 0.3f);
                    }
                    bool wall = WallAhead();
                    if (Time.time >= phaseEnd || wall)
                    {
                        if (wall) Bump();
                        phase = Phase.Recover;
                        phaseEnd = Time.time + RecoverTime;
                    }
                    break;
                case Phase.Recover:
                    if (Time.time >= phaseEnd) End();
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (phase == Phase.None || health.IsDead) return;

            if (phase == Phase.Dash)
            {
                rb.linearVelocity = new Vector2(direction * DashSpeed * stats.TotalMoveMultiplier, rb.linearVelocity.y);
                StrikeOverlaps();
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        private void Begin()
        {
            phase = Phase.Windup;
            phaseEnd = Time.time + WindupTime;
            slime.Suspended = true;
            transform.localScale = new Vector3(direction, 1f, 1f);
            if (knight == null) knight = GetComponent<SlimeKnight>();
            if (knight != null) knight.Flash(WarnColor, WindupTime);

            // 예고 (그림·소리만): 웅크림 + "슉" + 돌진할 쪽 점선.
            EnemySquash.For(gameObject).Windup(WindupTime);
            if (slime != null) EnemyTelegraph.Windup(slime, body.bounds);
            RemoveDashLine();
            dashLine = AimLineFx.Create(gameObject, WarnColor);
            UpdateDashLine(0f);
        }

        private void UpdateDashLine(float strength)
        {
            if (dashLine == null) return;
            var bounds = body.bounds;
            var from = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.min.y + 0.3f); // WallAhead와 같은 높이
            float length = DashSpeed * stats.TotalMoveMultiplier * DashTime;
            dashLine.Aim(from, from + new Vector2(direction * length, 0f), length, ground, strength);
        }

        private void RemoveDashLine()
        {
            if (dashLine != null) dashLine.Remove();
            dashLine = null;
        }

        // 벽에 부딪혀 멈춤: 먼지 + 쿵 + 작은 흔들림 (화면 안일 때만 흔든다).
        private void Bump()
        {
            var bounds = body.bounds;
            VfxPlayer.Dust(new Vector3(bounds.center.x + direction * bounds.extents.x, bounds.min.y, 0f), 0.8f);
            GameAudio.PlaySfx("Land", 0.7f);
            var cam = Camera.main;
            if (cam == null) return;
            var vp = cam.WorldToViewportPoint(bounds.center);
            if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return;
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) follow.Shake(0.15f, BumpShake);
        }

        private void End()
        {
            RemoveDashLine();
            if (phase == Phase.Windup) EnemySquash.For(gameObject).Cancel(); // 예고 중에 끊기면 웅크림을 바로 푼다
            phase = Phase.None;
            slime.Suspended = false;
            nextTime = Time.time + Cooldown / Mathf.Max(0.1f, stats.AttackSpeedMultiplier);
        }

        private bool FindTarget()
        {
            if (player == null)
            {
                var movement = FindFirstObjectByType<PlayerMovement>();
                if (movement == null) return false;
                player = movement.transform;
            }

            var playerHealth = player.GetComponent<HealthSystem>();
            if (playerHealth != null && playerHealth.IsDead) return false;

            float dx = player.position.x - transform.position.x;
            float dy = player.position.y - transform.position.y;
            if (Mathf.Abs(dx) > Range || Mathf.Abs(dx) < 0.5f || Mathf.Abs(dy) > MaxHeightGap) return false;
            direction = Mathf.Sign(dx);
            return true;
        }

        private bool Grounded() => Mathf.Abs(rb.linearVelocity.y) < 0.1f;

        private bool WallAhead()
        {
            var bounds = body.bounds;
            var origin = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.min.y + 0.3f);
            return Physics2D.Raycast(origin, new Vector2(direction, 0f), WallCheck, ground);
        }

        private void StrikeOverlaps()
        {
            var bounds = body.bounds;
            foreach (var hit in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f))
            {
                var target = hit.GetComponentInParent<HealthSystem>();
                if (target == null || target == health || target.GetComponent<EnemyController>() != null) continue;
                if (!struck.Add(target)) continue;

                if (DamageCalculator.Strike(stats, target, DamageKind.Magic, Element.Fire, DamageMultiplier) > 0f)
                    StatusEffects.TryApply(target, StatusDebuff.Burn, stats);
            }
        }

        private void OnDestroy()
        {
            RemoveDashLine();
            if (slime != null) slime.Suspended = false;
        }
    }
}
