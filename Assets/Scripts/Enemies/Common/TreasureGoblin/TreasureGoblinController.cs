using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 방 이벤트 "보물 고블린" (2026-11-14 사람 결정, ContentIdeas 이벤트 1): 금화 자루를 멘 고블린이 플레이어에게서 도망친다.
    // 공격·접촉 피해는 없고, EscapeTime(10초) 안에 쓰러뜨리면 골드를 많이 준다 (RunGold.OnEnemyKilled → Chunks/TreasureGoblinRule.Gold).
    // 시간이 다 되면 연기 속으로 사라진다 (쓰러뜨린 게 아니라 골드·경험치 없음). 마지막 3초는 머리 위에 3·2·1.
    // 벽·낭떠러지에 막히면 돌아서 플레이어 옆을 지나 반대쪽으로 달린다 (돌아선 뒤 TurnCommit초 동안은 다시 돌지 않는다).
    public class TreasureGoblinController : EnemyController
    {
        public const float EscapeTime = 10f;
        private const float FleeRange = 14f;      // 이 안에 플레이어가 있으면 도망 (밖이면 제자리 두리번)
        private const float TurnCommit = 0.8f;
        private const float WallCheckDistance = 0.6f;
        private const float WallCheckHeight = 0.4f;
        private const float SparkleInterval = 0.35f;
        private const float TrailInterval = 0.22f; // 달리는 동안 발밑 동전 자국 간격 (TreasureGoblinTrail)
        private static readonly Color CountColor = new Color(1f, 0.85f, 0.3f);

        private LayerMask ground;
        private float facing = 1f;
        private float moveDirection;
        private float bornAt;
        private float turnLockUntil;
        private float nextSparkle;
        private float nextTrail;
        private int lastCount = -1;
        private bool escaped;

        protected override bool ContactDamageActive => false;

        // 맞을 때마다 자루에서 금화가 흘러나온다 (2026-11-14 ① 컨텐츠, 놓쳐도 맞힌 만큼은 남게). 골드는 RunGold가 준다 (SkillHUD가 이 이벤트를 듣는다).
        // 한 마리에서 TreasureGoblinRule.MaxHitDrops번까지.
        public static event System.Action<TreasureGoblinController> Hit;
        private int hitDrops;

        protected override void Awake()
        {
            base.Awake();
            Health.Damaged += OnHitDropCoins;
        }

        private void OnHitDropCoins(float amount)
        {
            if (amount <= 0f || escaped || Health.IsDead || hitDrops >= TreasureGoblinRule.MaxHitDrops) return;
            hitDrops++;
            Hit?.Invoke(this);
        }

        public void Configure(LayerMask groundLayer) => ground = groundLayer;

        private void Start()
        {
            bornAt = Time.time;
            ShowNotice();
        }

        protected override void OnAliveUpdate()
        {
            float left = EscapeTime - (Time.time - bornAt);
            if (left <= 0f)
            {
                Escape();
                return;
            }

            int count = Mathf.CeilToInt(left);
            if (count <= 3 && count != lastCount)
            {
                lastCount = count;
                var body = BodyBounds;
                DamageNumbers.ShowText(new Vector3(body.center.x, body.max.y + 0.6f, 0f), count.ToString(), CountColor, 1.2f);
            }

            if (Time.time >= nextSparkle)
            {
                nextSparkle = Time.time + SparkleInterval;
                // 자루에서 금빛이 반짝 (길잡이 겸)
                EffectLibrary.PlayOneShot(transform.position + new Vector3(-facing * 0.3f, 0.7f, 0f), EffectLibrary.LoadRuntimeFrames("Effects/Coin"),
                    14f, new Color(1f, 0.85f, 0.3f, 0.8f), 0.25f);
            }

            var target = FindTarget();
            if (target == null || Mathf.Abs(target.position.x - transform.position.x) > FleeRange)
            {
                moveDirection = 0f;
                return;
            }

            if (Time.time >= turnLockUntil)
            {
                float away = transform.position.x >= target.position.x ? 1f : -1f;
                if (Blocked(away))
                {
                    away = -away; // 막혔으면 플레이어 옆을 지나 반대로
                    turnLockUntil = Time.time + TurnCommit;
                }
                SetFacing(away);
            }
            else if (Blocked(facing))
            {
                SetFacing(-facing);
                turnLockUntil = Time.time + TurnCommit;
            }

            moveDirection = Blocked(facing) ? 0f : facing;

            // 달리는 동안(땅 위) 발밑에 금빛 동전 자국 — 어디로 갔는지 보이게
            if (moveDirection != 0f && Mathf.Abs(Rb.linearVelocity.y) < 0.2f && Time.time >= nextTrail)
            {
                nextTrail = Time.time + TrailInterval;
                var body = BodyBounds;
                TreasureGoblinTrail.Drop(new Vector3(body.center.x - facing * 0.2f, body.min.y, 0f));
            }
        }

        protected override void Move()
        {
            Rb.linearVelocity = new Vector2(moveDirection * Stats.MoveSpeedUnits, Rb.linearVelocity.y);
        }

        private bool Blocked(float direction)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, WallCheckHeight);
            return LedgeAhead(direction, ground) || Physics2D.Raycast(origin, new Vector2(direction, 0f), WallCheckDistance, ground);
        }

        private void SetFacing(float direction)
        {
            facing = direction;
            transform.localScale = new Vector3(direction, 1f, 1f);
        }

        // 시간 초과: 연기 + "휙" 소리 + 알림, 그대로 사라진다 (Killed 이벤트 없음 → 골드·경험치 없음).
        private void Escape()
        {
            if (escaped) return;
            escaped = true;
            EffectLibrary.PlayOneShot(transform.position + Vector3.up * 0.5f, EffectLibrary.TeleportFrames, 20f, new Color(1f, 0.85f, 0.4f, 0.7f), 1.2f);
            GameAudio.PlaySfx("BlinkVanish", 0.7f);
            // 맞혀서 흘린 금화가 있으면 같이 알려 준다 (놓쳐도 헛걸음은 아니었다는 느낌)
            StageEvents.PostNotice(hitDrops > 0
                ? $"보물 고블린이 도망쳤다... (흘린 금화 +{hitDrops * TreasureGoblinRule.HitGold})"
                : "보물 고블린이 도망쳤다...");
            Destroy(gameObject);
        }
    }
}
