using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 스테이지 2 보스 "코볼트 족장과 와르그" 한 무리 (2026-11-24 사람 결정 F5: 족장 + 와르그, 둘이 한 보스).
    // 족장·와르그 몸마다 붙는다: 화면 위 보스 체력바에 셋을 합쳐 보이고(BossEvents),
    // 하나가 쓰러지면 남은 몸이 화가 나 빨라지고(KoboldChiefRules), 셋 다 쓰러지면 처치 연출. 쌍둥이 기사(SlimeKnight)와 같은 짜임.
    public class KoboldPack : MonoBehaviour
    {
        public const string DisplayName = "코볼트 족장과 와르그";
        private static readonly List<KoboldPack> members = new List<KoboldPack>();
        private static float lastNoticeTime = -10f;

        private HealthSystem health;
        private CombatStats stats;
        private KoboldController mover;
        private SpriteRenderer body;
        private Color baseColor = Color.white;
        private int rage;
        private bool fallen;

        public bool IsChief { get; set; }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            stats = GetComponent<CombatStats>();
            mover = GetComponent<KoboldController>();
            var visual = transform.Find("Visual");
            body = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (body != null) baseColor = body.color;
            members.Add(this);
            health.HealthChanged += OnHealthChanged;
        }

        private void Start()
        {
            BossEvents.Register(DisplayName, health);
            if (Time.time - lastNoticeTime > 2f)
            {
                lastNoticeTime = Time.time;
                StageEvents.PostNotice($"보스: {DisplayName}");
            }
        }

        private void Update()
        {
            if (fallen) return;
            // 넉백으로 낙사선 아래로 떨어져 치워져도 쓰러진 것으로 친다 (SlimeKnight와 같음)
            if (transform.position.y < FallRecovery.FloorY + FallRecovery.KillY) Fall();
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current <= 0f) Fall();
        }

        private void Fall()
        {
            if (fallen) return;
            fallen = true;
            bool anyAlive = false;
            foreach (var other in members)
            {
                if (other == this || other.fallen || other.transform.parent != transform.parent) continue;
                anyAlive = true;
                other.Enrage();
            }

            if (anyAlive)
            {
                GameAudio.PlaySfx("KnightLink", 0.7f, 0.05f);
                StageEvents.PostNotice(IsChief ? "족장을 잃은 와르그가 날뛴다!" : "무리를 잃고 화가 났다!");
            }
            else BossDefeatFx.Play(transform.position + Vector3.up * 0.6f, DisplayName);
        }

        private void Enrage()
        {
            if (fallen) return;
            rage++;
            if (stats != null) stats.MoveSpeedMultiplier *= KoboldChiefRules.EnrageSpeedStep;
            if (mover != null) mover.CooldownMultiplier = KoboldChiefRules.CooldownFor(rage);
            if (body != null) body.color = Color.Lerp(baseColor, new Color(1f, 0.35f, 0.3f), Mathf.Min(1f, 0.35f * rage));
        }

        private void OnDestroy()
        {
            members.Remove(this);
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                BossEvents.Unregister(health);
            }
        }
    }
}
