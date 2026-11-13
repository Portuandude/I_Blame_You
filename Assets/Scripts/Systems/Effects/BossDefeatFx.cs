using UnityEngine;

namespace IBlameYou.Systems
{
    // 보스·중간보스를 마지막으로 쓰러뜨린 순간의 연출: 짧은 슬로모션 + 화면 흰 번쩍임 + 큰 별·하트 폭발 + 화면 가운데 "이름 격파!" 띠 (SkillHUD.Boss.cs가 LastName·LastTime을 읽어 그린다)
    // + 연쇄 작은 폭발 4번 + 배경음악 1초 정적 (GameAudio.SilenceMusic, 그 뒤 스테이지 곡이 다시 차오른다).
    // 슬로모션은 실제 시간으로 재고, 끝날 때 timeScale이 아직 이 연출의 값일 때만 1로 되돌린다
    // (그사이 일시정지·특성 창·유물 창·플레이어 사망 연출이 timeScale을 바꿨으면 그쪽이 되돌리게 둔다).
    public class BossDefeatFx : MonoBehaviour
    {
        public const float SlowScale = 0.3f;
        private const float SlowDuration = 0.35f;   // 실제 시간(초)
        private const float FlashDuration = 0.3f;   // 실제 시간(초)
        private const float FlashAlpha = 0.55f;
        private const float Lifetime = 1f;          // 실제 시간. 이 뒤에 오브젝트를 치운다
        private const float BurstScale = 2.4f;
        private const float MusicSilence = 1f;      // 실제 시간(초). 보스 곡이 뚝 끊긴 정적 뒤 스테이지 곡이 다시 차오른다

        // 마지막 격파 (실제 시간). 화면 HUD가 읽는다 — 보스 체력바 마무리·가운데 격파 띠 (SkillHUD.Boss.cs).
        public static string LastName { get; private set; }
        public static float LastTime { get; private set; } = -100f;

        // 연쇄 작은 폭발: 큰 폭발 뒤 몸 둘레(고정 자리, 난수를 쓰지 않는다)에서 "퐁퐁" 4번 (실제 시간).
        private const float ChainInterval = 0.12f;
        private static readonly Vector2[] ChainOffsets =
        {
            new Vector2(-0.9f, 0.5f), new Vector2(0.8f, 0.9f), new Vector2(-0.5f, -0.4f), new Vector2(0.7f, -0.2f),
        };

        // 도메인 다시 불러오기를 끈 에디터에서도 이전 플레이의 격파가 남지 않게.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLast()
        {
            LastName = null;
            LastTime = -100f;
        }

        private float startTime;
        private bool slowing;
        private Vector3 origin;
        private int chainFired;

        // position: 보스가 쓰러진 자리 (몸 가운데쯤).
        public static void Play(Vector3 position, string bossName)
        {
            var go = new GameObject("BossDefeatFx");
            var fx = go.AddComponent<BossDefeatFx>();
            fx.startTime = Time.unscaledTime;
            fx.origin = position;
            LastName = bossName;
            LastTime = Time.unscaledTime;

            if (Mathf.Approximately(Time.timeScale, 1f))
            {
                Time.timeScale = SlowScale;
                fx.slowing = true;
            }

            var frames = EffectLibrary.LoadRuntimeFrames("Effects/ClearBurst");
            if (frames != null)
                EffectLibrary.PlayOneShot(position, frames, 14f, Color.clear, 0f, false, BurstScale, sortingOrder: 8);
            VfxPlayer.DeathPop(position, 2f);
            GameAudio.SilenceMusic(MusicSilence);
            GameAudio.PlaySfx("BossDefeat", 0.9f, 0f); // 격파 전용 "쾅" + 반짝 아르페지오 (Tools/AudioGen BossDefeat)
        }

        private float Elapsed => Time.unscaledTime - startTime;

        private void Update()
        {
            if (slowing && Elapsed >= SlowDuration) EndSlow();
            while (chainFired < ChainOffsets.Length && Elapsed >= ChainInterval * (chainFired + 1))
            {
                var offset = ChainOffsets[chainFired];
                VfxPlayer.DeathPop(origin + new Vector3(offset.x, offset.y, 0f), 0.9f);
                GameAudio.PlaySfx("SquishHit", 0.45f);
                chainFired++;
            }
            if (Elapsed >= Lifetime) Destroy(gameObject);
        }

        private void EndSlow()
        {
            if (!slowing) return;
            slowing = false;
            if (Mathf.Approximately(Time.timeScale, SlowScale)) Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            EndSlow(); // 씬 정리·재시작으로 먼저 사라져도 느린 시간이 남지 않게
        }

        private void OnGUI()
        {
            float alpha = FlashAlpha * (1f - Mathf.Clamp01(Elapsed / FlashDuration));
            if (alpha <= 0f) return;
            GUI.depth = -100; // HUD 위에
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
