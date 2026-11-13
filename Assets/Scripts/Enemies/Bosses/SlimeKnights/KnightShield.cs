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
        private const float BubbleSize = 1.6f;   // 그림이 없을 때 대체 원형 크기 (그림이 있으면 그림 크기 그대로, 깨짐 그림도 같은 1배)
        private const float BubbleHeight = 0.45f;
        private static readonly Color BubbleColor = new Color(0.55f, 0.9f, 1f, 0.7f);
        private static readonly Color WarnColor = new Color(0.5f, 0.95f, 1f);
        // 연출 (시간·수치는 그대로, 그림·소리만)
        private const float PopTime = 0.18f;      // 방패가 톡 부풀어 나오는 시간
        private const float RippleTime = 0.2f;    // 막힌 공격에 출렁이는 시간
        private const float BlockSfxGap = 0.12f;  // 연타에 막힘 소리가 겹치지 않게
        private const float BlinkTime = 0.6f;     // 방패가 내려가기 직전 깜빡이는 시간
        private const float ShrinkTime = 0.2f;    // 방패를 내릴 때 쏙 줄어드는 시간
        private const float MoteRadius = 0.8f;    // 약점 알갱이가 도는 반지름
        private const float MoteSize = 0.2f;
        private const float MoteSpin = 2.6f;      // 초당 회전(라디안)

        private Vector3 bubbleScale = Vector3.one;
        private float raisedAt = -100f;
        private float blockedAt = -100f;
        private float blockSfxAt = -100f;
        private float shrinkAt = -100f;
        private bool shrinking;
        private SpriteRenderer[] motes;

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

            Ripple();
            return damage * BlockRatio;
        }

        // 막힌 공격: 방패가 하얗게 출렁이고 둔한 "퉁" 소리
        private void Ripple()
        {
            blockedAt = Time.time;
            if (Time.time - blockSfxAt < BlockSfxGap) return;
            blockSfxAt = Time.time;
            GameAudio.PlaySfx("ShieldBlock", 0.6f);
        }

        private void Raise()
        {
            raised = true;
            lowerTime = Time.time + RaiseTime;
            slime.Suspended = true;
            if (stun != null) stun.Immune = true;
            if (knight == null) knight = GetComponent<SlimeKnight>();
            if (knight != null) knight.Flash(WarnColor, 0.3f);

            if (bubble == null)
            {
                bubble = EffectLibrary.CreateVisual(transform, "KnightShield", EffectLibrary.ShieldFrames, 10f, true, BubbleColor, BubbleSize, 3);
                bubble.transform.localPosition = new Vector3(0f, BubbleHeight, 0f);
                bubbleScale = bubble.transform.localScale;
            }

            bubble.color = BubbleColor;
            bubble.transform.localScale = Vector3.zero;
            bubble.gameObject.SetActive(true);
            raisedAt = Time.time;
            blockedAt = -100f;
            shrinking = false;
            ShowMotes(true);
            GameAudio.PlaySfx("ShieldUp", 0.7f);
        }

        // 방패 그림: 톡 부풀기 → 막히면 출렁 → 끝나기 직전 깜빡 → 내리면 쏙 줄어듦
        private void LateUpdate()
        {
            if (bubble == null) return;
            if (shrinking)
            {
                float s = 1f - (Time.time - shrinkAt) / ShrinkTime;
                if (s <= 0f)
                {
                    shrinking = false;
                    bubble.gameObject.SetActive(false);
                    ShowMotes(false);
                    return;
                }

                bubble.transform.localScale = bubbleScale * s;
                bubble.color = WithAlpha(BubbleColor, BubbleColor.a * s);
                UpdateMotes(s);
                return;
            }

            if (!raised) return;

            float pop = Mathf.Clamp01((Time.time - raisedAt) / PopTime);
            float scale = EaseOutBack(pop);
            float ripple = 1f - Mathf.Clamp01((Time.time - blockedAt) / RippleTime);
            scale *= 1f + 0.12f * ripple * Mathf.Sin(ripple * Mathf.PI);
            bubble.transform.localScale = bubbleScale * scale;

            var color = Color.Lerp(BubbleColor, new Color(1f, 1f, 1f, 0.9f), ripple * 0.6f);
            float remain = lowerTime - Time.time;
            if (remain < BlinkTime && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f) color.a *= 0.35f;  // 곧 풀린다
            bubble.color = color;
            UpdateMotes(pop);
        }

        private static float EaseOutBack(float t)
        {
            const float c = 1.7f;
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // 약점 힌트: 바람(초록)·땅(갈색) 알갱이 두 개가 방패 둘레를 돈다. 글자 대신 색으로 "이 속성으로 깨라"를 보여 준다
        private void ShowMotes(bool show)
        {
            if (motes == null)
            {
                if (!show) return;
                motes = new SpriteRenderer[2];
                var elements = new[] { Element.Wind, Element.Earth };
                for (int i = 0; i < motes.Length; i++)
                {
                    var go = new GameObject("WeakMote");
                    go.transform.SetParent(transform, false);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = SolidSpriteFactory.CreateCircle();
                    r.color = ElementInfo.Color(elements[i]);
                    r.sortingOrder = 4;
                    motes[i] = r;
                }
            }

            foreach (var mote in motes)
                if (mote != null) mote.gameObject.SetActive(show);
        }

        private void UpdateMotes(float strength)
        {
            if (motes == null) return;
            float angle = (Time.time - raisedAt) * MoteSpin;
            for (int i = 0; i < motes.Length; i++)
            {
                var mote = motes[i];
                if (mote == null) continue;
                float a = angle + i * Mathf.PI;
                // 납작한 타원 궤도: 앞(아래쪽)에 올 때 조금 크게
                float depth = 0.85f + 0.15f * -Mathf.Sin(a);
                mote.transform.localPosition = new Vector3(Mathf.Cos(a) * MoteRadius, BubbleHeight + Mathf.Sin(a) * MoteRadius * 0.45f, 0f);
                float size = MoteSize * depth * strength * (1f + 0.15f * Mathf.Sin(Time.time * 9f + i));
                mote.transform.localScale = new Vector3(size, size, 1f);
                mote.sortingOrder = Mathf.Sin(a) < 0f ? 4 : 2;  // 앞쪽은 방패 위, 뒤쪽은 방패 뒤
            }
        }

        private void Lower(bool broken)
        {
            raised = false;
            slime.Suspended = false;
            if (stun != null) stun.Immune = false;
            if (bubble != null)
            {
                if (broken || !bubble.gameObject.activeSelf)
                {
                    shrinking = false;
                    bubble.gameObject.SetActive(false);
                    ShowMotes(false);
                }
                else
                {
                    // 시간이 다 되어 내릴 때는 쏙 줄어들며 사라지고 작은 "뽁"
                    shrinking = true;
                    shrinkAt = Time.time;
                    GameAudio.PlaySfxPitched("ShieldUp", 0.7f, 0.4f);
                }
            }

            nextTime = Time.time + Cooldown / Mathf.Max(0.1f, stats.AttackSpeedMultiplier);
            if (broken)
            {
                DamageNumbers.ShowText(transform.position + Vector3.up * 1.5f, "방패 깨짐!", new Color(1f, 0.85f, 0.3f), 0.9f);
                // 거품이 유리처럼 조각나 흩어진다 (방패 그림과 같은 중심·크기), 기사는 잠깐 움찔
                var shatter = EffectLibrary.LoadRuntimeFrames("Effects/ShieldShatter");
                var center = transform.position + Vector3.up * BubbleHeight;
                if (shatter != null) EffectLibrary.PlayOneShot(center, shatter, 20f, Color.clear, 0f, false, 1f, sortingOrder: 6);
                else EffectLibrary.PlayHit(Element.Water, center, 1.2f);
                if (knight != null) knight.Flash(WarnColor, 0.15f);
                EnemySquash.For(gameObject).Land();
                GameAudio.PlaySfx("Hit_Wind", 0.7f);
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
