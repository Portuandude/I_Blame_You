using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(StaminaSystem))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 9f;
        [SerializeField] private float jumpForce = 12f;

        [Header("Run Stamina")]
        [SerializeField] private float runStaminaDrainPerSecond = 25f;
        [SerializeField] private float minStaminaToStartRun = 10f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.1f;
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody2D rb;
        private StaminaSystem stamina;
        private bool isGrounded;
        private bool isRunning;
        private bool jumpQueued;

        public bool IsRunning => isRunning;
        public bool IsGrounded => isGrounded;

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
        }

        private void Update()
        {
            isGrounded = groundCheck != null &&
                Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                jumpQueued = true;
            }
        }

        private void FixedUpdate()
        {
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
