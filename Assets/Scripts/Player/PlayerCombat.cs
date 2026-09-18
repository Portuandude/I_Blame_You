using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    [RequireComponent(typeof(HealthSystem))]
    public class PlayerCombat : MonoBehaviour
    {
        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");

        [Header("Attack")]
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private Vector2 attackSize = new Vector2(1.2f, 1f);
        [SerializeField] private float attackForwardOffset = 0.8f;
        [SerializeField] private float attackDuration = 0.5f;

        // 애니메이터가 실제로 어떤 상태에 얼마나 머무는지 눈으로 확인하기 위한 임시 진단 로그.
        private static readonly string[] KnownAnimatorStates = { "Idle", "Run", "Rise", "Fall", "Dash", "Attack" };

        private Animator animator;
        private PlayerMovement movement;
        private HealthSystem health;
        private bool isAttacking;
        private float attackTimeRemaining;
        private string lastLoggedState;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<HealthSystem>();
        }

        private void Update()
        {
            LogAnimatorStateChange();

            if (Input.GetMouseButtonDown(0) && !isAttacking && !movement.IsDashing)
            {
                StartAttack();
            }

            if (isAttacking)
            {
                attackTimeRemaining -= Time.deltaTime;
                if (attackTimeRemaining <= 0f)
                {
                    EndAttack();
                }
            }
        }

        private void StartAttack()
        {
            isAttacking = true;
            attackTimeRemaining = attackDuration;
            Debug.Log($"[Attack] t={Time.time:F3} 클릭 -> IsAttacking=true (지속 {attackDuration:F2}s)");
            if (animator != null) animator.SetBool(IsAttackingParam, true);

            float facing = Mathf.Sign(transform.localScale.x);
            Vector2 center = (Vector2)transform.position + new Vector2(facing * attackForwardOffset, 0f);
            var hits = Physics2D.OverlapBoxAll(center, attackSize, 0f);

            foreach (var hit in hits)
            {
                var targetHealth = hit.GetComponentInParent<HealthSystem>();
                if (targetHealth == null || targetHealth == health) continue;
                targetHealth.TakeDamage(attackDamage);
            }
        }

        private void EndAttack()
        {
            isAttacking = false;
            Debug.Log($"[Attack] t={Time.time:F3} attackDuration 종료 -> IsAttacking=false");
            if (animator != null) animator.SetBool(IsAttackingParam, false);
        }

        private void LogAnimatorStateChange()
        {
            if (animator == null) return;

            var info = animator.GetCurrentAnimatorStateInfo(0);
            string current = "Unknown";
            foreach (var name in KnownAnimatorStates)
            {
                if (info.IsName(name))
                {
                    current = name;
                    break;
                }
            }

            if (current == lastLoggedState) return;

            Debug.Log($"[Anim] t={Time.time:F3} state -> {current} (normalizedTime={info.normalizedTime:F2})");
            lastLoggedState = current;
        }

        private void OnDrawGizmosSelected()
        {
            float facing = Mathf.Sign(transform.localScale.x);
            Vector2 center = (Vector2)transform.position + new Vector2(facing * attackForwardOffset, 0f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, attackSize);
        }
    }
}
