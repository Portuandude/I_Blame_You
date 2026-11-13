using UnityEngine;

namespace IBlameYou.Systems
{
    // 전투 구역을 돌파하면 구역 가운데에 떨어지는 회복 구슬. 닿으면 체력과 마나를 일부 채우고 사라진다.
    // 줍지 않고 지나가도 청크 기록(ChunkStreamer)에 남아서, 다시 돌아오면 같은 자리에 또 있다.
    public class ArenaReward : MonoBehaviour
    {
        public const float HealRatio = 0.3f; // 최대 체력의 30%
        public const float ManaRatio = 0.5f; // 최대 마나의 50%
        private const float FloatHeight = 0.9f;
        private static readonly Vector3 HoneyGlintOffset = new Vector3(0f, 1.5f, 0f); // 꿀단지 시작 높이 (머리 위, 훈장·도토리와 같게)
        private static readonly Vector3 HorseshoeGlintOffset = new Vector3(-0.55f, 1.4f, 0f); // 말굽 자석 (꿀단지와 안 겹치게 왼쪽)
        private const float BobAmplitude = 0.15f;
        private const float BobSpeed = 3f;
        // 꿀단지를 가졌으면 구슬 뒤에 금빛 테두리가 숨 쉬듯 빛나고 가끔 반짝인다 ("더 많이 찬다")
        private static readonly Color HoneyRimColor = new Color(1f, 0.82f, 0.28f);
        private const float HoneyRimSize = 1.05f;      // 구슬(약 0.7유닛)보다 조금 크게
        private const float HoneyCheckInterval = 0.5f; // 유물을 다시 확인하는 간격
        private const float HoneySparkleInterval = 1.1f;
        // 자석: 살아 있는 플레이어가 구슬 가운데에서 이만큼 안에 들어오면 구슬이 플레이어 쪽으로 통통 끌려온다
        // (RelicBook.MagnetRadius, 말굽 자석이 있으면 HorseshoeMagnetRadius — 꿀단지처럼 0.5초마다 확인)
        private const float MagnetCheckInterval = 0.1f;
        private const float MagnetStartSpeed = 2f;
        private const float MagnetAcceleration = 14f;
        private const float MagnetMaxSpeed = 9f;
        private static readonly Collider2D[] MagnetHits = new Collider2D[16];
        private static readonly RaycastHit2D[] WallHits = new RaycastHit2D[8];

        private System.Action onCollected;
        private Transform visual;
        private bool collected;
        private float phase;
        private SpriteRenderer honeyRim;
        private float honeyCheckTimer;
        private float honeySparkleTimer;
        private Transform magnetTarget;
        private float magnetSpeed;
        private float magnetCheckTimer;
        private float nextWhooshTime;
        private float foundEdgeDistance; // 마지막으로 찾은 플레이어 몸 가장자리까지 거리 (FindMagnetTarget)
        private float magnetRadius = IBlameYou.Player.RelicBook.MagnetRadius;
        private float magnetRelicTimer;
        private SpriteRenderer orbRenderer;
        private float trailTimer;
        // 등장 (보상 연출, 2026-11-13): 방금 나온 구슬은 그림이 AppearStart배에서 통통하게 조금 넘쳤다가(1.15배) 제 크기로.
        private const float AppearTime = 0.35f;
        private const float AppearStart = 0.1f;
        private Vector3 visualBaseScale;
        private float appearElapsed = -1f; // 0 이상이면 커지는 중
        // 자석 꼬리: 끌려오는 동안 구슬 뒤로 분홍 잔상이 짧게 남는다 (Afterimage, 빨라졌을 때만)
        private const float TrailInterval = 0.05f;
        private const float TrailMinSpeed = 3f;
        private static readonly Color TrailColor = new Color(1f, 0.6f, 0.75f);
        private const float WhooshCooldown = 0.6f;

        // position: 구슬이 놓일 바닥 위 가운데 (월드). dropped: 방금 떨어진 것이면 등장 이펙트를 보여준다 (청크를 다시 만들 때는 false).
        public static ArenaReward Create(Transform parent, Vector2 position, bool dropped, System.Action onCollected)
        {
            var go = new GameObject("ArenaReward");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.2f, 2f);
            trigger.offset = new Vector2(0f, 1f);

