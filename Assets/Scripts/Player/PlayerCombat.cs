using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 B(공격 담당)의 평타. 마우스가 있는 쪽으로 전방 판정 박스를 휘두른다.
    // 피해는 스탯(물리 데미지)과 비율식 공식(DamageCalculator)으로, 공격 간격은 공격 속도(초당 횟수)로 정한다.
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(CombatStats))]
    [RequireComponent(typeof(PlayerControlState))]
    public class PlayerCombat : MonoBehaviour
    {
        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");

        [Header("Attack")]
        [SerializeField] private Vector2 attackSize = new Vector2(1.2f, 1f);
        [SerializeField] private float attackForwardOffset = 0.8f;
        [SerializeField] private float attackDuration = 0.5f; // 애니메이션 길이 (Player_Attack 8프레임@16)

        private Animator animator;
        private PlayerMovement movement;
        private HealthSystem health;
        private CombatStats stats;
        private PlayerControlState control;
        private HitStun hitStun;
        private bool isAttacking;
        private float attackTimeRemaining;
        private float nextAttackTime;
        private float attackFacing = 1f;

        public bool IsAttacking => isAttacking;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<HealthSystem>();
            stats = GetComponent<CombatStats>();
            control = GetComponent<PlayerControlState>();
            hitStun = GetComponent<HitStun>();
            if (hitStun == null) hitStun = gameObject.AddComponent<HitStun>(); // 프리팹을 재생성하기 전의 구버전 대비
        }

        private void Update()
        {
            if (RoleInput.AttackDown(control) && !isAttacking && Time.time >= nextAttackTime
                && !movement.IsDashing && !hitStun.IsStunned && !health.IsDead)
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

        // 스킬 시전 연출용으로 공격 애니메이션만 재생한다 (판정 없음).
        public void PlayCastAnimation(Vector2 aimPoint)
        {
            FaceTowards(aimPoint.x);
            isAttacking = true;
            attackTimeRemaining = attackDuration;
            if (animator != null) animator.SetBool(IsAttackingParam, true);
        }

        private void StartAttack()
        {
            var aim = RoleInput.AimWorldPoint((Vector2)transform.position + Vector2.right * movement.Facing);
            FaceTowards(aim.x);

            isAttacking = true;
            attackTimeRemaining = attackDuration;
            nextAttackTime = Time.time + Mathf.Max(stats.AttackInterval, attackDuration);
            if (animator != null) animator.SetBool(IsAttackingParam, true);

            Vector2 center = (Vector2)transform.position + new Vector2(attackFacing * attackForwardOffset, 0f);
            var hits = Physics2D.OverlapBoxAll(center, attackSize, 0f);

            var struck = new System.Collections.Generic.HashSet<HealthSystem>();
            foreach (var hit in hits)
            {
                var targetHealth = hit.GetComponentInParent<HealthSystem>();
                if (targetHealth == null || targetHealth == health || !struck.Add(targetHealth)) continue;
                DamageCalculator.Strike(stats, targetHealth, DamageKind.Physical);
            }
        }

        // B가 조준한 쪽으로 몸을 돌린다. 이후 A가 움직이면 다시 이동 방향을 본다.
        private void FaceTowards(float worldX)
        {
            attackFacing = worldX >= transform.position.x ? 1f : -1f;
            transform.localScale = new Vector3(attackFacing, 1f, 1f);
        }

        private void EndAttack()
        {
            isAttacking = false;
            if (animator != null) animator.SetBool(IsAttackingParam, false);
        }

        private void OnDisable()
        {
            if (isAttacking) EndAttack();
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 center = (Vector2)transform.position + new Vector2(attackFacing * attackForwardOffset, 0f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, attackSize);
        }
    }
}
