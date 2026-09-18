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

        private Animator animator;
        private PlayerMovement movement;
        private HealthSystem health;
        private HitStun hitStun;
        private bool isAttacking;
        private float attackTimeRemaining;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<HealthSystem>();
            hitStun = GetComponent<HitStun>();
            if (hitStun == null) hitStun = gameObject.AddComponent<HitStun>(); // 프리팹을 재생성하기 전의 구버전 대비
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && !isAttacking && !movement.IsDashing && !hitStun.IsStunned)
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
            if (animator != null) animator.SetBool(IsAttackingParam, false);
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