            var reward = go.AddComponent<ArenaReward>();
            reward.onCollected = onCollected;
            reward.phase = position.x;
            // 통통한 하트 구슬 (Tools/ArtGen/generate_vfx.py HeartOrb), 없으면 예전 구슬 그림
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/HeartOrb") ?? TrapCommon.Frames("Reward");
            var renderer = EffectLibrary.CreateVisual(go.transform, "Orb", frames, 10f, true,
                new Color(1f, 0.45f, 0.55f), 0.7f, 3);
            reward.visual = renderer.transform;
            reward.orbRenderer = renderer;
            reward.visual.localPosition = new Vector3(0f, FloatHeight, 0f);

            var rimGo = new GameObject("HoneyRim");
            rimGo.transform.SetParent(reward.visual, false);
            float rimScale = HoneyRimSize / Mathf.Max(0.01f, reward.visual.localScale.x); // 그림이 없어 구슬이 0.7배 원이어도 월드 크기는 같게
            rimGo.transform.localScale = new Vector3(rimScale, rimScale, 1f);
            reward.honeyRim = rimGo.AddComponent<SpriteRenderer>();
            reward.honeyRim.sprite = SolidSpriteFactory.CreateCircle();
            reward.honeyRim.color = HoneyRimColor;
            reward.honeyRim.sortingOrder = 2; // 구슬(3) 뒤
            reward.honeyRim.enabled = AnyPlayerHasHoney();

            if (dropped)
            {
                EffectLibrary.PlayHit(Element.Neutral, go.transform.position + Vector3.up * FloatHeight, 1.2f);
                reward.visualBaseScale = reward.visual.localScale;
                reward.appearElapsed = 0f;
                reward.visual.localScale = reward.visualBaseScale * AppearStart;
            }
            return reward;
        }

        // 아직 안 주운 구슬 목록 (HUD 화면 밖 길잡이 하트, SkillHUD.OrbGuide.cs). 켜질 때 넣고 꺼지거나 주우면 뺀다.
        private static readonly System.Collections.Generic.List<ArenaReward> active = new System.Collections.Generic.List<ArenaReward>();
        public static System.Collections.Generic.IReadOnlyList<ArenaReward> Active => active;
        // 화면에 보이는 구슬 자리 (둥실 떠 있는 그림 위치)
        public Vector3 OrbPosition => visual != null ? visual.position : transform.position;
        public bool Collected => collected;

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        private void Update()
        {
            if (visual == null) return;
            float y = FloatHeight + Mathf.Sin((Time.time + phase) * BobSpeed) * BobAmplitude;
            visual.localPosition = new Vector3(0f, y, 0f);
            UpdateAppear();
            UpdateHoneyRim();
            UpdateMagnet();
        }

        private void UpdateAppear()
        {
            if (appearElapsed < 0f) return;
            appearElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(appearElapsed / AppearTime);
            // 0.1 → 1.15 (t=0.6) → 1: 앞 60%는 빠르게 부풀고, 뒤 40%는 살짝 줄어들며 자리 잡는다
            float k = t < 0.6f ? Mathf.Lerp(AppearStart, 1.15f, 1f - (1f - t / 0.6f) * (1f - t / 0.6f))
                               : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
            visual.localScale = visualBaseScale * k;
            if (t >= 1f)
            {
                visual.localScale = visualBaseScale;
                appearElapsed = -1f;
            }
        }

