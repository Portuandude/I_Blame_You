using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 B(공격 담당)의 평타. 좌클릭으로 바라보는 방향에 무속성 마력탄(SpellBook.BasicBolt, 마나 5)을 쏜다.
    // 공격 간격은 공격 속도(초당 횟수)로 정한다. 근접 판정은 2026-10-16에 마력탄으로 바뀌었다.
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(CombatStats))]
    [RequireComponent(typeof(PlayerControlState))]
    public class PlayerCombat : MonoBehaviour
    {
        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");

        [Header("Attack")]
        [SerializeField] private float attackDuration = 0.5f; // 애니메이션 길이 (Player_Attack 8프레임@16)

        private Animator animator;
        private PlayerMovement movement;
        private HealthSystem health;
        private CombatStats stats;
        private PlayerControlState control;
        private HitStun hitStun;
        private SpellCaster caster;
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
            caster = GetComponent<SpellCaster>();
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
            // 평타 = 무속성 마력탄 (마나 5). 마나가 모자라면 공격 동작도 하지 않는다.
            if (caster == null) caster = GetComponent<SpellCaster>(); // 스포너가 SpellCaster를 나중에 붙이는 경우
            if (caster == null || !caster.FireBasicBolt()) return;

            isAttacking = true;
            attackTimeRemaining = attackDuration;
            nextAttackTime = Time.time + Mathf.Max(stats.AttackInterval, attackDuration);
            if (animator != null) animator.SetBool(IsAttackingParam, true);
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
    }
}
