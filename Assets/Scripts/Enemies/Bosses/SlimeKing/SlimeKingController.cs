using System.Collections;
using System.Collections.Generic;
using IBlameYou.Core;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 스테이지 1 보스 슬라임 킹 (2026-10-18 사용자 지시). 패턴 6개, 페이즈 2개.
    //   패턴 1 소환 (마나): 어미 슬라임 3체. 60초 안에 무리를 다 잡지 못하면 돌아와 합체하고 킹이 체력을 회복한다 (SlimeBrood).
    //   패턴 2 내려찍기 (기력): 높이 떠올라 플레이어 머리 위를 따라다니다가 낙하. 떨어질 자리에 그림자가 생긴다.
    //   패턴 3 촉수 (기력): 촉수 4가닥을 무작위로 뻗는다. 닿으면 2초 둔화, 5초 안에 멀어지지 못하면 그랩 (SlimeKingTentacle).
    //   패턴 4 박치기: 일반 슬라임처럼 웅크렸다가 도약해 덮친다. 자원이 없을 때도 쓸 수 있다.
    //   패턴 5 분열: 체력이 절반이 되면 2기로 나뉘어 남은 체력을 반씩 가진다. 여기서 2페이즈가 시작된다.
    //   패턴 6 발광 (체력): 2페이즈 전용. 체력을 조금 깎고 빛나며 구역 안 무작위 위치를 빠르게 연속으로 내려찍는다.
    // 패턴 중에는 경직되지 않는다 (슈퍼아머). 패턴 사이에는 플레이어 쪽으로 천천히 걸어온다.
    public class SlimeKingController : EnemyController
    {
        public const string DisplayName = "슬라임 킹";

        private enum Motion { Hold, Walk, Script }
        private enum Pattern { Summon, Slam, Tentacles, Headbutt, Glow }

        private static readonly int IsAttackingParam = Animator.StringToHash("IsAttacking");
        private static readonly Color BaseTint = new Color(0.6f, 0.35f, 0.95f);
        private static readonly Color GlowTint = new Color(1f, 0.85f, 0.35f);
        private static readonly Color TelegraphPurple = new Color(0.78f, 0.45f, 1f); // 촉수 점선·소환 자리 (킹 몸 보라)

        // 자원
        private const float ManaRegen = 4f;     // 초당
        private const float StaminaRegen = 10f; // 초당

        // 패턴 1 소환
        private const float SummonMana = 60f;
        private const float SummonCooldown = 25f;
        private const float BroodDuration = 60f;
        private const int BroodSize = 3;

        // 패턴 2 내려찍기
        private const float SlamStamina = 30f;
        private const float SlamRise = 6.5f;
        private const float SlamRiseTime = 0.5f;
        private const float SlamHover = 0.9f;
        private const float SlamTrackSpeed = 9f;
        private const float SlamDropSpeed = 28f;
        private const float SlamWidth = 4.5f;
        private const float SlamHeight = 1.5f;
        private const float SlamMultiplier = 1.2f;

        // 패턴 3 촉수
        private const float TentacleStamina = 25f;
        private const float TentacleRange = 9f;
        private const float TentacleSpread = 35f; // 첫 가닥은 플레이어를 정확히, 나머지는 ±이 각도 안에서 무작위
        private const int TentacleCount = 4;

        // 패턴 4 박치기
        private const float HeadbuttWindup = 0.375f;
        private const float HeadbuttAir = 0.6f;
        private const float HeadbuttMaxSpeed = 10f;
        private const float HeadbuttRecover = 0.4f;

        // 패턴 5 분열
        private const float SplitRatio = 0.5f;
        private const float SplitShrink = 0.8f;
        private const float SplitInvulnerable = 1.2f;

        // 패턴 6 발광
        private const float GlowHealthRatio = 0.05f;
        private const float GlowCooldown = 12f;
        private const int GlowSlams = 5;
        private const float GlowSlamMultiplier = 0.8f;

        private const float IntroDelay = 1.5f;
        private const float ArenaEdgeMargin = 2.5f;

        private static readonly List<SlimeKingController> kings = new List<SlimeKingController>();

        private LayerMask ground;
        private ElementRecolor recolor;
        private Transform visual;
        private Vector3 visualBaseScale;
        private float baseGravity;
        private Rect? arena;

        private float mana;
        private float stamina;
        private int phase = 1;
        private const float SplitMusicPitch = 1.06f;
        private bool splitDone;
        private bool splitPending;
        private float startDelay = IntroDelay;

        private Motion motion = Motion.Hold;
        private float walkDirection;
        private bool contact = true;
        private float facing = -1f;
        private float size = 1f;
        private int glowSlamIndex = -1; // 발광 연속 내려찍기 회차 (착지 연출 음 높이), 아니면 -1
        private float lastSummon = -999f;
        private float lastGlow = -999f;

        private readonly List<SlimeKingTentacle> tentacles = new List<SlimeKingTentacle>();
        private GameObject shadow;

        // 패턴 예고 (그림만, 패턴 시간·피해·범위는 그대로). Interrupt·OnDestroy에서 ClearTelegraphs로 치운다.
        private LandingMarkFx slamMark;      // 내려찍기 피해 폭
        private LandingMarkFx headbuttMark;  // 박치기로 뛰어 내릴 자리
        private AimLineFx tentacleLine;      // 첫 촉수가 나갈 쪽
        private readonly List<LandingMarkFx> summonMarks = new List<LandingMarkFx>(); // 어미 슬라임이 나올 자리
        private bool grabbing;
        private PlayerControlState grabbedControl;
        private HealthSystem grabbedHealth;
        private bool grabbedWasLocked;

        public bool Dead => IsDead;
        public Vector2 TentacleOrigin => BodyBounds.center;
        public float Mana => mana;
        public float Stamina => stamina;

        protected override bool ContactDamageActive => contact;
        protected override bool ShowsHealthBar => false; // 화면 위 보스 체력바를 쓴다

        public static SlimeKingController Nearest(Vector3 position)
        {
            SlimeKingController best = null;
            float bestDistance = float.MaxValue;
            foreach (var king in kings)
            {
                if (king == null || king.IsDead) continue;
                float distance = (king.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = king;
                }
            }

            return best;
        }

        public void Configure(LayerMask groundMask, ElementRecolor bodyRecolor)
        {
            ground = groundMask;
            recolor = bodyRecolor;
        }

        protected override void Awake()
        {
            base.Awake();
            kings.Add(this);
            GetComponent<HitStun>().Immune = true;
            baseGravity = Rb.gravityScale;
            mana = Stats.Stats.maxMana;
            stamina = Stats.Stats.maxStamina;
            visual = transform.Find("Visual");
            if (visual != null) visualBaseScale = visual.localScale;
            Health.HealthChanged += OnKingHealthChanged;
        }

        private void Start()
        {
            BossEvents.Register(DisplayName, Health);
            var zone = GetComponentInParent<ArenaZone>();
            if (zone != null) arena = zone.Area;
            SetFacing(facing);
            if (!splitDone) StageEvents.PostNotice($"보스: {DisplayName}");
            StartCoroutine(Brain(startDelay));
        }

        // 분열로 새로 생긴 쪽 몸. Start 전에 불러야 한다.
        public void BecomeTwin(float current, float max, float experience, float bodySize, float direction, float delay)
        {
            splitDone = true; // SetValues가 다시 분열을 부르지 않게 먼저
            phase = 2;
            lastSummon = Time.time;
            Health.SetValues(current, max);
            Stats.Stats.experience = experience;
            size = bodySize;
            facing = direction;
            SetFacing(direction);
            startDelay = delay;
            motion = Motion.Script;
            Rb.linearVelocity = new Vector2(direction * 5f, 7f);
            StartCoroutine(Invulnerability(delay));
        }

        protected override void OnAliveUpdate()
        {
            mana = Mathf.Min(Stats.Stats.maxMana, mana + ManaRegen * Time.deltaTime);
            stamina = Mathf.Min(Stats.Stats.maxStamina, stamina + StaminaRegen * Time.deltaTime);

            if (splitPending) DoSplit();
        }

        protected override void Move()
        {
            switch (motion)
            {
                case Motion.Walk:
                    float speed = StatUnits.MoveSpeedToUnits(Stats.Stats.moveSpeed) * Stats.TotalMoveMultiplier;
                    Rb.linearVelocity = new Vector2(walkDirection * speed, Rb.linearVelocity.y);
                    break;
                case Motion.Hold:
                    Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
                    break;
                // Script: 패턴 코루틴이 직접 움직인다.
            }
        }

        // ---- 두뇌: 걷기 → 패턴 하나 → 걷기 ... ----

        private IEnumerator Brain(float delay)
        {
            yield return new WaitForSeconds(delay);
            motion = Motion.Hold;

            while (!IsDead)
            {
                float rest = phase == 1 ? Random.Range(1.0f, 1.8f) : Random.Range(0.8f, 1.3f);
                yield return Walk(rest);

                var target = FindTarget();
                if (target == null)
                {
                    yield return null;
                    continue;
                }

                switch (Choose(target))
                {
                    case Pattern.Summon: yield return Summon(); break;
                    case Pattern.Slam:
                        stamina -= SlamStamina;
                        yield return Slam(SlamRise, SlamRiseTime, SlamHover, null, SlamMultiplier);
                        break;
                    case Pattern.Tentacles: yield return Tentacles(); break;
                    case Pattern.Glow: yield return Glow(); break;
                    default: yield return Headbutt(); break;
                }
            }
        }

        private IEnumerator Walk(float duration)
        {
            motion = Motion.Walk;
            float end = Time.time + duration;
            while (Time.time < end)
            {
                var target = FindTarget();
                walkDirection = 0f;
                if (target != null)
                {
                    float dx = target.position.x - transform.position.x;
                    if (Mathf.Abs(dx) > 1.5f) walkDirection = Mathf.Sign(dx);
                    if (walkDirection != 0f) SetFacing(walkDirection);
                    if (walkDirection != 0f && LedgeAhead(walkDirection, ground)) walkDirection = 0f;
                }

                yield return null;
            }

            walkDirection = 0f;
            motion = Motion.Hold;
        }

        private Pattern Choose(Transform target)
        {
            float dx = Mathf.Abs(target.position.x - transform.position.x);
            float distance = Vector2.Distance(TentacleOrigin, target.position);

            var options = new List<KeyValuePair<Pattern, float>>();
            if (mana >= SummonMana && SlimeBrood.Active == null && Time.time - lastSummon >= SummonCooldown)
                options.Add(new KeyValuePair<Pattern, float>(Pattern.Summon, phase == 1 ? 3f : 2f));
            if (stamina >= SlamStamina)
                options.Add(new KeyValuePair<Pattern, float>(Pattern.Slam, dx > 5f ? 3f : 2f));
            if (stamina >= TentacleStamina && distance <= TentacleRange + 1f)
                options.Add(new KeyValuePair<Pattern, float>(Pattern.Tentacles, 2f));
            if (phase == 2 && Time.time - lastGlow >= GlowCooldown && Health.Current > Health.Max * 0.1f)
                options.Add(new KeyValuePair<Pattern, float>(Pattern.Glow, 2f));
            options.Add(new KeyValuePair<Pattern, float>(Pattern.Headbutt, dx <= 6f ? 3f : 1f));

            float total = 0f;
            foreach (var option in options) total += option.Value;
            float roll = Random.value * total;
            foreach (var option in options)
            {
                roll -= option.Value;
                if (roll <= 0f) return option.Key;
            }

            return Pattern.Headbutt;
        }

        // ---- 패턴 1: 소환 ----

        private IEnumerator Summon()
        {
            mana -= SummonMana;
            lastSummon = Time.time;
            motion = Motion.Hold;
            Say("어미 슬라임 소환!", new Color(0.8f, 0.55f, 1f));

            // 나올 자리를 먼저 정해 웅크리는 동안 보라 타원으로 보여 준다 (나오면 그 자리 타원은 사라진다).
            var spawnXs = new float[BroodSize];
            ClearSummonMarks();
            for (int i = 0; i < BroodSize; i++)
            {
                spawnXs[i] = ClampX(transform.position.x + (i - (BroodSize - 1) / 2f) * 4.5f + Random.Range(-0.5f, 0.5f));
                var mark = LandingMarkFx.Create(gameObject, 1.6f, TelegraphPurple);
                PlaceMark(mark, spawnXs[i], transform.position.y + 1.5f, 0f);
                summonMarks.Add(mark);
            }

            yield return Squash(0.6f, 0.8f, k =>
            {
                for (int i = 0; i < summonMarks.Count; i++) PlaceMark(summonMarks[i], spawnXs[i], transform.position.y + 1.5f, k);
            });

            var brood = SlimeBrood.Create(transform.parent, BroodDuration);
            for (int i = 0; i < BroodSize; i++)
            {
                float x = spawnXs[i];
                if (i < summonMarks.Count && summonMarks[i] != null) summonMarks[i].Remove();
                var position = new Vector3(x, transform.position.y + 1.5f, 0f);
                EffectLibrary.PlayOneShot(position + Vector3.up * 0.6f, EffectLibrary.TeleportFrames, 20f, new Color(0.8f, 0.5f, 1f, 0.7f), 1.4f);
                var mother = MotherSlimeSpawner.Spawn(position, EffectLibrary.Config);
                if (mother != null)
                {
                    mother.gameObject.AddComponent<BossSummon>(); // 소환된 적은 골드 없음 (Core/RunGold)
                    brood.RegisterEnemy(mother.GetComponent<HealthSystem>());
                }
                yield return new WaitForSeconds(0.25f);
            }

            ClearSummonMarks();
            GameAudio.PlaySfx("Cast_Earth", 0.7f);
            StageEvents.PostNotice($"{BroodDuration:0}초 안에 슬라임을 모두 처치하라!");
            yield return new WaitForSeconds(0.4f);
        }

        // 소환한 무리가 제한 시간 안에 다 처치되지 않아 돌아왔다.
        public void AbsorbBrood(float healRatio)
        {
            if (IsDead || healRatio <= 0f) return;
            float amount = Health.Max * healRatio;
            Health.Heal(amount);
            DamageNumbers.ShowText(BodyBounds.center + Vector3.up * BodyBounds.extents.y, $"합체! +{amount:0}", new Color(0.5f, 1f, 0.5f), 1.3f);
            EffectLibrary.PlayHit(Element.Water, BodyBounds.center, 2f);
            SlimeBroodFx.Gulp(transform, BodyBounds.center, BodyBounds.size.y); // "꿀꺽" + 초록 고리 + 방울 (그림·소리만)
        }

        // ---- 패턴 2 / 6: 내려찍기 ----
        // targetX가 없으면 떠 있는 동안 플레이어 머리 위를 따라가고, 있으면 그 자리로 미끄러져 간다 (발광).

        private IEnumerator Slam(float rise, float riseTime, float hover, float? targetX, float multiplier)
        {
            contact = false;
            motion = Motion.Script;
            if (!targetX.HasValue) yield return Squash(0.3f, 0.75f);

            Rb.gravityScale = 0f;
            Rb.linearVelocity = Vector2.zero;
            Vector2 from = Rb.position;
            float top = from.y + rise;

            // 피해 폭 예고: 발광처럼 자리가 정해져 있으면 떠오를 때부터, 따라다니는 내려찍기는 떠 있는 동안 그 아래.
            RemoveSlamMark();
            float markWidth = SlamWidth * size;
            if (targetX.HasValue)
            {
                slamMark = LandingMarkFx.Create(gameObject, markWidth);
                PlaceMark(slamMark, targetX.Value, top, 0f);
            }

            for (float t = 0f; t < riseTime; t += Time.fixedDeltaTime)
            {
                float k = t / riseTime;
                Rb.MovePosition(new Vector2(from.x, Mathf.Lerp(from.y, top, 1f - (1f - k) * (1f - k))));
                yield return new WaitForFixedUpdate();
            }

            CreateShadow();
            if (slamMark == null) slamMark = LandingMarkFx.Create(gameObject, markWidth);
            float x = from.x;
            for (float t = 0f; t < hover; t += Time.fixedDeltaTime)
            {
                if (targetX.HasValue)
                {
                    x = Mathf.Lerp(from.x, targetX.Value, Mathf.SmoothStep(0f, 1f, t / (hover * 0.7f)));
                }
                else
                {
                    var target = FindTarget();
                    if (target != null)
                    {
                        x = Mathf.MoveTowards(x, ClampX(target.position.x), SlamTrackSpeed * Time.fixedDeltaTime);
                        SetFacing(target.position.x >= x ? 1f : -1f);
                    }
                }

                Rb.MovePosition(new Vector2(x, top + Mathf.Sin(t * 12f) * 0.08f));
                PlaceShadow(x, GroundBelow(x, top));
                PlaceMark(slamMark, targetX ?? x, top, t / hover); // 떨어질 때가 다가올수록 진해진다
                yield return new WaitForFixedUpdate();
            }

            float landing = GroundBelow(x, top);
            PlaceMark(slamMark, x, top, 1f);
            SlimeKingLandFx.Fall(new Vector3(x, top, 0f));
            float y = top;
            while (true)
            {
                y -= SlamDropSpeed * Time.fixedDeltaTime;
                if (y <= landing) break;
                Rb.MovePosition(new Vector2(x, y));
                yield return new WaitForFixedUpdate();
            }

            Rb.position = new Vector2(x, landing);
            Rb.linearVelocity = Vector2.zero;
            Rb.gravityScale = baseGravity;
            DestroyShadow();
            RemoveSlamMark();
            Impact(x, landing, multiplier, SlamWidth * size);

            yield return Squash(targetX.HasValue ? 0.15f : 0.35f, 0.7f);
            motion = Motion.Hold;
            contact = true;
        }

        private void Impact(float x, float y, float multiplier, float width)
        {
            var struck = new HashSet<HealthSystem>();
            var center = new Vector2(x, y + SlamHeight / 2f);
            foreach (var hit in Physics2D.OverlapBoxAll(center, new Vector2(width, SlamHeight), 0f))
            {
                var target = hit.GetComponentInParent<HealthSystem>();
                if (target == null || target == Health || target.GetComponent<EnemyController>() != null) continue;
                if (struck.Add(target)) StrikePlayer(target, multiplier);
            }

            Shake(0.3f, 0.35f);
            EffectLibrary.PlayHit(Element.Earth, new Vector3(x, y + 0.3f, 0f), 2f * size);
            GameAudio.PlaySfx("Hit_Earth", 0.6f); // 아래 KingSlam과 겹치므로 조금 작게
            SlimeKingLandFx.Slam(new Vector3(x, y, 0f), width, size, glowSlamIndex);
        }

        // ---- 패턴 3: 촉수 ----

        private IEnumerator Tentacles()
        {
            stamina -= TentacleStamina;
            motion = Motion.Hold;
            Say("촉수!", new Color(0.8f, 0.55f, 1f));

            // 웅크리는 동안 첫 촉수가 나갈 쪽(플레이어)으로 보라 점선. 촉수가 나가면 사라진다.
            RemoveTentacleLine();
            tentacleLine = AimLineFx.Create(gameObject, TelegraphPurple);
            yield return Squash(0.5f, 0.9f, k =>
            {
                var aimTarget = FindTarget();
                if (aimTarget != null && tentacleLine != null)
                    tentacleLine.Aim(TentacleOrigin, (Vector2)aimTarget.position + Vector2.up * 0.5f, TentacleRange, ground, k);
            });
            RemoveTentacleLine();

            var target = FindTarget();
            if (target == null) yield break;

            Vector2 aim = (Vector2)target.position + Vector2.up * 0.5f - TentacleOrigin;
            SetFacing(aim.x >= 0f ? 1f : -1f);
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            tentacles.Clear();
            for (int i = 0; i < TentacleCount; i++)
            {
                float angle = (baseAngle + (i == 0 ? 0f : Random.Range(-TentacleSpread, TentacleSpread))) * Mathf.Deg2Rad;
                tentacles.Add(SlimeKingTentacle.Launch(this, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), TentacleRange, ground));
                yield return new WaitForSeconds(0.08f);
            }

            // 붙은 촉수가 끊기거나 그랩이 끝날 때까지 기다린다.
            float timeout = Time.time + 8f;
            while (Time.time < timeout)
            {
                tentacles.RemoveAll(t => t == null);
                if (tentacles.Count == 0 && !grabbing) break;
                yield return null;
            }

            CleanupTentacles();
        }

        public void OnTentacleAttached(SlimeKingTentacle attached)
        {
            foreach (var tentacle in tentacles) if (tentacle != null && tentacle != attached) tentacle.Retract();
        }

        public void OnTentacleReleased(SlimeKingTentacle released) { }

        // 촉수에 5초 동안 붙잡혀 있었다: 킹 앞으로 끌어와 피해를 주고 잠깐 경직시킨다.
        public IEnumerator GrabRoutine(PlayerMovement player, float pullTime, float multiplier, float stunTime)
        {
            // 촉수 둘이 거의 같은 순간 붙으면 그랩이 두 번 시작될 수 있다: 먼저 끝난 쪽의 ReleaseGrab이 grabbedHealth를 비워
            // 남은 쪽이 null을 밟았다 (2026-11-14 플레이테스트 NullReferenceException). 이미 잡고 있으면 새 그랩은 무시하고, 체력은 지역 변수로 든다.
            if (player == null || IsDead || grabbing) yield break;
            var health = player.GetComponent<HealthSystem>();
            if (health == null) yield break;

            grabbing = true;
            grabbedControl = player.GetComponent<PlayerControlState>();
            grabbedHealth = health;
            grabbedWasLocked = grabbedControl != null && grabbedControl.Locked;
            if (grabbedControl != null) grabbedControl.Locked = true;
            var body = player.GetComponent<Rigidbody2D>();

            float side = player.transform.position.x >= transform.position.x ? 1f : -1f;
            SetFacing(side);
            Say("그랩!", new Color(1f, 0.4f, 0.4f));

            Vector2 start = body.position;
            for (float t = 0f; t < pullTime; t += Time.fixedDeltaTime)
            {
                if (player == null || health.IsDead || !grabbing) break;
                var bounds = BodyBounds;
                var end = new Vector2(bounds.center.x + side * (bounds.extents.x + 0.3f), transform.position.y + 0.2f);
                body.position = Vector2.Lerp(start, end, t / pullTime);
                body.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
            }

            if (player != null && !health.IsDead && grabbing)
            {
                StrikePlayer(health, multiplier);
                var stun = player.GetComponent<HitStun>();
                if (stun != null) stun.Apply(stunTime);
                Shake(0.25f, 0.3f);
                GameAudio.PlaySfx("Hit_Earth", 0.7f);
            }

            ReleaseGrab();
        }

        private void ReleaseGrab()
        {
            if (grabbedControl != null && !grabbedWasLocked && grabbedHealth != null && !grabbedHealth.IsDead)
                grabbedControl.Locked = false;
            grabbedControl = null;
            grabbedHealth = null;
            grabbing = false;
        }

        // ---- 패턴 4: 박치기 ----

        private IEnumerator Headbutt()
        {
            contact = false;
            motion = Motion.Hold;
            var target = FindTarget();
            if (target != null) SetFacing(target.position.x >= transform.position.x ? 1f : -1f);
            if (Animator != null) Animator.SetBool(IsAttackingParam, true);

            // 예고: "슉" 소리 + 뛰어 내릴 자리(플레이어 쪽, 실제 점프와 같은 계산)에 붉은 타원. 착지하면 사라진다.
            EnemyTelegraph.Windup(this, BodyBounds);
            RemoveHeadbuttMark();
            headbuttMark = LandingMarkFx.Create(gameObject, BodyBounds.size.x * 1.1f);
            for (float t = 0f; t < HeadbuttWindup; t += Time.deltaTime)
            {
                var aimTarget = FindTarget();
                float aimDx = aimTarget != null ? aimTarget.position.x - transform.position.x : facing * 4f;
                PlaceMark(headbuttMark, transform.position.x + HeadbuttVx(aimDx) * HeadbuttAir, BodyBounds.center.y + 1f, t / HeadbuttWindup);
                yield return null;
            }

            target = FindTarget();
            float dx = target != null ? target.position.x - transform.position.x : facing * 4f;
            float gravity = Mathf.Abs(Physics2D.gravity.y) * Rb.gravityScale;
            float vx = HeadbuttVx(dx);
            if (Mathf.Abs(vx) > 0.01f) SetFacing(Mathf.Sign(vx));
            PlaceMark(headbuttMark, transform.position.x + vx * HeadbuttAir, BodyBounds.center.y + 1f, 1f); // 실제로 뛴 속도로 자리를 굳힌다

            motion = Motion.Script;
            Rb.linearVelocity = new Vector2(vx, gravity * HeadbuttAir / 2f);

            var struck = new HashSet<HealthSystem>();
            for (float t = 0f; t < HeadbuttAir + 0.4f; t += Time.fixedDeltaTime)
            {
                var bounds = BodyBounds;
                foreach (var hit in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f))
                {
                    var health = hit.GetComponentInParent<HealthSystem>();
                    if (health == null || health == Health || health.GetComponent<EnemyController>() != null) continue;
                    if (struck.Add(health)) StrikePlayer(health);
                }

                if (t > 0.15f && Grounded()) break;
                yield return new WaitForFixedUpdate();
            }

            RemoveHeadbuttMark();
            motion = Motion.Hold;
            Shake(0.12f, 0.15f);
            if (Grounded())
            {
                var landBounds = BodyBounds;
                SlimeKingLandFx.Land(new Vector3(landBounds.center.x, landBounds.min.y, 0f), size);
            }
            yield return new WaitForSeconds(HeadbuttRecover);
            if (Animator != null) Animator.SetBool(IsAttackingParam, false);
            contact = true;
        }

        // ---- 패턴 5: 분열 ----

        private void OnKingHealthChanged(float current, float max)
        {
            if (!splitDone && current > 0f && current <= max * SplitRatio) splitPending = true;
        }

        private void DoSplit()
        {
            splitPending = false;
            splitDone = true;
            phase = 2;
            Interrupt();

            float current = Health.Current / 2f;
            float max = Health.Max / 2f;
            Health.SetValues(current, max);
            Stats.Stats.experience *= 0.5f;
            size *= SplitShrink;
            SetFacing(facing);

            float away = -facing;
            var splitCenter = BodyBounds.center; // 분열 연출 자리 (몸이 갈라지기 전 가운데)
            var twin = SlimeKingSpawner.Spawn(transform.position + new Vector3(away * 1.5f, 0.5f, 0f), EffectLibrary.Config,
                entrance: false);
            if (twin != null)
            {
                twin.transform.SetParent(transform.parent, true);
                twin.BecomeTwin(current, max, Stats.Stats.experience, size, away, SplitInvulnerable);
                var group = transform.parent != null ? transform.parent.GetComponentInParent<IEnemyGroup>() : null;
                if (group != null) group.RegisterEnemy(twin.GetComponent<HealthSystem>());
            }

            motion = Motion.Script;
            Rb.linearVelocity = new Vector2(-away * 5f, 7f);
            StartCoroutine(Invulnerability(SplitInvulnerable));
            StartCoroutine(Brain(SplitInvulnerable + 0.3f));

            StageEvents.PostNotice($"{DisplayName}이(가) 둘로 분열했다!");
            if (GameAudio.CurrentMusic == "Boss") GameAudio.SetMusicPitch(SplitMusicPitch); // 2페이즈는 보스 곡도 조금 빠르게 (구역을 돌파해 곡이 바뀌면 원래대로)
            DamageNumbers.ShowText(BodyBounds.center + Vector3.up * 1.5f, "분열!", new Color(0.8f, 0.5f, 1f), 1.5f);
            EffectLibrary.PlayHit(Element.Water, BodyBounds.center, 2.5f);
            Shake(0.3f, 0.3f);

            // 분열 연출 (그림·소리만, 2026-11-13): 짧은 히트스톱 + "뿌직-쪼옥" + 보라 젤리 방울 + 큰 보라 고리,
            // 갈라진 두 몸 사이 젤리 끈이 늘어나다 무적 시간 절반쯤에 끊긴다
            HitStop.Play(SplitHitStop);
            SlimeSplitFx.Burst(splitCenter, SlimeSplitFx.GooColor, 9, 1.4f, 1f);
            VfxPlayer.ManaRing(transform, splitCenter - transform.position, SlimeSplitFx.GooColor, 3f);
            if (twin != null)
            {
                var offset = splitCenter - transform.position;
                SlimeSplitFx.JellyString(transform, offset, twin.transform, offset, SlimeSplitFx.GooColor, 0.5f, SplitInvulnerable * 0.5f);
            }
        }

        private const float SplitHitStop = 0.1f;

        private IEnumerator Invulnerability(float duration)
        {
            Health.Invulnerable = true;
            var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            for (float t = 0f; t < duration; t += 0.1f)
            {
                if (renderer != null) renderer.enabled = !renderer.enabled;
                yield return new WaitForSeconds(0.1f);
            }

            if (renderer != null) renderer.enabled = true;
            Health.Invulnerable = false;
        }

        // ---- 패턴 6: 발광 ----

        private IEnumerator Glow()
        {
            lastGlow = Time.time;
            Health.Drain(Mathf.Min(Health.Max * GlowHealthRatio, Health.Current - 1f));
            Say("발광!", GlowTint);
            StageEvents.PostNotice($"{DisplayName}이(가) 빛나기 시작했다!");
            motion = Motion.Hold;

            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                SetTint(Color.Lerp(BaseTint, GlowTint, Mathf.PingPong(t * 5f, 1f)));
                yield return null;
            }

            SetTint(GlowTint);
            for (int i = 0; i < GlowSlams && !IsDead; i++)
            {
                glowSlamIndex = i;
                yield return Slam(SlamRise * 0.75f, 0.25f, 0.35f, RandomArenaX(), GlowSlamMultiplier);
            }

            glowSlamIndex = -1;
            SetTint(BaseTint);
        }

        // ---- 공통 ----

        // 하던 패턴을 끊고 기본 상태로 되돌린다 (분열, 사망).
        private void Interrupt()
        {
            StopAllCoroutines();
            glowSlamIndex = -1;
            CleanupTentacles();
            DestroyShadow();
            ClearTelegraphs();
            ReleaseGrab();
            Rb.gravityScale = baseGravity;
            motion = Motion.Hold;
            contact = true;
            SetTint(BaseTint);
            if (visual != null)
            {
                visual.localScale = visualBaseScale;
                var renderer = visual.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.enabled = true;
            }

            Health.Invulnerable = false;
            if (Animator != null) Animator.SetBool(IsAttackingParam, false);
        }

        protected override bool PlaysDeathPop => false;

        protected override void OnDied()
        {
            Interrupt();
            if (Nearest(transform.position) != null) return; // 다른 몸이 아직 살아 있다

            if (SlimeBrood.Active != null) SlimeBrood.Active.Dismiss();
            BossEvents.Notice = null;
            StageEvents.PostNotice($"{DisplayName}을(를) 쓰러뜨렸다!");
            Shake(0.4f, 0.4f);
            BossDefeatFx.Play(BodyBounds.center, DisplayName);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            kings.Remove(this);
            ReleaseGrab();
            CleanupTentacles();
            DestroyShadow();
            ClearTelegraphs();
            if (Health != null)
            {
                Health.HealthChanged -= OnKingHealthChanged;
                BossEvents.Unregister(Health);
            }
        }

        private void CleanupTentacles()
        {
            foreach (var tentacle in tentacles) if (tentacle != null) Destroy(tentacle.gameObject);
            tentacles.Clear();
        }

        // 웅크렸다 펴기 (패턴 예고).
        // eachFrame: 매 프레임 진행도(0~1)를 받아 예고 그림을 옮긴다 (없어도 된다).
        private IEnumerator Squash(float duration, float depth, System.Action<float> eachFrame = null)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                eachFrame?.Invoke(t / duration);
                if (visual != null)
                {
                    float k = Mathf.Sin(t / duration * Mathf.PI);
                    float y = Mathf.Lerp(1f, depth, k);
                    visual.localScale = new Vector3(visualBaseScale.x * (2f - y), visualBaseScale.y * y, visualBaseScale.z);
                }
                yield return null;
            }

            if (visual != null) visual.localScale = visualBaseScale;
        }

        // ---- 패턴 예고 그림 ----

        private float HeadbuttVx(float dx) => Mathf.Clamp(dx / HeadbuttAir, -HeadbuttMaxSpeed, HeadbuttMaxSpeed);

        // x 아래 땅(fromY에서 아래로)에 타원을 둔다. 아래가 비어 있으면 숨긴다.
        private void PlaceMark(LandingMarkFx mark, float x, float fromY, float strength)
        {
            if (mark == null) return;
            var hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, 40f, ground);
            mark.Place(hit.collider != null ? hit.point : new Vector2(x, fromY), strength, hit.collider != null);
        }

        private void RemoveSlamMark()
        {
            if (slamMark != null) slamMark.Remove();
            slamMark = null;
        }

        private void RemoveHeadbuttMark()
        {
            if (headbuttMark != null) headbuttMark.Remove();
            headbuttMark = null;
        }

        private void RemoveTentacleLine()
        {
            if (tentacleLine != null) tentacleLine.Remove();
            tentacleLine = null;
        }

        private void ClearSummonMarks()
        {
            foreach (var mark in summonMarks) if (mark != null) mark.Remove();
            summonMarks.Clear();
        }

        private void ClearTelegraphs()
        {
            RemoveSlamMark();
            RemoveHeadbuttMark();
            RemoveTentacleLine();
            ClearSummonMarks();
        }

        private void CreateShadow()
        {
            DestroyShadow();
            shadow = new GameObject("SlimeKingShadow");
            shadow.transform.SetParent(transform.parent, true);
            var renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 0;
            var frames = EffectLibrary.LoadRuntimeFrames("Bosses/SlimeKing/Shadow");
            float width = SlamWidth * size;
            if (frames != null)
            {
                renderer.sprite = frames[0];
                float scale = width / frames[0].bounds.size.x;
                shadow.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                renderer.sprite = SolidSpriteFactory.CreateCircle();
                renderer.color = new Color(0.15f, 0f, 0.2f, 0.5f);
                shadow.transform.localScale = new Vector3(width, 0.4f, 1f);
            }
        }

        private void PlaceShadow(float x, float y)
        {
            if (shadow != null) shadow.transform.position = new Vector3(x, y + 0.1f, 0f);
        }

        private void DestroyShadow()
        {
            if (shadow != null) Destroy(shadow);
            shadow = null;
        }

        private float GroundBelow(float x, float fromY)
        {
            var hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, 40f, ground);
            if (hit.collider != null) return hit.point.y;
            return arena.HasValue ? arena.Value.yMin : fromY - SlamRise;
        }

        private bool Grounded()
        {
            var bounds = BodyBounds;
            return Physics2D.Raycast(new Vector2(bounds.center.x, bounds.min.y + 0.05f), Vector2.down, 0.15f, ground);
        }

        private float ClampX(float x)
        {
            if (!arena.HasValue) return x;
            return Mathf.Clamp(x, arena.Value.xMin + ArenaEdgeMargin, arena.Value.xMax - ArenaEdgeMargin);
        }

        private float RandomArenaX()
        {
            if (arena.HasValue) return Random.Range(arena.Value.xMin + ArenaEdgeMargin, arena.Value.xMax - ArenaEdgeMargin);
            var target = FindTarget();
            float center = target != null ? target.position.x : transform.position.x;
            return center + Random.Range(-8f, 8f);
        }

        private void SetFacing(float direction)
        {
            facing = direction;
            transform.localScale = new Vector3(direction * size, size, 1f);
        }

        private void SetTint(Color color)
        {
            if (recolor != null) recolor.Tint = color;
        }

        private void Say(string text, Color color)
        {
            var bounds = BodyBounds;
            DamageNumbers.ShowText(new Vector3(bounds.center.x, bounds.max.y + 0.6f, 0f), text, color, 1.3f);
        }

        private static void Shake(float duration, float magnitude)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(duration, magnitude);
        }
    }
}
