using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 프리즘 젤리 패턴이 남기는 위험물 (JellyPatterns가 만든다). 수치는 PrismJellyRules.
    // 모두 플레이어 몸(PlayerCombat이 있는 HealthSystem)만 맞히고, 주인 젤리가 사라지거나 방이 치워지면 함께 사라진다.
    public static class JellyHazards
    {
        private const int TexSize = 12;
        private static Sprite blob;

        // 흰 동그라미 (색은 SpriteRenderer.color로 곱한다)
        public static Sprite Blob()
        {
            if (blob != null) return blob;
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = TexSize / 2f;
            for (int y = 0; y < TexSize; y++)
                for (int x = 0; x < TexSize; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    var col = Color.clear;
                    if (d <= r - 0.5f)
                    {
                        col = d > r - 1.8f ? new Color(0.8f, 0.8f, 0.8f) : Color.white;
                        float hx = x + 0.5f - r * 0.65f, hy = y + 0.5f - r * 1.35f;
                        if (hx * hx + hy * hy < 2.5f) col = new Color(1f, 1f, 1f, 1f);
                    }
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            blob = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f);
            return blob;
        }

        // 겹침 조회 결과 (PlayersIn 안에서만 쓰고 바로 비운다 — 피해 처리 전에 results로 옮기므로 겹쳐 불려도 안전)
        private static readonly List<Collider2D> overlaps = new List<Collider2D>();

        // 겹친 것 중 플레이어 몸 (적·자기 자신 제외)을 results에 담는다 (위험물마다 프레임마다 불려 새 배열·집합을 만들지 않게).
        // results는 부르는 위험물이 따로 가진 목록 — 피해 처리 중 다른 위험물이 덮어쓰지 않게.
        public static List<HealthSystem> PlayersIn(Vector2 center, Vector2 size, List<HealthSystem> results)
        {
            results.Clear();
            OverlapQuery.Box(center, size, overlaps);
            foreach (var hit in overlaps)
            {
                var target = hit.GetComponentInParent<HealthSystem>();
                if (target == null || target.IsDead || target.GetComponent<PlayerCombat>() == null) continue;
                if (!results.Contains(target)) results.Add(target);
            }
            overlaps.Clear();
            return results;
        }

        // 주인 젤리가 사라졌거나 쓰러졌으면 위험물도 사라진다 (죽은 보스가 때리지 않게, 방이 치워져도)
        public static bool OwnerGone(GameObject owner)
        {
            if (owner == null) return true;
            var health = owner.GetComponent<HealthSystem>();
            return health != null && health.IsDead;
        }

        public static StatusDebuff DebuffFor(Element color)
        {
            switch (color)
            {
                case Element.Fire: return StatusDebuff.Burn;
                case Element.Water:
                case Element.Earth: return StatusDebuff.Wet; // 땅 = 둔화 (기획 초안). 경직(Stun)은 웅덩이에 서 있는 동안 되풀이되면 너무 세다
                default: return StatusDebuff.None;            // 바람 = 밀침 (JellyPuddle)
            }
        }
    }

    // 출렁 착지 충격파: 착지점에서 한쪽으로 바닥을 타고 간다. 바닥에서 ShockwaveHeight 위까지만 맞는다 (점프로 피함).
    public class JellyShockwave : MonoBehaviour
    {
        private readonly List<HealthSystem> players = new List<HealthSystem>(); // JellyHazards.PlayersIn 결과 (다시 씀)
        private const float DustGap = 0.07f;
        private readonly HashSet<HealthSystem> struck = new HashSet<HealthSystem>();
        private GameObject owner;
        private CombatStats attacker;
        private Element element;
        private float direction;
        private float groundY;
        private float age;
        private float nextDust;
        private LayerMask ground;

        public static void Launch(GameObject owner, Vector2 feet, float direction, Element element)
        {
            var go = new GameObject("JellyShockwave");
            go.transform.SetParent(owner.transform.parent, true);
            go.transform.position = feet;
            var wave = go.AddComponent<JellyShockwave>();
            wave.owner = owner;
            wave.attacker = owner.GetComponent<CombatStats>();
            wave.element = element;
            wave.direction = Mathf.Sign(direction);
            wave.groundY = feet.y;
            wave.ground = EnemySpawner.GroundMask();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (JellyHazards.OwnerGone(owner) || age >= PrismJellyRules.ShockwaveSeconds)
            {
                Destroy(gameObject);
                return;
            }

            var pos = transform.position;
            float step = PrismJellyRules.ShockwaveSpeed * Time.deltaTime;
            // 벽에 닿으면 멈춘다
            if (Physics2D.Raycast(new Vector2(pos.x, groundY + 0.3f), new Vector2(direction, 0f), step + 0.1f, ground))
            {
                Destroy(gameObject);
                return;
            }
            pos.x += direction * step;
            transform.position = pos;

            if (Time.time >= nextDust)
            {
                nextDust = Time.time + DustGap;
                VfxPlayer.Dust(new Vector3(pos.x, groundY, 0f), 0.5f);
            }

            float h = PrismJellyRules.ShockwaveHeight;
            foreach (var target in JellyHazards.PlayersIn(new Vector2(pos.x, groundY + h * 0.5f), new Vector2(0.8f, h), players))
            {
                if (!struck.Add(target)) continue;
                DamageCalculator.Strike(attacker, target, DamageKind.Magic, element, 0.9f);
            }
        }
    }

    // 젤리 방울: 포물선으로 날아가 땅에 닿으면 그 자리에 웅덩이. 날아가는 중에 플레이어에 닿으면 작은 피해 후 사라진다.
    public class JellyDroplet : MonoBehaviour
    {
        private readonly List<HealthSystem> players = new List<HealthSystem>(); // JellyHazards.PlayersIn 결과 (다시 씀)
        private const float Gravity = 16f;
        private const float MaxLife = 3f;
        private GameObject owner;
        private CombatStats attacker;
        private Element element;
        private Vector2 velocity;
        private float age;
        private LayerMask ground;

        public static void Launch(GameObject owner, Vector2 from, Vector2 velocity, Element element)
        {
            var go = new GameObject("JellyDroplet");
            go.transform.SetParent(owner.transform.parent, true);
            go.transform.position = from;
            go.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = JellyHazards.Blob();
            sr.color = ElementInfo.Color(element);
            sr.sortingOrder = 3;
            var drop = go.AddComponent<JellyDroplet>();
            drop.owner = owner;
            drop.attacker = owner.GetComponent<CombatStats>();
            drop.element = element;
            drop.velocity = velocity;
            drop.ground = EnemySpawner.GroundMask();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (JellyHazards.OwnerGone(owner) || age >= MaxLife)
            {
                Destroy(gameObject);
                return;
            }

            velocity.y -= Gravity * Time.deltaTime;
            Vector2 pos = transform.position;
            Vector2 step = velocity * Time.deltaTime;
            var hit = Physics2D.Raycast(pos, step.normalized, step.magnitude + 0.05f, ground);
            if (hit.collider != null)
            {
                // 내려오다 바닥(윗면)에 닿으면 웅덩이, 올라가다 천장·벽에 닿으면 그냥 터진다
                if (velocity.y < 0f && hit.normal.y > 0.5f) JellyPuddle.Create(owner, hit.point, element);
                else VfxPlayer.Dust(hit.point, 0.3f);
                Destroy(gameObject);
                return;
            }
            transform.position = pos + step;

            foreach (var target in JellyHazards.PlayersIn(transform.position, new Vector2(0.3f, 0.3f), players))
            {
                DamageCalculator.Strike(attacker, target, DamageKind.Magic, element, 0.5f);
                Destroy(gameObject);
                return;
            }
        }
    }

    // 젤리 웅덩이: PuddleSeconds 동안 바닥에 깔린다. 위에 있으면 PuddleTick마다 색에 맞는 상태이상 (불 화상, 물·땅 둔화),
    // 바람 웅덩이는 웅덩이 가운데에서 바깥쪽으로 민다 (움직이는 발판처럼 PlayerMovement.SetPlatformVelocity, 물리 프레임보다 먼저).
    [DefaultExecutionOrder(-100)]
    public class JellyPuddle : MonoBehaviour
    {
        private readonly List<HealthSystem> players = new List<HealthSystem>(); // JellyHazards.PlayersIn 결과 (다시 씀)
        private const float Width = 1.6f;
        private const float Height = 0.6f;
        private const float WindPush = 3.5f;
        private GameObject owner;
        private CombatStats attacker;
        private Element element;
        private float age;
        private float nextTick;
        private SpriteRenderer sr;

        public static void Create(GameObject owner, Vector2 point, Element element)
        {
            var go = new GameObject("JellyPuddle");
            go.transform.SetParent(owner.transform.parent, true);
            go.transform.position = new Vector3(point.x, point.y + 0.04f, 0f);
            go.transform.localScale = new Vector3(Width / 0.12f, 0.25f / 0.12f, 1f); // 그림 0.12유닛 → 납작한 타원
            var puddle = go.AddComponent<JellyPuddle>();
            puddle.owner = owner;
            puddle.attacker = owner.GetComponent<CombatStats>();
            puddle.element = element;
            puddle.sr = go.AddComponent<SpriteRenderer>();
            puddle.sr.sprite = JellyHazards.Blob();
            var c = ElementInfo.Color(element);
            c.a = 0.75f;
            puddle.sr.color = c;
            puddle.sr.sortingOrder = 1;
            VfxPlayer.Dust(go.transform.position, 0.4f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (JellyHazards.OwnerGone(owner) || age >= PrismJellyRules.PuddleSeconds)
            {
                Destroy(gameObject);
                return;
            }
            // 마지막 0.5초 옅어진다
            float left = PrismJellyRules.PuddleSeconds - age;
            if (left < 0.5f)
            {
                var c = sr.color;
                c.a = 0.75f * left / 0.5f;
                sr.color = c;
            }

            if (Time.time < nextTick) return;
            var debuff = JellyHazards.DebuffFor(element);
            if (debuff == StatusDebuff.None) return;
            bool any = false;
            foreach (var target in JellyHazards.PlayersIn(Center, new Vector2(Width, Height), players))
            {
                any = true;
                var hp = target.GetComponentInParent<HealthSystem>();
                if (hp != null && hp.IsInvulnerable) continue; // 대시·순간이동 무적 중엔 웅덩이 상태이상도 안 걸린다 (기사 돌진·불기둥 함정과 같게)
                StatusEffects.TryApply(target, debuff, attacker);
            }
            if (any) nextTick = Time.time + PrismJellyRules.PuddleTick;
        }

        private void FixedUpdate()
        {
            if (element != Element.Wind || JellyHazards.OwnerGone(owner)) return;
            foreach (var target in JellyHazards.PlayersIn(Center, new Vector2(Width, Height), players))
            {
                var mover = target.GetComponent<PlayerMovement>();
                if (mover == null) continue;
                float side = target.transform.position.x >= transform.position.x ? 1f : -1f;
                mover.SetPlatformVelocity(new Vector2(side * WindPush, 0f));
            }
        }

        private Vector2 Center => (Vector2)transform.position + new Vector2(0f, Height * 0.5f);
    }

    // 프리즘 광선: BeamWarn초 동안 무지개 점선으로 높이를 예고한 뒤 BeamSeconds 동안 방을 가로지른다 (벽에서 끊김).
    // 높이는 예고를 시작할 때 플레이어 몸 높이로 묶는다 → P1이 위/아래 순간이동으로 층을 바꿔 피한다.
    public class PrismBeam : MonoBehaviour
    {
        private readonly List<HealthSystem> players = new List<HealthSystem>(); // JellyHazards.PlayersIn 결과 (다시 씀)
        private const float Reach = 40f;
        private static readonly Color[] Rainbow =
        {
            new Color(1f, 0.35f, 0.35f), new Color(1f, 0.7f, 0.3f), new Color(1f, 0.95f, 0.4f),
            new Color(0.45f, 1f, 0.55f), new Color(0.4f, 0.75f, 1f), new Color(0.75f, 0.5f, 1f)
        };

        private readonly HashSet<HealthSystem> struck = new HashSet<HealthSystem>();
        private GameObject owner;
        private CombatStats attacker;
        private Element element;
        private float y;
        private float left, right;
        private float age;
        private AimLineFx warnLeft, warnRight;
        private readonly List<SpriteRenderer> bands = new List<SpriteRenderer>();

        public static PrismBeam Fire(GameObject owner, float originX, float height, Element element)
        {
            var go = new GameObject("PrismBeam");
            go.transform.SetParent(owner.transform.parent, true);
            go.transform.position = new Vector3(originX, height, 0f);
            var beam = go.AddComponent<PrismBeam>();
            beam.owner = owner;
            beam.attacker = owner.GetComponent<CombatStats>();
            beam.element = element;
            beam.y = height;
            var ground = EnemySpawner.GroundMask();
            var hitL = Physics2D.Raycast(new Vector2(originX, height), Vector2.left, Reach, ground);
            var hitR = Physics2D.Raycast(new Vector2(originX, height), Vector2.right, Reach, ground);
            beam.left = hitL.collider != null ? hitL.point.x : originX - Reach;
            beam.right = hitR.collider != null ? hitR.point.x : originX + Reach;
            beam.warnLeft = AimLineFx.Create(go, new Color(1f, 0.6f, 0.95f));
            beam.warnRight = AimLineFx.Create(go, new Color(0.6f, 0.9f, 1f));
            return beam;
        }

        public bool Done => age >= PrismJellyRules.BeamWarn + PrismJellyRules.BeamSeconds;

        private void Update()
        {
            age += Time.deltaTime;
            if (JellyHazards.OwnerGone(owner) || Done)
            {
                Destroy(gameObject);
                return;
            }

            var from = new Vector2(transform.position.x, y);
            if (age < PrismJellyRules.BeamWarn)
            {
                float strength = age / PrismJellyRules.BeamWarn;
                if (warnLeft != null) warnLeft.Aim(from, new Vector2(left, y), Reach, 0, strength);
                if (warnRight != null) warnRight.Aim(from, new Vector2(right, y), Reach, 0, strength);
                return;
            }

            if (bands.Count == 0) Ignite();
            float t = (age - PrismJellyRules.BeamWarn) / PrismJellyRules.BeamSeconds;
            for (int i = 0; i < bands.Count; i++)
            {
                var c = bands[i].color;
                c.a = 0.9f * (1f - t * t);
                bands[i].color = c;
            }

            float width = right - left;
            foreach (var target in JellyHazards.PlayersIn(new Vector2((left + right) * 0.5f, y), new Vector2(width, PrismJellyRules.BeamHalfHeight * 2f), players))
            {
                if (!struck.Add(target)) continue;
                DamageCalculator.Strike(attacker, target, DamageKind.Magic, element, 1.4f);
            }
        }

        // 여섯 줄 무지개 띠 + 쾅 소리·흔들림
        private void Ignite()
        {
            if (warnLeft != null) warnLeft.Remove();
            if (warnRight != null) warnRight.Remove();
            warnLeft = warnRight = null;

            float thickness = PrismJellyRules.BeamHalfHeight * 2f;
            float band = thickness / Rainbow.Length;
            for (int i = 0; i < Rainbow.Length; i++)
            {
                var go = new GameObject("Band");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3((left + right) * 0.5f, y - thickness * 0.5f + band * (i + 0.5f), 0f);
                go.transform.localScale = new Vector3(right - left, band, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SolidSpriteFactory.CreateSquare();
                sr.color = Rainbow[i];
                sr.sortingOrder = 4;
                bands.Add(sr);
            }
            GameAudio.PlaySfx("KnightLink", 0.9f, 0f);
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<IBlameYou.Core.CameraFollow>() : null;
            if (follow != null) follow.Shake(0.2f, 0.12f);
        }

        private void OnDestroy()
        {
            if (warnLeft != null) warnLeft.Remove();
            if (warnRight != null) warnRight.Remove();
        }
    }
}
