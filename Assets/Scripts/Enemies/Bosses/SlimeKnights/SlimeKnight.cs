using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 쌍둥이 슬라임 기사 한 몸의 공통 처리.
    // - 두 몸의 체력을 화면 위 보스 체력바에 합쳐 보인다 (BossEvents)
    // - 한쪽이 쓰러지면 같은 전투 구역의 남은 쪽이 곧바로 광폭화한다 (EnrageAbility.Trigger)
    // - 몸 색(기술 예고 깜빡임, 광폭화 붉은 맥동)은 ElementRecolor.Tint로 준다. ElementRecolor가 매 프레임 색을 덮어쓰기 때문
    public class SlimeKnight : MonoBehaviour
    {
        public const string DisplayName = "쌍둥이 슬라임 기사";
        private const float NoticeInterval = 3f;
        private static readonly Color EnrageColor = new Color(1f, 0.45f, 0.4f);
        private static readonly List<SlimeKnight> knights = new List<SlimeKnight>();
        private static float lastNoticeTime = -100f;

        private HealthSystem health;
        private EnrageAbility enrage;
        private ElementRecolor recolor;
        private Color flashColor;
        private float flashUntil;
        private bool fallen;

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

            bool enraged = false;
            foreach (var other in knights)
            {
                if (other == this || other.fallen || other.transform.parent != transform.parent) continue;
                if (other.enrage == null || other.enrage.Active) continue;
                other.enrage.Trigger();
                enraged = true;
            }

            if (enraged) StageEvents.PostNotice("짝을 잃은 기사가 광폭화했다!");
        }

        private void Update()
        {
            if (recolor == null || fallen) return;

            var color = Color.white;
            if (enrage != null && enrage.Active)
                color = Color.Lerp(Color.white, EnrageColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
            if (Time.time < flashUntil)
                color = Color.Lerp(color, flashColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
            recolor.Tint = color;
        }

        private void OnDestroy()
        {
            knights.Remove(this);
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                BossEvents.Unregister(health);
            }
        }
    }
}