        // 가까이 온 플레이어 쪽으로 구슬(트리거째)이 빨라지며 끌려온다. 닿으면 OnTriggerEnter2D로 줍는다.
        // 끌려가다 플레이어가 쓰러지거나 멀어지면 그 자리에 멈춘다 (청크 기록 자리는 그대로라 다시 오면 원래 자리).
        private void UpdateMagnet()
        {
            if (collected) return;
            magnetCheckTimer -= Time.deltaTime;
            magnetRelicTimer -= Time.deltaTime;
            if (magnetRelicTimer <= 0f)
            {
                magnetRelicTimer = HoneyCheckInterval;
                magnetRadius = AnyPlayerHas(IBlameYou.Player.RelicId.HorseshoeMagnet)
                    ? IBlameYou.Player.RelicBook.HorseshoeMagnetRadius : IBlameYou.Player.RelicBook.MagnetRadius;
            }
            if (magnetCheckTimer <= 0f)
            {
                magnetCheckTimer = MagnetCheckInterval;
                var found = FindMagnetTarget();
                // 끌려오기 시작할 때 작게 "슝" (벽 모서리에서 잡혔다 놓쳤다 해도 0.6초에 한 번만)
                if (found != null && magnetTarget == null && Time.time >= nextWhooshTime)
                {
                    GameAudio.PlaySfx("MagnetWhoosh", 0.5f);
                    nextWhooshTime = Time.time + WhooshCooldown;
                    // 말굽 자석 덕분에 기본 거리 밖에서 잡혔으면 머리 위로 말굽 자석이 톡
                    // (몸 가장자리까지 거리로 판단, 잡힌 그 플레이어가 유물을 가졌을 때만)
                    var foundRelics = found.GetComponent<IBlameYou.Player.PlayerRelics>();
                    if (foundRelics != null && foundRelics.Has(IBlameYou.Player.RelicId.HorseshoeMagnet)
                        && foundEdgeDistance > IBlameYou.Player.RelicBook.MagnetRadius)
                        VfxPlayer.HorseshoeGlint(found, HorseshoeGlintOffset); // 곧 주우면 꿀단지가 가운데에 뜨므로 왼쪽으로 비켜서
                }
                magnetTarget = found;
                if (magnetTarget == null) magnetSpeed = 0f;
            }
            if (magnetTarget == null) return;

            magnetSpeed = Mathf.Min(MagnetMaxSpeed, Mathf.Max(MagnetStartSpeed, magnetSpeed + MagnetAcceleration * Time.deltaTime));
            // 구슬 가운데(바닥 + FloatHeight)를 플레이어 몸통 쪽으로
            Vector3 orbCenter = transform.position + Vector3.up * FloatHeight;
            Vector3 goal = magnetTarget.position + Vector3.up * 0.6f;
            Vector3 next = Vector3.MoveTowards(orbCenter, goal, magnetSpeed * Time.deltaTime);
            transform.position += next - orbCenter;

            if (magnetSpeed >= TrailMinSpeed)
            {
                trailTimer -= Time.deltaTime;
                if (trailTimer <= 0f)
                {
                    trailTimer = TrailInterval;
                    Afterimage.Spawn(orbRenderer, TrailColor, 0.5f, 0.18f);
                }
            }
        }

