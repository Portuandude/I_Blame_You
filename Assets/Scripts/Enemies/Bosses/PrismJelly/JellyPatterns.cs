using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 프리즘 젤리 패턴 (Docs/MidBossDraft.md 후보 D 2~4번, 수치는 PrismJellyRules).
    // - 출렁 착지: 웅크렸다(예고) 높이 뛰고, 내려앉는 순간 좌우로 바닥 충격파 (JellyShockwave, 점프·위로 순간이동으로 피함)
    // - 젤리 방울: 지금 색 방울 4~6개를 포물선으로 흩뿌린다. 떨어진 자리에 2초 웅덩이 (JellyPuddle)
    //   출렁 착지와 젤리 방울을 PatternInterval마다 번갈아 쓴다. 평소 도약 덮치기(SlimeController)는 그 사이에 그대로.
    // - 프리즘 광선: 체력 50% 이하에서 BeamInterval마다 (다른 패턴과 따로). 플레이어 높이를 묶어 예고 뒤 방을 가로지른다 (PrismBeam)
    // 맞아서 경직되면 예고 중인 착지는 끊긴다 (기사 돌진과 같은 규칙). 광선은 이미 쏜 것이라 끊기지 않는다.
    [RequireComponent(typeof(SlimeController))]
    public class JellyPatterns : MonoBehaviour
    {
        private const float DetectRange = 16f;
        private const float AirborneTimeout = 2.5f;
        private const float DropletSpeedMin = 4f;
        private const float DropletSpeedMax = 8f;

        private enum Phase { None, SlamWindup, SlamAir, DropletWindup }

        private SlimeController slime;
        private PrismJelly jelly;
        private Rigidbody2D rb;
        private HealthSystem health;
        private HitStun stun;
        private Collider2D body;
        private Transform player;
        private Phase phase;
        private float phaseEnd;
        private bool leftGround;
        private bool falling; // 정점을 지나 내려오기 시작했다 (정점의 vy≈0을 착지로 잘못 보지 않게)
        private float nextPattern;
        private float nextBeam;
        private bool beamAnnounced;
        private PrismJellyRules.Pattern last = PrismJellyRules.Pattern.Droplets; // 첫 패턴은 출렁 착지

        private void Awake()
        {
            slime = GetComponent<SlimeController>();
            jelly = GetComponent<PrismJelly>();
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<HealthSystem>();
            stun = GetComponent<HitStun>();
            body = GetComponent<Collider2D>();
        }

        private void Start()
        {
            nextPattern = Time.time + PrismJellyRules.FirstPatternDelay;
        }

        private Element Tone => jelly != null ? jelly.CurrentColor : Element.Neutral;

        private void Update()
        {
            if (health.IsDead)
            {
                if (phase != Phase.None) End();
                return;
            }

            UpdateBeam();

            if ((phase == Phase.SlamWindup || phase == Phase.DropletWindup) && stun != null && stun.IsStunned)
            {
                End();
                return;
            }

            switch (phase)
            {
                case Phase.None:
                    if (Time.time >= nextPattern && !slime.Attacking && (stun == null || !stun.IsStunned) && Grounded() && FindPlayer())
                        Begin(PrismJellyRules.NextPattern(last));
                    break;
                case Phase.SlamWindup:
                    if (Time.time >= phaseEnd)
                    {
                        phase = Phase.SlamAir;
                        phaseEnd = Time.time + AirborneTimeout;
                        leftGround = false;
                        falling = false;
                        rb.linearVelocity = new Vector2(0f, PrismJellyRules.SlamJumpSpeed);
                        EnemySquash.For(gameObject).Release();
                        GameAudio.PlaySfx("Jump", 0.6f);
                    }
                    break;
                case Phase.SlamAir:
                    if (!leftGround && rb.linearVelocity.y > 0.5f) leftGround = true;
                    if (leftGround && rb.linearVelocity.y < -0.5f) falling = true;
                    if (falling && rb.linearVelocity.y <= 0.1f && OnGround()) Land();
                    else if (Time.time >= phaseEnd) End();
                    break;
                case Phase.DropletWindup:
                    if (Time.time >= phaseEnd)
                    {
                        Scatter();
                        phase = Phase.None; // 끝까지 갔으니 웅크림 취소(Cancel) 없이
                        End();
                    }
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (phase == Phase.SlamWindup || phase == Phase.DropletWindup)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            else if (phase == Phase.SlamAir && player != null)
            {
                // 공중에서 플레이어 쪽으로 천천히 따라간다 (덮치려고)
                float dx = player.position.x - transform.position.x;
                rb.linearVelocity = new Vector2(Mathf.Clamp(dx, -2.5f, 2.5f), rb.linearVelocity.y);
            }
        }

        private void Begin(PrismJellyRules.Pattern pattern)
        {
            last = pattern;
            slime.Suspended = true;
            bool slam = pattern == PrismJellyRules.Pattern.Slam;
            float windup = slam ? PrismJellyRules.SlamWindup : PrismJellyRules.DropletWindup;
            phase = slam ? Phase.SlamWindup : Phase.DropletWindup;
            phaseEnd = Time.time + windup;
            EnemySquash.For(gameObject).Windup(windup);
            EnemyTelegraph.Windup(slime, body.bounds);
        }

        private void Land()
        {
            var bounds = body.bounds;
            var feet = new Vector2(bounds.center.x, bounds.min.y);
            EnemySquash.For(gameObject).Land();
            VfxPlayer.Shockwave(feet, 1.2f);
            GameAudio.PlaySfx("Land", 0.9f);
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<IBlameYou.Core.CameraFollow>() : null;
            if (follow != null) follow.Shake(0.2f, 0.15f);
            JellyShockwave.Launch(gameObject, new Vector2(bounds.min.x, feet.y), -1f, Tone);
            JellyShockwave.Launch(gameObject, new Vector2(bounds.max.x, feet.y), 1f, Tone);
            phase = Phase.None;
            End();
        }

        // 지금 색 방울을 위쪽 부채꼴로 고르게 흩뿌린다 (플레이어 쪽으로 조금 더)
        private void Scatter()
        {
            EnemySquash.For(gameObject).Release();
            int count = PrismJellyRules.DropletCount(Random.value);
            float toward = player != null ? Mathf.Sign(player.position.x - transform.position.x) : 1f;
            var from = (Vector2)body.bounds.center + Vector2.up * body.bounds.extents.y;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float vx = Mathf.Lerp(-DropletSpeedMax, DropletSpeedMax, t) + toward * 1.5f;
                float vy = Mathf.Lerp(DropletSpeedMin, DropletSpeedMax, 1f - Mathf.Abs(t - 0.5f) * 2f) + Random.Range(0f, 1.5f);
                JellyDroplet.Launch(gameObject, from, new Vector2(vx, vy), Tone);
            }
            GameAudio.PlaySfx("Hit_Water", 0.6f);
        }

        private void UpdateBeam()
        {
            if (!PrismJellyRules.BeamUnlocked(health.Current, health.Max)) return;
            if (!beamAnnounced)
            {
                beamAnnounced = true;
                nextBeam = Time.time + 2f;
                StageEvents.PostNotice("프리즘 젤리가 빛을 모은다! 광선은 층을 바꿔 피하자");
            }
            if (Time.time < nextBeam || !FindPlayer()) return;
            nextBeam = Time.time + PrismJellyRules.BeamInterval;
            var playerBody = player.GetComponent<Collider2D>();
            float height = playerBody != null ? playerBody.bounds.center.y : player.position.y + 0.5f;
            PrismBeam.Fire(gameObject, transform.position.x, height, Tone);
        }

        private void End()
        {
            if (phase == Phase.SlamWindup || phase == Phase.DropletWindup) EnemySquash.For(gameObject).Cancel();
            phase = Phase.None;
            slime.Suspended = false;
            nextPattern = Time.time + PrismJellyRules.PatternInterval;
        }

        private bool FindPlayer()
        {
            if (player == null)
            {
                var movement = FindFirstObjectByType<PlayerMovement>();
                if (movement == null) return false;
                player = movement.transform;
            }
            var playerHealth = player.GetComponent<HealthSystem>();
            if (playerHealth != null && playerHealth.IsDead) return false;
            return Vector2.Distance(player.position, transform.position) <= DetectRange;
        }

        private bool Grounded() => Mathf.Abs(rb.linearVelocity.y) < 0.1f;

        // 발밑 바로 아래에 땅이 있는지 (착지 판정)
        private bool OnGround()
        {
            var b = body.bounds;
            return Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 0.15f, EnemySpawner.GroundMask()).collider != null;
        }

        private void OnDisable()
        {
            if (phase != Phase.None) End();
        }

        private void OnDestroy()
        {
            if (slime != null) slime.Suspended = false;
        }
    }
}
