using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 A의 순간이동 (기획서 2.5). W/A/S/D로 상/하/좌/우 중 하나를 잡고 Enter. 미니게임 없이 바로 발동하고 마나를 쓴다.
    // 아무 방향도 안 누르고 있으면 바라보는 방향으로. 벽 점프가 없으므로 세로 이동 수단을 겸한다.
    // 좌/우/위는 땅·발판 앞에서 멈추고, 아래는 발판(한쪽 발판·움직이는/스위치 발판·무너지는 발판)을 뚫되 낙사하지 않는 지점까지만 간다 (규칙: TeleportRules).
    [RequireComponent(typeof(PlayerControlState))]
    [RequireComponent(typeof(ManaSystem))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class TeleportSkill : MonoBehaviour
    {
        [SerializeField] private float manaCost = 20f;
        [SerializeField] private float distance = 4f;
        [SerializeField] private float invulnerableTime = 0.15f;

        private PlayerControlState control;
        private ManaSystem mana;
        private Rigidbody2D rb;
        private Collider2D body;
        private HealthSystem health;
        private HitStun hitStun;
        private PlayerMovement movement;
        private float invulnerableUntil;
        private bool grantedInvulnerability;
        private readonly List<RaycastHit2D> castResults = new List<RaycastHit2D>();
        private readonly List<Collider2D> overlapResults = new List<Collider2D>();
        private ContactFilter2D groundFilter;

        // 유물 종이비행기 등이 곱하는 순간이동 마나 배율 (PlayerRelics). 기본 1.
        public float CostMultiplier { get; set; } = 1f;
        public float ManaCost => manaCost * CostMultiplier;

        public float Distance
        {
            get => distance;
            set => distance = value;
        }

        private void Awake()
        {
            control = GetComponent<PlayerControlState>();
            mana = GetComponent<ManaSystem>();
            rb = GetComponent<Rigidbody2D>();
            body = GetComponent<Collider2D>();
            health = GetComponent<HealthSystem>();
            hitStun = GetComponent<HitStun>();
            movement = GetComponent<PlayerMovement>();

            groundFilter = new ContactFilter2D();
            int ground = LayerMask.NameToLayer("Ground");
            groundFilter.SetLayerMask(ground >= 0 ? (LayerMask)(1 << ground) : (LayerMask)1);
            groundFilter.useTriggers = false;
        }

        private void Update()
        {
            if (grantedInvulnerability && Time.time >= invulnerableUntil)
            {
                grantedInvulnerability = false;
                if (!movement.IsDashing && (hitStun == null || !hitStun.IsStunned)) health.Invulnerable = false;
            }

            if (!RoleInput.TeleportDown(control)) return;
            if (health.IsDead || movement.IsDashing || (hitStun != null && hitStun.IsStunned)) return;

            var input = RoleInput.TeleportDirection();
            TeleportRules.Cardinal(input.x, input.y, movement.Facing >= 0f ? 1 : -1, out int dx, out int dy);
            var direction = new Vector2(dx, dy);

            // 갈 거리를 먼저 정한다: 갈 곳이 없으면 마나를 쓰지 않고 실패만 알린다.
            float travel = PlanTravel(direction);
            // 마력 안개(스위치 발판 낭떠러지): 출발·도착·지나는 길이 닿으면 흩어진다.
            bool warded = travel >= TeleportRules.MinTravel &&
                (BlinkWard.Blocks(rb.position, movement.IsGroundedStable) || BlinkWard.Touches(rb.position, rb.position + direction * travel));
            if (travel < TeleportRules.MinTravel || warded)
            {
                SkillMessages.Post(ManaUser.PlayerA, warded ? "순간이동 불가: 마력 안개가 흩뜨림"
                    : dy < 0 ? "순간이동 불가: 아래에 안전하게 설 곳이 없음" : "순간이동 불가: 막혀 있음", true);
                EffectLibrary.PlayOneShot(rb.position, EffectLibrary.TeleportFrames, 30f, new Color(1f, 0.45f, 0.45f, 0.45f), 0.6f);
                return;
            }

            if (!mana.TryConsume(ManaCost, ManaUser.PlayerA))
            {
                SkillMessages.Post(ManaUser.PlayerA, $"마나 부족: 순간이동 (필요 {ManaCost:0})", true);
                return;
            }

            Teleport(direction, travel);
        }

        private float PlanTravel(Vector2 direction)
        {
            if (body == null) return distance;
            if (direction.y < 0f) return PlanDown();
            return TeleportRules.Blocked(distance, CastDistance(direction, includePlatforms: true));
        }

        // 몸을 direction으로 밀었을 때 처음 막는 면까지의 거리. 서 있는 바닥처럼 옆으로 스치는 면은 무시한다.
        private float CastDistance(Vector2 direction, bool includePlatforms)
        {
            float nearest = float.PositiveInfinity;
            body.Cast(direction, groundFilter, castResults, distance);
            foreach (var hit in castResults)
            {
                if (Vector2.Dot(hit.normal, direction) > -0.5f) continue;
                if (!includePlatforms && hit.collider.usedByEffector) continue; // 위에서만 밟히는 발판
                nearest = Mathf.Min(nearest, hit.distance);
            }

            return nearest;
        }

        private float PlanDown()
        {
            var bounds = body.bounds;
            var size = (Vector2)bounds.size * 0.95f;
            Vector2 center = bounds.center;
            float feetY = bounds.min.y;
            float killY = FallRecovery.FloorY + FallRecovery.KillY;

            // 아래 땅(발판 제외)까지의 거리: 몸 Cast는 합쳐진 땅 콜라이더에서 옆 벽 hit 하나만 돌려줄 수 있어,
            // 발밑의 좁은 상자를 따로 밀어 본다 (구석에 붙어 서 있어도 바닥을 놓치지 않게).
            float solidHit = FeetCast(new Vector2(center.x, feetY + 0.05f), size.x * 0.9f, distance + 0.1f, solidOnly: true);

            return TeleportRules.PlanDown(distance, solidHit, feetY, killY,
                t => Physics2D.OverlapBox(center + Vector2.down * t, size, 0f, groundFilter, overlapResults) > 0,
                t => FeetCast(new Vector2(center.x, feetY - t + 0.05f), size.x, TeleportRules.MaxSafeDrop + 0.1f, solidOnly: false));
        }

        // 발 밑 얇은 상자를 아래로 밀어 처음 닿는 디딜 곳까지의 거리 (없으면 PositiveInfinity).
        // solidOnly: 발판 역할 오브젝트는 건너뛴다 (뚫고 내려갈 수 있으므로): 위에서만 밟히는 발판(effector)과
        //   UnsafeGround가 붙은 발판(움직이는·스위치 발판은 effector 없는 단단한 상자라 예전에는 아래 순간이동을 막았다).
        // 무너지는 발판·움직이는 발판(UnsafeGround)은 내려선 뒤 떨어질 수 있으니 안전한 착지로 치지 않는다.
        private float FeetCast(Vector2 origin, float width, float maxDistance, bool solidOnly)
        {
            int count = Physics2D.BoxCast(origin, new Vector2(width, 0.05f), 0f, Vector2.down, groundFilter, castResults, maxDistance);
            float nearest = float.PositiveInfinity;
            bool nearestUnsafe = false;
            for (int i = 0; i < count; i++)
            {
                var hit = castResults[i];
                if (solidOnly && IsPlatform(hit.collider)) continue;
                float d = Mathf.Max(hit.distance - 0.05f, 0f);
                if (d > nearest) continue;
                bool unsafeHere = hit.collider.GetComponentInParent<UnsafeGround>() != null;
                nearestUnsafe = d < nearest ? unsafeHere : nearestUnsafe || unsafeHere; // 같은 거리면 불안한 쪽을 따른다
                nearest = d;
            }

            if (!solidOnly && nearestUnsafe) return float.PositiveInfinity; // 처음 닿는 곳이 불안한 발판이면 안전하지 않다
            return nearest;
        }

        private static bool IsPlatform(Collider2D collider) =>
            collider.usedByEffector || collider.GetComponentInParent<UnsafeGround>() != null;

        private void Teleport(Vector2 direction, float travel)
        {
            Vector2 from = rb.position;
            Vector2 to = from + direction * travel;

            var visual = transform.Find("Visual");
            if (visual != null) Afterimage.Spawn(visual.GetComponent<SpriteRenderer>(), new Color(0.8f, 0.6f, 1f), 0.6f, 0.25f); // 떠난 자리에 잔상
            EffectLibrary.PlayOneShot(from, EffectLibrary.TeleportFrames, 20f, new Color(0.8f, 0.6f, 1f, 0.7f), 1.2f);
            rb.position = to;
            transform.position = to;
            var movement = GetComponent<PlayerMovement>();
            if (movement != null) movement.ResetAirborne();
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, 0f));
            EffectLibrary.PlayOneShot(to, EffectLibrary.TeleportFrames, 20f, new Color(0.8f, 0.6f, 1f, 0.7f), 1.2f);

            if (direction.x != 0f) transform.localScale = new Vector3(Mathf.Sign(direction.x), 1f, 1f);

            health.Invulnerable = true;
            grantedInvulnerability = true;
            invulnerableUntil = Time.time + invulnerableTime;

            SkillMessages.Post(ManaUser.PlayerA, "순간이동");
        }
    }
}
