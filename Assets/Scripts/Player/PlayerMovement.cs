using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(StaminaSystem))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(HitStun))]
    [RequireComponent(typeof(PlayerControlState))]
    // 플레이어 A(이동 담당)의 이동/점프/달리기/대쉬. 입력은 RoleInput에서 A 역할 키만 읽는다.
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 9f;
        [SerializeField] private float jumpForce = 12f;

        [Header("Run Stamina")]
        [SerializeField] private float runStaminaDrainPerSecond = 25f;
        [SerializeField] private float minStaminaToStartRun = 10f;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashStaminaCost = 50f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.1f;
        [SerializeField] private LayerMask groundLayer;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");
        private static readonly int IsDashingParam = Animator.StringToHash("IsDashing");
        private static readonly int IsRunningParam = Animator.StringToHash("IsRunning");

        private Rigidbody2D rb;
        private StaminaSystem stamina;
        private HealthSystem health;
        private HitStun hitStun;
        private PlayerControlState control;
        private Animator animator;
        private CombatStats combatStats;
        private bool isGrounded;
        private bool wasGrounded = true;  // 처음 놓일 때 착지 먼지가 나지 않게
        private float airMinVelocityY;    // 공중에서 가장 빨리 떨어진 속도 (착지 프레임에는 이미 0이라 따로 기억)
        private const float DustFallSpeed = 6f;   // 이보다 빨리 떨어져야 착지 먼지 (작은 턱은 먼지 없음)
        private const float DustFullSpeed = 18f;
        private const float LandSoundSpeed = 11f;   // 이보다 빨리 떨어져야 착지음 Land (보통 점프 착지는 소리 없음)
        private const float LandSoundCooldown = 0.25f;
        private float lastLandSoundTime = -1f;
        private bool isRunning;
        private bool jumpQueued;
        private bool isDashing;
        private float dashTimeRemaining;
        private float dashDirection;
        private float defaultGravityScale;
        private Vector2 platformVelocity; // 움직이는 발판이 이번 물리 프레임에 넘겨준 속도 (한 번 쓰고 비운다)

        public bool IsRunning => isRunning;
        public bool IsGrounded => isGrounded;
        // 확실히 땅에 서 있는지: 점프를 눌렀는데 아직 물리 프레임이 안 돌아 isGrounded가 남아 있는 순간도 공중으로 본다
        // (마력 안개 근처 공중 금지를 점프 직후 순간이동으로 피하지 못하게, BlinkWard.Blocks).
        public bool IsGroundedStable => isGrounded && !jumpQueued && rb.linearVelocity.y <= 0.1f;
        // 발밑 높이 (몸 기준). 발밑에 붙이는 효과(레벨업 빛기둥 등)가 쓴다.
        public float FeetLocalY => groundCheck != null ? groundCheck.localPosition.y : 0f;
        public bool IsDashing => isDashing;
        public float PostDashGrace { get; set; } // 대쉬가 끝난 뒤 더 이어지는 무적 시간 (유물 그림자 베일)
        public float Facing => Mathf.Sign(transform.localScale.x);

        public float DashStaminaCost
        {
            get => dashStaminaCost;
            set => dashStaminaCost = value;
        }

        public float DashDuration
        {
            get => dashDuration;
            set => dashDuration = value;
        }

        // 스탯 문서의 이동속도(문서 단위)를 게임 속도로 바꿔 지정한다. 달리기는 기획서 5.1 가안대로 걷기 × 1.8.
        public void ConfigureSpeeds(float walk, float run)
        {
            walkSpeed = walk;
            runSpeed = run;
        }

        // 인스펙터에서 손으로 배치한 프리팹이 아니라 코드로 생성한 플레이어(예: PlayerSpawner)를 위한 설정 진입점.
        public void ConfigureGroundCheck(Transform check, LayerMask layer)
        {
            groundCheck = check;
            groundLayer = layer;
        }

        // MovingPlatform이 자기 FixedUpdate(먼저 실행)에서 부른다. 발판 위에 있는 프레임에만 들어온다.
        public void SetPlatformVelocity(Vector2 velocity)
        {
            platformVelocity = velocity;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            stamina = GetComponent<StaminaSystem>();
            health = GetComponent<HealthSystem>();
            hitStun = GetComponent<HitStun>();
            if (hitStun == null) hitStun = gameObject.AddComponent<HitStun>(); // 프리팹을 재생성하기 전의 구버전 대비
            control = GetComponent<PlayerControlState>();
            if (control == null) control = gameObject.AddComponent<PlayerControlState>();
            animator = GetComponentInChildren<Animator>();
            combatStats = GetComponent<CombatStats>();
            defaultGravityScale = rb.gravityScale;

            // 인스펙터에서 손으로 만든 Rigidbody2D는 회전 잠금이 꺼져 있을 수 있어,
            // 캡슐/원형 콜라이더가 바닥 모서리에 걸리면 캐릭터가 넘어지듯 회전한다. 항상 잠가둔다.
            rb.freezeRotation = true;

            // 물리는 고정 프레임(50Hz)으로 위치를 갱신하는데 카메라는 렌더 프레임마다 따라가서, 보간이 없으면
            // 캐릭터가 계단식으로 움직이고 카메라와 박자가 어긋나 번져/떨려 보인다. 렌더 프레임 사이를 보간한다.
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(float amount)
        {
            if (!health.IsDead) GameAudio.PlaySfx("Hurt", 0.7f);
        }

        private void Update()
        {
            isGrounded = groundCheck != null &&
                Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            UpdateLandingDust();

            bool stunned = hitStun.IsStunned;

            if (RoleInput.JumpDown(control) && isGrounded && !stunned)
            {
                jumpQueued = true;
            }

            // 마력 안개(스위치 발판 낭떠러지) 안이나, 공중에서 안개 가까이에서는 대시를 시작하지 못한다 (기력도 쓰지 않음).
            if (RoleInput.DashDown(control) && !isDashing && !stunned && !BlinkWard.Blocks(rb.position, IsGroundedStable) && stamina.TryConsume(dashStaminaCost))
            {
                StartDash();
            }

            UpdateAnimator();
        }

        // 순간이동·낭떠러지 복귀처럼 떨어지던 중에 위치를 옮길 때: 옮긴 자리에서 착지 먼지가 나지 않게 낙하 기록을 지운다.
        public void ResetAirborne()
        {
            airMinVelocityY = 0f;
            wasGrounded = true;
            var squash = GetComponent<PlayerSquash>();
            if (squash != null) squash.ResetAirborne(); // 옮긴 자리에서 가짜 착지 찌그러짐이 나지 않게
        }

        private void UpdateLandingDust()
        {
            if (!isGrounded) airMinVelocityY = Mathf.Min(airMinVelocityY, rb.linearVelocity.y);
            else if (!wasGrounded && airMinVelocityY < -DustFallSpeed)
            {
                VfxPlayer.Dust(groundCheck.position, Mathf.InverseLerp(DustFallSpeed, DustFullSpeed, -airMinVelocityY));
                // 높은 곳에서 떨어진 착지만 폭신한 착지음 (세게 떨어질수록 조금 크게, 연달아 울리지 않게)
                if (-airMinVelocityY > LandSoundSpeed && Time.time - lastLandSoundTime > LandSoundCooldown)
                {
                    lastLandSoundTime = Time.time;
                    GameAudio.PlaySfx("Land", Mathf.Lerp(0.35f, 0.75f, Mathf.InverseLerp(LandSoundSpeed, DustFullSpeed, -airMinVelocityY)));
                }
            }
            if (isGrounded) airMinVelocityY = 0f;
            wasGrounded = isGrounded;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            animator.SetFloat(SpeedParam, Mathf.Abs(rb.linearVelocity.x));
            animator.SetBool(IsGroundedParam, isGrounded);
            animator.SetFloat(VerticalVelocityParam, rb.linearVelocity.y);
            animator.SetBool(IsDashingParam, isDashing);
            animator.SetBool(IsRunningParam, isRunning);
        }

        private void StartDash()
        {
            isDashing = true;
            dashTimeRemaining = dashDuration;
            dashDirection = Mathf.Sign(transform.localScale.x);
            rb.gravityScale = 0f;
            health.Invulnerable = true;
            jumpQueued = false; // 같은 프레임에 누른 점프가 대쉬가 끝난 뒤 공중에서 터지지 않게
            GameAudio.PlaySfx("Dash", 0.8f);
        }

        private void EndDash()
        {
            isDashing = false;
            rb.gravityScale = defaultGravityScale;
            health.Invulnerable = false;
            if (PostDashGrace > 0f) health.GrantGrace(PostDashGrace);
        }

        private void FixedUpdate()
        {
            float carryX = platformVelocity.x;
            platformVelocity = Vector2.zero;

            if (isDashing)
            {
                rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
                dashTimeRemaining -= Time.fixedDeltaTime;
                // 대시하다 마력 안개에 들어가면 그 자리에서 흩어진다: 가로 속도를 걷기 속도로 낮춰 대시 거리로 건너뛰지 못하게.
                if (BlinkWard.Contains(rb.position))
                {
                    EndDash();
                    rb.linearVelocity = new Vector2(dashDirection * walkSpeed, 0f);
                    return;
                }
                if (dashTimeRemaining <= 0f)
                {
                    EndDash();
                }
                return;
            }

            if (hitStun.IsStunned || control.MovementFrozen)
            {
                isRunning = false;
                jumpQueued = false;
                rb.linearVelocity = new Vector2(carryX, rb.linearVelocity.y);
                return;
            }

            if (jumpQueued)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpQueued = false;
                GameAudio.PlaySfx("Jump", 0.6f);
            }

            float moveInput = RoleInput.MoveAxis(control);
            bool wantsToRun = Mathf.Abs(moveInput) > 0.01f && RoleInput.RunHeld(control);

            // 스태미나가 임계치 이상일 때만 달리기를 새로 시작하게 해서, 0 근처에서 걷기/달리기가 매 프레임 토글되는 것을 막는다.
            if (wantsToRun && !isRunning && stamina.Current >= minStaminaToStartRun)
            {
                isRunning = true;
            }

            if (isRunning && (!wantsToRun || !stamina.TryConsume(runStaminaDrainPerSecond * Time.fixedDeltaTime)))
            {
                isRunning = false;
            }

            float speed = isRunning ? runSpeed : walkSpeed;
            if (combatStats != null) speed *= combatStats.TotalMoveMultiplier; // 특성 이동속도, 젖음/붙잡기 둔화
            rb.linearVelocity = new Vector2(moveInput * speed + carryX, rb.linearVelocity.y);

            if (Mathf.Abs(moveInput) > 0.01f)
            {
                transform.localScale = new Vector3(Mathf.Sign(moveInput), 1f, 1f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
