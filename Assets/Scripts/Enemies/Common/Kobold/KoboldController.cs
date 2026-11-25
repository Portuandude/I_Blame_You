using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 돌격형 (스테이지 2 숲, 2026-11-24 사람 결정 C1): 코볼트 근접 돌격형·와르그·코볼트 족장이 같이 쓴다.
    // 감지 거리 안이면 쫓다가, 돌격 거리(가로 ChargeMin~ChargeMax, 높이 차 작음)에 들면 웅크려 예고(Windup) →
    // 앞으로 빠르게 달린다(Charge, 벽·낭떠러지 앞에서 멈춤). 달리는 동안 몸 앞 판정에 닿은 플레이어를 한 번 때린다 → 숨 고르기(Recover).
    // 덮치기(pounce)가 켜진 몸(와르그)은 돌격 첫 순간 살짝 뛰어오른다. 그림은 임시 (고블린·슬라임 그림 색만 바꿈, 아트 규칙).
    public class KoboldController : EnemyController
    {
        private enum Phase { None, Windup, Charge, Recover }

        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");
        private const float ArrivalTolerance = 0.2f;
        private const float LoseRangeMultiplier = 1.25f;

        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private float chargeMin = 1.2f;
        [SerializeField] private float chargeMax = 5.5f;
        [SerializeField] private float windupTime = 0.5f;
        [SerializeField] private float chargeSpeed = 8.5f;
        [SerializeField] private float chargeTime = 0.45f;
        [SerializeField] private float recoverTime = 0.6f;
        [SerializeField] private float chargeDamage = 1.2f;
        [SerializeField] private float pounceSpeed;          // 0이면 땅으로 달림 (와르그는 위로 튀어 덮침)
        [SerializeField] private Vector2 hitbox = new Vector2(0.8f, 0.8f);
        [SerializeField] private LayerMask obstacleLayer;

        private readonly List<Collider2D> overlap = new List<Collider2D>();
        private readonly HashSet<HealthSystem> struck = new HashSet<HealthSystem>();
        private Phase phase;
        private float phaseLeft;
        private float nextChargeTime;
        private float facing = -1f;
        private float moveDirection;
        private float homeX;
        private bool chasing;

        // 바깥(족장 그물 등)이 돌격 중인지 보고 다른 기술을 미룬다
        public bool Busy => phase != Phase.None;
        public float Facing => facing;
        public bool Stunned => IsStunned; // 족장 그물이 경직 중에는 던지지 않게

        protected override bool ContactDamageActive => phase == Phase.None || phase == Phase.Recover;

        // 보스 무리(KoboldPack)는 화면 위 보스 체력바가 합쳐 보여 주므로 머리 위 체력바를 끈다
        public bool ShowsOwnHealthBar { get; set; } = true;
        protected override bool ShowsHealthBar => ShowsOwnHealthBar;

        public void Configure(float detection, float minRange, float maxRange, float windup, float speed, float time, float recover,
            float damage, float pounce = 0f)
        {
            detectionRange = detection;
            chargeMin = minRange;
            chargeMax = maxRange;
            windupTime = windup;
            chargeSpeed = speed;
            chargeTime = time;
            recoverTime = recover;
            chargeDamage = damage;
            pounceSpeed = pounce;
        }

        public void ConfigureObstacleLayer(LayerMask layer) => obstacleLayer = layer;

        public void ConfigureHitbox(Vector2 size) => hitbox = size;

        // 광폭화 등으로 돌격 사이를 줄인다 (1 = 그대로)
        public float CooldownMultiplier { get; set; } = 1f;

        private void Start()
        {
            homeX = transform.position.x;
            SetFacing(facing);
            nextChargeTime = Time.time + 0.8f; // 나타나자마자 달려들지 않게
        }

        protected override void OnAliveUpdate()
        {
            var target = FindTarget();
            UpdateChase(target);

            if (phase == Phase.Windup && target == null)
            {
                // 웅크린 사이 플레이어가 쓰러지면 시체 쪽으로 돌격하지 않고 거둔다 (슬라임·고블린과 같게, 버그 점검 2026-11-25)
                StopAll();
                nextChargeTime = Time.time + Stats.AttackInterval * CooldownMultiplier;
                return;
            }
            if (phase != Phase.None)
            {
                UpdatePhase();
                return;
            }

            if (chasing && TryStartCharge(target)) return;

            if (chasing)
            {
                float dx = target.position.x - transform.position.x;
                moveDirection = Mathf.Abs(dx) > ArrivalTolerance ? Mathf.Sign(dx) : 0f;
                if (moveDirection != 0f) SetFacing(moveDirection);
                if (Mathf.Abs(dx) < chargeMin * 0.6f) moveDirection = 0f;
                if (moveDirection != 0f && WallAhead(moveDirection)) moveDirection = 0f;
                return;
            }

            float offset = transform.position.x - homeX;
            if (Mathf.Abs(offset) > 3f)
            {
                SetFacing(-Mathf.Sign(offset));
                if (WallAhead(facing)) { homeX = transform.position.x; SetFacing(-facing); }
            }
            else if (WallAhead(facing)) SetFacing(-facing);
            moveDirection = facing;
        }

        protected override void Move()
        {
            switch (phase)
            {
                case Phase.Charge:
                    if (WallAhead(facing) || (pounceSpeed > 0f && AirLedgeAhead(facing))) { EndCharge(); Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y); return; }
                    Rb.linearVelocity = new Vector2(facing * chargeSpeed * Stats.TotalMoveMultiplier, Rb.linearVelocity.y);
                    StrikeAhead();
                    return;
                case Phase.Windup:
                case Phase.Recover:
                    Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
                    return;
            }

            float speed = Stats.MoveSpeedUnits * (chasing ? 1f : 0.5f);
            Rb.linearVelocity = new Vector2(moveDirection * speed, Rb.linearVelocity.y);
        }

        protected override void OnDied()
        {
            StopAll();
        }

        private bool TryStartCharge(Transform target)
        {
            if (target == null || Time.time < nextChargeTime || IsStunned) return false;
            float dx = target.position.x - transform.position.x;
            float dy = Mathf.Abs(target.position.y - transform.position.y);
            if (Mathf.Abs(dx) < chargeMin || Mathf.Abs(dx) > chargeMax || dy > 1.5f) return false;

            SetFacing(Mathf.Sign(dx));
            moveDirection = 0f;
            phase = Phase.Windup;
            phaseLeft = windupTime;
            EnemySquash.For(gameObject).Windup(windupTime);
            EnemyTelegraph.Windup(this, BodyBounds);
            VfxPlayer.AimGlint((Vector2)transform.position + new Vector2(facing * 0.5f, BodyBounds.size.y * 0.6f));
            return true;
        }

        private void UpdatePhase()
        {
            if (IsStunned)
            {
                StopAll();
                nextChargeTime = Time.time + Stats.AttackInterval * CooldownMultiplier;
                return;
            }

            phaseLeft -= Time.deltaTime;
            if (phaseLeft > 0f) return;

            switch (phase)
            {
                case Phase.Windup:
                    phase = Phase.Charge;
                    phaseLeft = chargeTime;
                    struck.Clear();
                    EnemySquash.For(gameObject).Release();
                    if (Animator != null) Animator.SetBool(IsAttackingParam, true);
                    if (pounceSpeed > 0f) Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, pounceSpeed);
                    GameAudio.PlaySfx("Dash", 0.45f, 0.1f);
                    VfxPlayer.Dust(transform.position + new Vector3(-facing * 0.3f, 0f, 0f), 0.6f); // 박차는 먼지
                    break;
                case Phase.Charge:
                    EndCharge();
                    break;
                default:
                    phase = Phase.None;
                    break;
            }
        }

        private void EndCharge()
        {
            if (phase != Phase.Charge) return;
            phase = Phase.Recover;
            phaseLeft = recoverTime;
            nextChargeTime = Time.time + recoverTime + Stats.AttackInterval * CooldownMultiplier;
            if (Animator != null) Animator.SetBool(IsAttackingParam, false);
        }

        private void StopAll()
        {
            if (phase == Phase.Windup) EnemySquash.For(gameObject).Cancel();
            phase = Phase.None;
            if (Animator != null) Animator.SetBool(IsAttackingParam, false);
        }

        private void StrikeAhead()
        {
            var body = BodyBounds;
            Vector2 center = new Vector2(body.center.x + facing * (body.extents.x + hitbox.x * 0.3f), body.center.y);
            foreach (var hit in OverlapQuery.Box(center, hitbox, overlap))
            {
                var health = hit.GetComponentInParent<HealthSystem>();
                if (health == null || !struck.Add(health)) continue;
                if (StrikePlayer(health, chargeDamage))
                    EffectLibrary.PlayOneShot(hit.bounds.ClosestPoint(center), EffectLibrary.SlashFrames, 24f, new Color(1f, 0.85f, 0.6f, 0.8f), 0.9f, facing < 0f);
            }
        }

        private void UpdateChase(Transform target)
        {
            if (target == null) { chasing = false; return; }
            float distance = Vector2.Distance(transform.position, target.position);
            bool was = chasing;
            chasing = chasing ? distance <= detectionRange * LoseRangeMultiplier : distance <= detectionRange;
            if (chasing && !was) ShowNotice();
        }

        private bool WallAhead(float direction)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.4f);
            return LedgeAhead(direction, obstacleLayer) || Physics2D.Raycast(origin, new Vector2(direction, 0f), BodyBounds.extents.x + 0.3f, obstacleLayer);
        }

        // 덮치기로 뜬 동안에는 LedgeAhead(발밑에 땅이 있을 때만 봄)가 꺼지므로, 앞쪽 아래로 더 길게 바닥을 찾는다.
        // 없으면 돌격을 멈춰 와르그가 낭떠러지 밖으로 날아가 떨어지지 않게 (버그 점검 2026-11-24)
        private const float AirLedgeCheck = 6f;

        private bool AirLedgeAhead(float direction)
        {
            var bounds = BodyBounds;
            Vector2 ahead = new Vector2(bounds.center.x + direction * (bounds.extents.x + 0.25f), bounds.min.y + 0.1f);
            return !Physics2D.Raycast(ahead, Vector2.down, AirLedgeCheck, obstacleLayer);
        }

        private void SetFacing(float direction)
        {
            facing = direction;
            transform.localScale = new Vector3(direction, 1f, 1f);
        }
    }
}
