using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 A의 순간이동 (기획서 2.5). 방향키로 방향을 잡고 Enter. 미니게임 없이 바로 발동하고 마나를 쓴다.
    // 방향키를 안 누르고 있으면 바라보는 방향으로. 벽/바닥을 뚫지 않도록 그 앞에서 멈춘다.
    [RequireComponent(typeof(PlayerControlState))]
    [RequireComponent(typeof(ManaSystem))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class TeleportSkill : MonoBehaviour
    {
        [SerializeField] private float manaCost = 20f;
        [SerializeField] private float distance = 4f;
        [SerializeField] private float invulnerableTime = 0.15f;

        private const float WallSkin = 0.05f;

        private PlayerControlState control;
        private ManaSystem mana;
        private Rigidbody2D rb;
        private Collider2D body;
        private HealthSystem health;
        private HitStun hitStun;
        private PlayerMovement movement;
        private float invulnerableUntil;
        private bool grantedInvulnerability;
        private readonly List<RaycastHit2D> castResults = new List<RaycastHit2D>();

        public float ManaCost => manaCost;

        private void Awake()
        {
            control = GetComponent<PlayerControlState>();
            mana = GetComponent<ManaSystem>();
            rb = GetComponent<Rigidbody2D>();
            body = GetComponent<Collider2D>();
            health = GetComponent<HealthSystem>();
            hitStun = GetComponent<HitStun>();
            movement = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            if (grantedInvulnerability && Time.time >= invulnerableUntil)
            {
                grantedInvulnerability = false;
                if (!movement.IsDashing && (hitStun == null || !hitStun.IsStunned)) health.Invulnerable = false;
            }

            if (!RoleInput.TeleportDown(control)) return;
            if (health.IsDead || movement.IsDashing || (hitStun != null && hitStun.IsStunned)) return;

            var direction = RoleInput.TeleportDirection();
            if (direction == Vector2.zero) direction = new Vector2(movement.Facing, 0f);

            if (!mana.TryConsume(manaCost, ManaUser.PlayerA))
            {
                SkillMessages.Post(ManaUser.PlayerA, $"마나 부족: 순간이동 (필요 {manaCost:0})", true);
                return;
            }

            Teleport(direction);
        }

        private void Teleport(Vector2 direction)
        {
            float travel = distance;
            if (body != null)
            {
                var filter = new ContactFilter2D();
                int ground = LayerMask.NameToLayer("Ground");
                filter.SetLayerMask(ground >= 0 ? (LayerMask)(1 << ground) : (LayerMask)1);
                filter.useTriggers = false;

                body.Cast(direction, filter, castResults, distance);
                foreach (var hit in castResults)
                {
                    // 서 있는 바닥처럼 진행 방향을 막지 않는 면(옆으로 스치는 면)은 무시한다.
                    if (Vector2.Dot(hit.normal, direction) > -0.5f) continue;
                    travel = Mathf.Min(travel, Mathf.Max(hit.distance - WallSkin, 0f));
                }
            }

            Vector2 from = rb.position;
            Vector2 to = from + direction * travel;

            EffectLibrary.PlayOneShot(from, EffectLibrary.TeleportFrames, 20f, new Color(0.8f, 0.6f, 1f, 0.7f), 1.2f);
            rb.position = to;
            transform.position = to;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, 0f));
            EffectLibrary.PlayOneShot(to, EffectLibrary.TeleportFrames, 20f, new Color(0.8f, 0.6f, 1f, 0.7f), 1.2f);

            if (direction.x != 0f) transform.localScale = new Vector3(Mathf.Sign(direction.x), 1f, 1f);

            health.Invulnerable = true;
            grantedInvulnerability = true;
            invulnerableUntil = Time.time + invulnerableTime;

            SkillMessages.Post(ManaUser.PlayerA, "순간이동");
        }
    }
}
