using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(StaminaSystem))]
    [RequireComponent(typeof(HealthSystem))]
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
        [SerializeField] private KeyCode dashKey = KeyCode.LeftControl;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.1f;
        [SerializeField] private LayerMask groundLayer;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int VerticalVelocityParam = Animator.StringToHash("VerticalVelocity");
        private static readonly int IsDashingParam = Animator.StringToHash("IsDashing");

        private Rigidbody2D rb;
        private StaminaSystem stamina;
        private HealthSystem health;
        private Animator animator;
        private bool isGrounded;
        private bool isRunning;
        private bool jumpQueued;
        private bool isDashing;
        private float dashTimeRemaining;
        private float dashDirection;
        private float defaultGravityScale;

        public bool IsRunning => isRunning;
        public bool IsGrounded => isGrounded;
        public bool IsDashing => isDashing;

        // 인스펙터에서 손으로 배치한 프리팹이 아니라 코드로 생성한 플레이어(예: PlayerSpawner)를 위한 설정 진입점.
        public void ConfigureGroundCheck(Transform check, LayerMask layer)
        {
            groundCheck = check;
            groundLayer = layer;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            stamina = GetComponent<StaminaSystem>();
            health = GetComponent<HealthSystem>();
            animator = GetComponentInChildren<Animator>();
            defaultGravityScale = rb.gravityScale;

            // 인스펙터에서 손으로 만든 Rigidbody2D는 회전 잠금이 꺼져 있을 수 있어,
            // 캡슐/원형 콜라이더가 바닥 모서리에 걸리면 캐릭터가 넘어지듯 회전한다. 항상 잠가둔다.
            rb.freezeRotation = true;
        }

        private void Update()
        {
            isGrounded = groundCheck != null &&
                Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                jumpQueued = true;
            }

            if (Input.GetKeyDown(dashKey) && !isDashing && stamina.TryConsume(dashStaminaCost))
            {
                StartDash();
            }

            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            animator.SetFloat(SpeedParam, Mathf.Abs(rb.linearVelocity.x));
            animator.SetBool(IsGroundedParam, isGrounded);
            animator.SetFloat(VerticalVelocityParam, rb.linearVelocity.y);
            animator.SetBool(IsDashingParam, isDashing);
        }

        private void StartDash()
        {
            isDashing = true;
            dashTimeRemaining = dashDuration;
            dashDirection = Mathf.Sign(transform.localScale.x);
            rb.gravityScale = 0f;
            health.Invulnerable = true;
        }

        private void EndDash()
        {
            isDashing = false;
            rb.gravityScale = defaultGravityScale;
            health.Invulnerable = false;
        }

        private void FixedUpdate()
        {
            if (isDashing)
            {
                rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
                dashTimeRemaining -= Time.fixedDeltaTime;
                if (dashTimeRemaining <= 0f)
                {
                    EndDash();
                }
                return;
            }

            if (jumpQueued)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpQueued = false;
            }

            float moveInput = Input.GetAxisRaw("Horizontal");
            bool wantsToRun = Mathf.Abs(moveInput) > 0.01f &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

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
            rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

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
