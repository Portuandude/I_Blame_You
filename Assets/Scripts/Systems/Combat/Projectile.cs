using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 날아가며 겹친 대상에게 피해를 주는 투사체 (B의 공격 스킬, 고블린 궁수의 화살).
    // Rigidbody 없이 매 물리 프레임 위치를 옮기고 겹침 조회로 판정한다. 벽/바닥에 닿으면 사라진다.
    // 적이 쏜 투사체는 바람 구멍 기둥을 지나며 위로 휜다 (WindVent.LiftAt).
    // SetHoming을 부르면 가장 가까운 대상 쪽으로 초당 turnRate도까지 방향을 꺾는다 (유도성). 플레이어 투사체만.
    public class Projectile : MonoBehaviour
    {
        private CombatStats owner;
        private bool hitsEnemies;
        private DamageKind kind;
        private Element element;
        private float multiplier = 1f;
        private Vector2 velocity;
        private float radius;
        private bool piercing;
        private float lifeRemaining;
        private LayerMask groundMask;
        private readonly HashSet<HealthSystem> alreadyHit = new HashSet<HealthSystem>();
        // 겹침 검사 결과를 담아 두고 다시 쓴다 (OverlapCircleAll은 물리 프레임마다 새 배열을 만든다). 투사체마다 따로 둬서 맞힘 처리 중 다른 투사체가 덮어쓰지 않는다
        private readonly List<Collider2D> overlaps = new List<Collider2D>(8);
        private Vector2 launchPosition;
        private bool launchKnown;

        private const float RetargetInterval = 0.1f;
        private float homingTurnRate;
        private float homingRange;
        private float retargetTimer;
        private HealthSystem homingTarget;

        // 피해를 준 직후 대상마다 한 번 호출된다 (속성 디버프 부여 등).
        public System.Action<HealthSystem> Hit;

        // 방금 Hit을 부른 적중이 무적(대시·순간이동·경직 무적 등)에 막혔는지. 맞은 뒤 Strike가 켠 경직 무적과 구분하려고 Strike 전에 잰다.
        public bool LastHitBlocked { get; private set; }

        // 벽/바닥에 부딪혀 사라질 때 그 위치로 한 번 호출된다 (적중 이펙트 등).
        public System.Action<Vector2> Impact;

        // turnRate: 초당 최대 회전 각도(도). range: 이 거리 안의 대상만 쫓는다.
        public void SetHoming(float turnRate, float range)
        {
            if (!hitsEnemies) return; // 적이 쏜 투사체(화살 등)는 유도하지 않는다. Launch 뒤에 불러야 한다.
            homingTurnRate = turnRate;
            homingRange = range;
            retargetTimer = 0f;
        }

        // owner: 피해 계산에 쓸 공격자 스탯. hitEnemies: true면 적만, false면 플레이어만 맞힌다.
        public void Launch(CombatStats attacker, bool hitEnemies, DamageKind damageKind, Element damageElement, Vector2 direction,
            float speed, float hitRadius, float range, bool pierce, float damageMultiplier = 1f)
        {
            owner = attacker;
            hitsEnemies = hitEnemies;
            kind = damageKind;
            element = damageElement;
            multiplier = damageMultiplier;
            velocity = direction.normalized * speed;
            radius = hitRadius;
            piercing = pierce;
            lifeRemaining = speed > 0f ? range / speed : 1f;

            int ground = LayerMask.NameToLayer("Ground");
            groundMask = ground >= 0 ? (LayerMask)(1 << ground) : (LayerMask)0;

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // 적이 쏜 화살이 바람 구멍 기둥 안을 지나면 위로 휜다 (2026-11-14). 빠르기는 그대로 두고 방향만 꺾는다.
        private void BendInWind(Vector2 position)
        {
            float lift = WindVent.LiftAt(position);
            if (lift <= 0f) return;
            float speed = velocity.magnitude;
            if (speed <= 0f) return;
            velocity = (velocity + Vector2.up * lift * Time.fixedDeltaTime).normalized * speed;
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void FixedUpdate()
        {
            Vector2 position = transform.position;
            if (!launchKnown)
            {
                launchPosition = position;
                launchKnown = true;
            }
            if (homingTurnRate > 0f) Steer(position);
            if (!hitsEnemies) BendInWind(position);
            Vector2 step = velocity * Time.fixedDeltaTime;

            var wall = groundMask != 0 ? Physics2D.Raycast(position, step.normalized, step.magnitude, groundMask) : default;
            if (wall.collider != null)
            {
                // 플레이어 마법이 봉인문 같은 속성 장치에 맞으면 속성을 전달한다 (적이 쏜 화살은 제외).
                if (hitsEnemies)
                {
                    var pushed = wall.collider.GetComponentInParent<IElementPushReceiver>(); // 바람 상자: 날아온 방향도 넘긴다
                    if (pushed != null) pushed.ReceiveElement(element, velocity);
                    else
                    {
                        var receiver = wall.collider.GetComponentInParent<IElementReceiver>();
                        if (receiver != null) receiver.ReceiveElement(element);
                    }
                }
                Impact?.Invoke(wall.point);
                Destroy(gameObject);
                return;
            }

            transform.position = position + step;

            Overlap(transform.position, radius);
            foreach (var hit in overlaps)
            {
                // 길을 막지 않는 트리거 속성 장치 (마법 스위치 룬). 플레이어 마법만 전달하고 사라진다.
                // 쏜 자리에서 이미 겹쳐 있던 장치(룬 위에 서서 다른 룬을 쏠 때)는 건너뛴다: 쏜 방향으로 장치 반폭보다 앞에 있어야 한다.
                if (hitsEnemies && hit.isTrigger)
                {
                    var touched = hit.GetComponent<IElementTouchReceiver>(); // 숲 덩굴 등: 속성만 알리고 그대로 지나간다
                    if (touched != null) { touched.TouchElement(element); continue; }
                    var trigger = hit.GetComponent<IElementReceiver>();
                    var bounds = hit.bounds;
                    bool ahead = velocity.x == 0f
                        || (bounds.center.x - launchPosition.x) * Mathf.Sign(velocity.x) > bounds.extents.x;
                    if (trigger != null && ahead)
                    {
                        trigger.ReceiveElement(element);
                        Impact?.Invoke(transform.position);
                        Destroy(gameObject);
                        return;
                    }
                }

                var health = hit.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || !alreadyHit.Add(health)) continue;
                if (owner != null && health.gameObject == owner.gameObject) continue;

                bool isEnemy = health.GetComponent<IBlameYou.Enemies.EnemyController>() != null;
                if (isEnemy != hitsEnemies)
                {
                    alreadyHit.Remove(health);
                    continue;
                }

                // 관통 탄은 무적인 대상을 "맞힘"으로 남기지 않고 지나간다: 무적이 풀린 뒤 아직 겹쳐 있으면 그때 맞힌다 (버그 점검 2026-11-24)
                if (piercing && health.IsInvulnerable)
                {
                    alreadyHit.Remove(health);
                    continue;
                }

                LastHitBlocked = health.IsInvulnerable;
                DamageCalculator.Strike(owner, health, kind, element, multiplier);
                Hit?.Invoke(health);
                if (!piercing)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            lifeRemaining -= Time.fixedDeltaTime;
            if (lifeRemaining <= 0f) Destroy(gameObject);
        }

        private void Steer(Vector2 position)
        {
            retargetTimer -= Time.fixedDeltaTime;
            if (homingTarget != null && (homingTarget.IsDead || homingTarget.IsInvulnerable || alreadyHit.Contains(homingTarget))) homingTarget = null; // 무적이 된 대상은 놓는다 (관통 탄이 무적 대상 둘레를 맴돌던 것, 버그 점검 2026-11-25)
            // 대상이 없어도 RetargetInterval마다만 찾는다 (적이 없을 때 물리 프레임마다 Overlap을 돌던 것, 버그 점검 2026-11-24)
            if (retargetTimer <= 0f)
            {
                homingTarget = FindTarget(position);
                retargetTimer = RetargetInterval;
            }

            if (homingTarget == null) return;

            Vector2 toTarget = (Vector2)homingTarget.transform.position + new Vector2(0f, 0.4f) - position;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            float current = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float angle = Mathf.MoveTowardsAngle(current, desired, homingTurnRate * Time.fixedDeltaTime);
            float speed = velocity.magnitude;
            velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // OverlapCircleAll과 같은 결과 (트리거 포함 여부는 프로젝트 설정 queriesHitTriggers를 따른다)
        private void Overlap(Vector2 center, float r)
        {
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            Physics2D.OverlapCircle(center, r, filter, overlaps);
        }

        // 아직 맞히지 않은 살아 있는 대상 중 가장 가까운 것. 지금 무적인 대상(대시·순간이동·등장 중)은 고르지 않는다.
        private HealthSystem FindTarget(Vector2 position)
        {
            HealthSystem best = null;
            float bestDistance = float.MaxValue;
            Overlap(position, homingRange);
            foreach (var col in overlaps)
            {
                var health = col.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || health.IsInvulnerable || alreadyHit.Contains(health)) continue;
                if (owner != null && health.gameObject == owner.gameObject) continue;
                bool isEnemy = health.GetComponent<IBlameYou.Enemies.EnemyController>() != null;
                if (isEnemy != hitsEnemies) continue;

                float distance = ((Vector2)health.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = health;
                }
            }

            return best;
        }
    }
}