        private Transform FindMagnetTarget()
        {
            Vector2 center = transform.position + Vector3.up * FloatHeight;
            var filter = new ContactFilter2D();
            filter.NoFilter();
            int playerLayer = LayerMask.NameToLayer(CharacterLayers.PlayerLayerName); // 플레이어 층이 있으면 그 층만 (바닥 칸이 버퍼를 채우지 않게)
            if (playerLayer >= 0) filter.SetLayerMask(1 << playerLayer);
            int count = Physics2D.OverlapCircle(center, magnetRadius, filter, MagnetHits);
            Transform best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var player = TrapCommon.PlayerFrom(MagnetHits[i]);
                if (player == null) continue;
                var health = player.GetComponent<HealthSystem>();
                if (health != null && health.IsDead) continue;
                if (WallBetween(center, (Vector2)player.transform.position + Vector2.up * 0.6f)) continue; // 벽 너머로는 끌려가지 않는다
                float distance = ((Vector2)player.transform.position - center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = player.transform;
                }
            }
            // 고른 플레이어 몸 가장자리까지 가장 가까운 거리 (OverlapCircle은 가장자리로 잡는다)
            foundEdgeDistance = float.MaxValue;
            if (best != null)
                for (int i = 0; i < count; i++)
                {
                    var player = TrapCommon.PlayerFrom(MagnetHits[i]);
                    if (player == null || player.transform != best) continue;
                    foundEdgeDistance = Mathf.Min(foundEdgeDistance, (MagnetHits[i].ClosestPoint(center) - center).magnitude);
                }
            return best;
        }

        // from → to 사이에 막힌 땅(벽·바닥)이 있으면 true. 위에서만 밟히는 한쪽 발판(PlatformEffector2D)은 막지 않는다
        // (구슬이 발판 위·아래에 있어도 플레이어가 그 발판을 지나 다닐 수 있으니까).
        private static bool WallBetween(Vector2 from, Vector2 to)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(1 << TrapCommon.GroundLayer);
            filter.useTriggers = false;
            int count = Physics2D.Linecast(from, to, filter, WallHits);
            for (int i = 0; i < count; i++)
            {
                var hit = WallHits[i].collider;
                if (hit == null || hit.usedByEffector) continue; // 한쪽 발판
                if (TrapCommon.PlayerFrom(hit) != null) continue; // Ground 층이 없어 Default로 찾을 때 플레이어 몸은 벽이 아니다
                return true;
            }
            return false;
        }

        private void UpdateHoneyRim()
        {
            if (honeyRim == null) return;
            honeyCheckTimer -= Time.deltaTime;
            if (honeyCheckTimer <= 0f)
            {
                honeyCheckTimer = HoneyCheckInterval;
                honeyRim.enabled = AnyPlayerHasHoney();
            }
            if (!honeyRim.enabled) return;

            var color = HoneyRimColor;
            color.a = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin((Time.time + phase) * 4f));
            honeyRim.color = color;

            honeySparkleTimer -= Time.deltaTime;
            if (honeySparkleTimer <= 0f)
            {
                honeySparkleTimer = HoneySparkleInterval;
                // 테두리 위쪽 가장자리에서 번갈아 좌우로 작게 반짝
                float side = Mathf.FloorToInt(Time.time / HoneySparkleInterval) % 2 == 0 ? 1f : -1f;
                VfxPlayer.AimGlint(visual.position + new Vector3(0.32f * side, 0.3f, 0f));
            }
        }

        // 둘(A·B가 함께 쓰는 캐릭터) 중 누구라도 꿀단지를 가졌는지.
        private static bool AnyPlayerHasHoney() => AnyPlayerHas(IBlameYou.Player.RelicId.HoneyJar);

        private static bool AnyPlayerHas(IBlameYou.Player.RelicId id)
        {
            foreach (var relics in FindObjectsByType<IBlameYou.Player.PlayerRelics>(FindObjectsSortMode.None))
                if (relics.Has(id)) return true;
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        // 쓰러진 채 겹쳐 있다가 되살아난 경우에도 주울 수 있게.
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (collected) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;

            var health = player.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;

            collected = true;
            var progression = player.GetComponent<IBlameYou.Player.PlayerProgression>();
            float healRatio = progression != null ? progression.RewardHealRatio : HealRatio; // 생명 5 특성이면 45%
            var relics = player.GetComponent<IBlameYou.Player.PlayerRelics>();
            bool honey = relics != null && relics.Has(IBlameYou.Player.RelicId.HoneyJar);
            if (honey) healRatio += IBlameYou.Player.RelicBook.HoneyJarHealBonus; // 꿀단지: +15%p
            if (health != null)
            {
                float before = health.Current;
                health.Heal(health.Max * healRatio);
                DamageNumbers.ShowHeal(player.transform.position, health.Current - before);
            }
            var mana = player.GetComponent<ManaSystem>();
            if (mana != null) mana.Restore(mana.Max * ManaRatio);

            var orbCenter = transform.position + Vector3.up * FloatHeight;
            var pop = EffectLibrary.LoadRuntimeFrames("Effects/HeartPop");
            if (pop != null) EffectLibrary.PlayOneShot(orbCenter, pop, 18f, Color.clear, 0f, false, 1.1f, sortingOrder: 7); // 하트·별이 톡톡
            else EffectLibrary.PlayHit(Element.Water, orbCenter, 1.2f);
            GameAudio.PlaySfx("Pickup", 0.7f);
            if (honey) VfxPlayer.HoneyGlint(player.transform, HoneyGlintOffset); // 머리 위로 작은 꿀단지가 톡
            string honeyNote = honey ? $" · 꿀단지 +{IBlameYou.Player.RelicBook.HoneyJarHealBonus * 100f:0}%" : "";
            StageEvents.PostNotice($"보상: 체력 {healRatio * 100f:0}% · 마나 {ManaRatio * 100f:0}% 회복{honeyNote}");
            onCollected?.Invoke();
            Destroy(gameObject);
        }
    }
}
