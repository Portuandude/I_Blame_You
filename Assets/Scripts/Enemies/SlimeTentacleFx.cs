using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 킹 촉수(SlimeKingTentacle) 연출 (그림·소리만, 속도·사거리·둔화·그랩 시간은 그대로, 2026-11-13).
    // - Lash: 한 번의 촉수 패턴에서 첫 가닥이 나갈 때 "휙" (TentacleLash) + 킹 몸 쪽에 보라 젤리 방울 둘
    // - Slap: 촉수 끝이 벽·바닥에 부딪혀 되감길 때 끝에서 보라 방울 셋 + "철퍽" (TentacleSlap)
    // - Grab: 플레이어에 붙을 때 0.05초 히트스톱 + 플레이어 둘레 보라 고리 + "찰싹" (TentacleGrab)
    // - TetherPulse: 붙은 동안 끝 구슬이 두근거리고 그랩이 가까울수록 빨라진다. 마지막 1초엔 선이 붉게 깜빡인다
    // - Snap: 멀어져서 끊어내면 끊긴 자리에서 보라 방울 다섯 + 플레이어 둘레 초록 고리 + "뽁" (TentacleSnap)
    // 소리는 화면 안에서만 난다. 네 가닥이 한꺼번에 벽에 닿아도 소리가 겹쳐 커지지 않게 짧은 간격을 둔다.
    public static class SlimeTentacleFx
    {
        public static readonly Color Purple = new Color(0.75f, 0.5f, 1f, 0.95f);
        public static readonly Color Danger = new Color(1f, 0.3f, 0.3f, 0.95f);
        public static readonly Color FreeColor = new Color(0.6f, 1f, 0.6f, 0.95f);

        public const float GrabHitStop = 0.05f;
        public const float BlinkWindow = 1f;   // 그랩 직전 이 시간 동안 선이 깜빡인다
        private const float LashCooldown = 0.3f; // 한 패턴의 가닥 4개(0.08초 간격)가 "휙" 하나로 들리게
        private const float SlapCooldown = 0.12f;

        private static float lastLash = -10f;
        private static float lastSlap = -10f;

        public static void Lash(Vector3 origin, Vector2 direction)
        {
            float now = Time.unscaledTime;
            bool first = now < lastLash || now - lastLash >= LashCooldown;
            if (first)
            {
                lastLash = now;
                if (SlimeSplitFx.OnScreen(origin)) GameAudio.PlaySfx("TentacleLash", 0.8f);
            }

            // 촉수가 나가는 쪽으로 작은 방울 둘 (가닥마다)
            for (int i = 0; i < 2; i++)
            {
                var v = (Vector3)(direction.normalized * Random.Range(2f, 3.5f)) + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.5f, 1.5f), 0f);
                SlimeSplitFx.Drop(origin, v, Purple, Random.Range(0.1f, 0.16f));
            }
        }

        public static void Slap(Vector3 tip, Vector2 direction)
        {
            float now = Time.unscaledTime;
            if ((now < lastSlap || now - lastSlap >= SlapCooldown) && SlimeSplitFx.OnScreen(tip))
            {
                lastSlap = now;
                GameAudio.PlaySfx("TentacleSlap", 0.75f);
            }

            // 부딪힌 면에서 되튀듯이: 진행 방향의 반대쪽 반원으로 퍼진다
            Vector2 back = -direction.normalized;
            float baseAngle = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 3; i++)
            {
                float angle = (baseAngle + Mathf.Lerp(-60f, 60f, i / 2f) + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                var v = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(3f, 5f) + Vector3.up * 1.5f;
                SlimeSplitFx.Drop(tip, v, Purple, Random.Range(0.12f, 0.2f));
            }
        }

        public static void Grab(Transform player, Vector3 center)
        {
            if (player == null) return;
            HitStop.Play(GrabHitStop);
            VfxPlayer.ManaRing(player, player.InverseTransformPoint(center), Purple, 1.4f);
            if (SlimeSplitFx.OnScreen(center)) GameAudio.PlaySfx("TentacleGrab", 0.85f);
        }

        // 붙은 동안 매 프레임. tetherTime: 붙은 뒤 지난 시간, grabTime: 그랩까지 걸리는 시간.
        // 끝 구슬 크기(baseScale 기준)와 선 색을 정한다. danger 색 섞기(보라→빨강)는 SlimeKingTentacle이 하던 그대로.
        public static void TetherPulse(Transform tip, float baseScale, LineRenderer line, float tetherTime, float grabTime)
        {
            float danger = Mathf.Clamp01(tetherTime / grabTime);
            float speed = Mathf.Lerp(6f, 22f, danger);
            float pulse = 1f + Mathf.Lerp(0.12f, 0.3f, danger) * Mathf.Max(0f, Mathf.Sin(tetherTime * speed));
            if (tip != null) tip.localScale = new Vector3(baseScale * pulse, baseScale * pulse, 1f);
            if (line == null) return;

            var end = Color.Lerp(Purple, Danger, danger);
            var start = new Color(0.55f, 0.3f, 0.9f, 0.95f);
            bool blink = tetherTime >= grabTime - BlinkWindow && Mathf.Repeat(tetherTime, 0.16f) < 0.08f;
            if (blink)
            {
                start = Danger;
                end = Color.white;
            }

            line.startColor = start;
            line.endColor = end;
        }

        public static void Snap(Vector3 breakPoint, Transform player, Vector3 playerCenter)
        {
            if (SlimeSplitFx.OnScreen(breakPoint) || SlimeSplitFx.OnScreen(playerCenter)) GameAudio.PlaySfx("TentacleSnap", 0.85f);
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(20f, 160f, i / 4f) * Mathf.Deg2Rad;
                var v = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(3.5f, 6f);
                SlimeSplitFx.Drop(breakPoint, v, Purple, Random.Range(0.14f, 0.24f));
            }

            if (player != null) VfxPlayer.ManaRing(player, player.InverseTransformPoint(playerCenter), FreeColor, 1.6f);
        }
    }
}
