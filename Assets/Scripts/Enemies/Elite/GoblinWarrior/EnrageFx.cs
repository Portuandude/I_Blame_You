using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 고블린 대전사 광폭화 연출 (그림·소리만, 광폭화 수치·시간은 EnrageAbility 그대로).
    // EnrageAbility.Begin이 처음 켜질 때 붙인다 (프리팹에 없어도 동작). 쌍둥이 슬라임 기사는 SlimeKnight가 따로 연출하므로 붙이지 않는다.
    // - 켜지는 순간: 짧은 히트스톱 + 붉은 큰 고리
    // - 켜져 있는 동안: 머리 위 붉은 김, 빠르게 움직일 때 붉은 잔상 (KnightEnrageFx 재사용)
    // - 시간이 다 돼 풀리면: 회색 김 + "헉헉…" + EnrageEnd 소리 (쓰러져서 끝난 경우는 없음)
    public class EnrageFx : MonoBehaviour
    {
        private const float BeginHitStop = 0.06f;     // 켜지는 순간 (실제 시간, 초). 정예 처치(0.09)보다 짧게
        private const float PuffInterval = 0.3f;
        private const float GhostInterval = 0.08f;
        private const float GhostMinSpeed = 3.2f;     // 옆으로 이보다 빠를 때만 잔상 (광폭화 추격 3.6, 떨어질 때는 빼고)
        private const float HeadHeight = 1.45f;       // 발밑(루트)에서 머리 위까지
        private static readonly Color RingColor = new Color(1f, 0.3f, 0.25f, 0.9f);
        private static readonly Color SteamColor = new Color(1f, 0.35f, 0.3f, 0.7f);
        private static readonly Color TiredColor = new Color(0.8f, 0.8f, 0.85f, 0.75f);
        private static readonly Color GhostColor = new Color(1f, 0.35f, 0.3f, 0.45f);

        private EnrageAbility enrage;
        private HealthSystem health;
        private SpriteRenderer body;
        private Rigidbody2D rb;
        private bool wasActive;
        private float nextPuff;
        private float nextGhost;

        private void Awake()
        {
            enrage = GetComponent<EnrageAbility>();
            health = GetComponent<HealthSystem>();
            body = GetComponentInChildren<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
        }

        // EnrageAbility.Begin에서 부른다.
        public void OnBegin()
        {
            HitStop.Play(BeginHitStop);
            VfxPlayer.ManaRing(transform, Vector3.up * (HeadHeight * 0.5f), RingColor, 2.2f);
            wasActive = true;
            nextPuff = Time.time + 0.1f;
        }

        private void Update()
        {
            if (enrage == null) return;
            if (health != null && health.IsDead) return;

            bool active = enrage.Active;
            Vector3 head = transform.position + Vector3.up * HeadHeight;

            if (active)
            {
                if (Time.time >= nextPuff)
                {
                    nextPuff = Time.time + PuffInterval;
                    KnightEnrageFx.Puff(head + Vector3.right * Random.Range(-0.2f, 0.2f), SteamColor, 0.85f);
                }
                if (rb != null && Mathf.Abs(rb.linearVelocity.x) >= GhostMinSpeed && Time.time >= nextGhost)
                {
                    nextGhost = Time.time + GhostInterval;
                    KnightEnrageFx.Ghost(body, GhostColor);
                }
            }
            else if (wasActive)
            {
                for (int i = 0; i < 3; i++)
                    KnightEnrageFx.Puff(head + new Vector3(-0.25f + 0.25f * i, -0.1f, 0f), TiredColor, 0.7f);
                DamageNumbers.ShowText(head + Vector3.up * 0.3f, "헉헉…", new Color(0.75f, 0.78f, 0.85f), 1f);
                GameAudio.PlaySfx("EnrageEnd", 0.8f);
            }
            wasActive = active;
        }
    }
}
