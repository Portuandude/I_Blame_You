using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 쌍둥이 슬라임 기사 한 몸의 공통 처리.
    // - 두 몸의 체력을 화면 위 보스 체력바에 합쳐 보인다 (BossEvents)
    // - 한쪽이 쓰러지면 같은 전투 구역의 남은 쪽이 곧바로 광폭화한다 (EnrageAbility.Trigger)
    // - 몸 색(기술 예고 깜빡임, 광폭화 붉은 맥동)은 ElementRecolor.Tint로 준다. ElementRecolor가 매 프레임 색을 덮어쓰기 때문
    // - 광폭화 연출 (그림·소리만, KnightEnrageFx): 짝이 쓰러지는 순간 짧은 히트스톱 + KnightLink 소리 + 빛 알갱이가 남은 기사에게 날아감,
    //   광폭화 중 머리 위 붉은 김·빠를 때 붉은 잔상·보스 체력바 아래 "광폭화! N초", 풀리면 회색 김 + "헉헉…" + EnrageEnd 소리 + 곡 빠르기 원래대로
    public class SlimeKnight : MonoBehaviour
    {
        public const string DisplayName = "쌍둥이 슬라임 기사";
        private const string MidBossMusicName = "MidBoss"; // ArenaZone의 중간보스 곡
        private const float EnragedMusicPitch = 1.08f;
        private const float NoticeInterval = 3f;
        private static readonly Color EnrageColor = new Color(1f, 0.45f, 0.4f);
        private static readonly List<SlimeKnight> knights = new List<SlimeKnight>();
        private static float lastNoticeTime = -100f;
        private static SlimeKnight bossNoticeOwner; // BossEvents.Notice를 지금 쓰고 있는 기사 (남이 쓴 줄은 지우지 않게)

        private const float LinkHitStop = 0.1f;        // 짝이 쓰러지는 순간 (실제 시간, 초). 정예 처치(0.09)보다 살짝 길게
        private const float PuffInterval = 0.3f;       // 광폭화 중 붉은 김 간격
        private const float GhostInterval = 0.08f;     // 광폭화 중 잔상 간격
        private const float GhostMinSpeed = 2.5f;      // 이보다 빠를 때만 잔상 (돌진·도약)
        private static readonly Color SteamColor = new Color(1f, 0.35f, 0.3f, 0.7f);
        private static readonly Color TiredColor = new Color(0.8f, 0.8f, 0.85f, 0.75f);
        private static readonly Color GhostColor = new Color(1f, 0.35f, 0.3f, 0.45f);

        private HealthSystem health;
        private EnrageAbility enrage;
        private ElementRecolor recolor;
        private Color flashColor;
        private float flashUntil;
        private bool fallen;
        private SpriteRenderer body;
        private Rigidbody2D rb;
        private bool wasEnraged;
        private float nextPuff;
        private float nextGhost;

        public void Configure(ElementRecolor bodyRecolor, EnrageAbility enrageAbility)
        {
            recolor = bodyRecolor;
            enrage = enrageAbility;
        }

        // 기술 예고: seconds 동안 몸이 color로 빠르게 깜빡인다.
        public void Flash(Color color, float seconds)
        {
            flashColor = color;
            flashUntil = Time.time + seconds;
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            rb = GetComponent<Rigidbody2D>();
            var visual = transform.Find("Visual");
            body = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
            knights.Add(this);
            health.HealthChanged += OnHealthChanged;
        }

        private void Start()
        {
            BossEvents.Register(DisplayName, health);
            // 두 몸이 0.3초 간격으로 나오므로 알림은 한 번만
            if (Time.time - lastNoticeTime > NoticeInterval)
            {
                lastNoticeTime = Time.time;
                StageEvents.PostNotice($"중간보스: {DisplayName}");
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current > 0f || fallen) return;
            fallen = true;
            if (recolor != null) recolor.Tint = Color.white;
            ReleaseBossNotice();

            bool enraged = false;
            bool partnerAlive = false;
            foreach (var other in knights)
            {
                if (other == this || other.fallen || other.transform.parent != transform.parent) continue;
                partnerAlive = true;
                if (other.enrage == null || other.enrage.Active) continue;
                other.enrage.Trigger();
                enraged = true;
                // 쓰러진 기사 원소색 빛 알갱이가 남은 기사에게 날아가 닿으면 붉은 고리 (광폭화 자체는 바로 켜진다)
                var stats = GetComponent<CombatStats>();
                var linkColor = stats != null && stats.HasBodyElement ? ElementInfo.Color(stats.BodyElement) : Color.white;
                KnightEnrageFx.LinkSpark(transform.position + Vector3.up * 0.6f, linkColor, other.transform);
            }

            if (enraged)
            {
                HitStop.Play(LinkHitStop);
                GameAudio.PlaySfx("KnightLink", 0.8f, 0f);
                StageEvents.PostNotice("짝을 잃은 기사가 광폭화했다!");
                if (GameAudio.CurrentMusic == MidBossMusicName) GameAudio.SetMusicPitch(EnragedMusicPitch); // 남은 기사 싸움은 곡도 조금 빠르게 (구역을 돌파해 곡이 바뀌면 원래대로)
            }
            if (!partnerAlive) BossDefeatFx.Play(transform.position + Vector3.up * 0.6f, DisplayName); // 둘 다 쓰러졌다
        }

        private void Update()
        {
            if (fallen) return;
            UpdateEnrageFx();
            if (recolor == null) return;

            var color = Color.white;
            if (enrage != null && enrage.Active)
                color = Color.Lerp(Color.white, EnrageColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
            if (Time.time < flashUntil)
                color = Color.Lerp(color, flashColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
            recolor.Tint = color;
        }

        private void UpdateEnrageFx()
        {
            bool active = enrage != null && enrage.Active;
            Vector3 head = transform.position + Vector3.up * 1.3f;

            if (active)
            {
                if (Time.time >= nextPuff)
                {
                    nextPuff = Time.time + PuffInterval;
                    KnightEnrageFx.Puff(head + Vector3.right * Random.Range(-0.25f, 0.25f), SteamColor, 1f);
                }
                if (rb != null && rb.linearVelocity.magnitude >= GhostMinSpeed && Time.time >= nextGhost)
                {
                    nextGhost = Time.time + GhostInterval;
                    KnightEnrageFx.Ghost(body, GhostColor);
                }
                bossNoticeOwner = this;
                BossEvents.Notice = $"광폭화! {Mathf.CeilToInt(enrage.Remaining)}초";
            }
            else if (wasEnraged)
            {
                // 광폭화가 시간이 다 돼 풀렸다: 기운이 빠진다
                ReleaseBossNotice();
                for (int i = 0; i < 3; i++)
                    KnightEnrageFx.Puff(head + new Vector3(-0.3f + 0.3f * i, -0.1f, 0f), TiredColor, 0.8f);
                DamageNumbers.ShowText(head + Vector3.up * 0.3f, "헉헉…", new Color(0.75f, 0.78f, 0.85f), 1f);
                GameAudio.PlaySfx("EnrageEnd", 0.8f);
                if (GameAudio.CurrentMusic == MidBossMusicName) GameAudio.SetMusicPitch(1f);
            }
            wasEnraged = active;
        }

        private void ReleaseBossNotice()
        {
            if (bossNoticeOwner != this) return;
            bossNoticeOwner = null;
            BossEvents.Notice = null;
        }

        private void OnDestroy()
        {
            ReleaseBossNotice();
            knights.Remove(this);
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                BossEvents.Unregister(health);
            }
        }
    }
}
