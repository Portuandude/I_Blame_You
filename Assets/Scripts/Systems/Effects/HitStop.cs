using UnityEngine;

namespace IBlameYou.Systems
{
    // 히트스톱 (전투 손맛, 2026-11-12): 플레이어 공격이 적을 쓰러뜨리거나 치명타가 들어간 순간 시간이 아주 잠깐 거의 멈춘다.
    // - 길이는 실제 시간으로 잰다. timeScale이 1일 때만 걸고, 끝날 때 아직 이 값(StopScale)일 때만 1로 되돌린다
    //   (그사이 일시정지·특성 창·유물 창·보스 격파 슬로모션·사망 연출이 timeScale을 바꿨으면 그쪽이 되돌리게 둔다. BossDefeatFx와 같은 규칙).
    // - 이미 멈춰 있는 중에 더 긴 요청이 오면 끝나는 시각만 늦춘다. 광역 마법이 여러 마리를 한꺼번에 쓰러뜨려도
    //   쌓이지 않게, 한 번 끝난 뒤 Cooldown 동안은 새로 걸지 않는다.
    public class HitStop : MonoBehaviour
    {
        public const float StopScale = 0.05f;
        public const float KillDuration = 0.05f;   // 일반 적을 쓰러뜨렸을 때 (실제 시간, 초)
        public const float CritDuration = 0.03f;   // 치명타 (쓰러뜨리지 않았을 때)
        public const float EliteDuration = 0.09f;  // 정예 (어미 슬라임·고블린 대전사)를 쓰러뜨렸을 때
        public const float Cooldown = 0.12f;       // 끝난 뒤 이 동안은 새로 걸지 않는다

        private static HitStop instance;
        private static float lastEnd = -10f;

        private float endTime;

        public static bool Active => instance != null;

        public static void Play(float duration)
        {
            if (duration <= 0f) return;
            float now = Time.unscaledTime;

            if (instance != null)
            {
                instance.endTime = Mathf.Max(instance.endTime, now + duration);
                return;
            }

            // (도메인 리로드 없이 다시 플레이해 unscaledTime이 작아져도 막히지 않게 now >= lastEnd도 본다)
            if (now >= lastEnd && now - lastEnd < Cooldown) return;
            if (!Mathf.Approximately(Time.timeScale, 1f)) return;

            var go = new GameObject("HitStop");
            instance = go.AddComponent<HitStop>();
            instance.endTime = now + duration;
            Time.timeScale = StopScale;
        }

        private void Update()
        {
            if (Time.unscaledTime >= endTime) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 씬 정리·재시작으로 먼저 사라져도 멈춘 시간이 남지 않게
            if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
            if (instance == this) instance = null;
            lastEnd = Time.unscaledTime;
        }
    }
}
