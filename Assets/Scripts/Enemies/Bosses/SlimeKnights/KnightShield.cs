using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 물 기사의 방패: 플레이어가 가까우면 멈춰 서서 물방울 방패를 든다. 든 동안 받는 피해 75% 감소, 경직 안 됨.
    // 약점 속성(바람·땅) 마법에 맞으면 방패가 깨지고 그 피해는 그대로 들어간다 → B가 약점을 치고, A는 그 틈에 몰아친다.
    // 광폭화하면 방패를 더 자주 든다.
    [RequireComponent(typeof(SlimeController))]
    public class KnightShield : MonoBehaviour, IDamageModifier
    {
        private const float FirstDelay = 1.5f;
        private const float Cooldown = 5f;
        private const float RaiseTime = 3f;
        private const float Range = 9f;
        private const float BlockRatio = 0.25f;
        private static readonly Color BubbleColor = new Color(0.55f, 0.9f, 1f, 0.7f);
        private static readonly Color WarnColor = new Color(0.5f, 0.95f, 1f);

        private SlimeController slime;
        private SlimeKnight knight;
        private Rigidbody2D rb;
        private HealthSystem health;
        private HitStun stun;
        private CombatStats stats;
        private SpriteRenderer bubble;
        private Transform player;
        private bool raised;
        private float lowerTime;
        private float nextTime;

        public bool Raised => raised;

        private void Awake()
        {
            slime = GetComponent<SlimeController>();
            knight = GetComponent<SlimeKnight>();
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<HealthSystem>();
            stun = GetComponent<HitStun>();
            stats = GetComponent<CombatStats>();
        }

        private void Start()
        {
            nextTime = Time.time + FirstDelay;
        }

        private void Update()
        {
            if (health.IsDead)
            {
                if (raised) Lower(false);
                return;
            }

            if (raised)
            {
                if (Time.time >= lowerTime) Lower(false);
                return;
            }

            if (Time.time >= nextTime && !slime.Attacking && (stun == null || !stun.IsStunned) && Mathf.Abs(rb.linearVelocity.y) < 0.1f && PlayerNear())
                Raise();
        }

        private void FixedUpdate()
        {
            if (raised) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        // DamageCalculator가 피해를 넣기 직전에 부른다.
        public float Modify(float damage, DamageKind kind, Element element)
        {
            if (!raised) return damage;
            if (kind == DamageKind.Magic && (element == Element.Wind || element == Element.Earth))
            {
                Lower(true);
                return damage;
            }

            return damage * BlockRatio;
        }

        private void Raise()
        {
            raised = true;
            lowerTime = Time.time + RaiseTime;
            slime.Suspended = true;
            if (stun != null) stun.Immune = true;
            if (knight != null) knight.Flash(WarnColor, 0.3f);

            if (bubble == null)
            {
                bubble = EffectLibrary.CreateVisual(transform, "KnightShield", EffectLibrary.ShieldFrames, 10f, true, BubbleColor, 1.6f, 3);
                bubble.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            }

            bubble.color = BubbleColor;
            bubble.gameObject.SetActive(true);
        }

        private void Lower(bool broken)
        {
            raised = false;
            slime.Suspended = false;
            if (stun != null) stun.Immune = false;
            if (bubble != null) bubble.gameObject.SetActive(false);
            nextTime = Time.time + Cooldown / Mathf.Max(0.1f, stats.AttackSpeedMultiplier);
            if (broken)
            {
                DamageNumbers.ShowText(transform.position + Vector3.up * 1.5f, "방패 깨짐!", new Color(1f, 0.85f, 0.3f), 0.9f);
                EffectLibrary.PlayHit(Element.Water, transform.position + Vector3.up * 0.45f, 1.2f);
            }
        }

        private bool PlayerNear()
        {
            if (player == null)
            {
                var movement = FindFirstObjectByType<PlayerMovement>();
                if (movement == null) return false;
                player = movement.transform;
            }

            var playerHealth = player.GetComponent<HealthSystem>();
            if (playerHealth != null && playerHealth.IsDead) return false;
            return Vector2.Distance(player.position, transform.position) <= Range;
        }

        private void OnDestroy()
        {
            if (slime != null) slime.Suspended = false;
        }
    }
}
