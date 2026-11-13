using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 슬라임 도약 덮치기 연출 (그림·소리만, 도약 시간·속도·피해는 SlimeController 그대로, 2026-11-13).
    // - Hop: 뛰어오르는 순간 발밑 작은 먼지 + "뾱" (SlimeHop)
    // - Landed: 실제로 땅에 닿은 순간 납작하게 눌림(EnemySquash.Land) + 먼지 + 작은 젤리 방울 + "퐁" (SlimePlop)
    //   정예·중간보스(어미 슬라임, 슬라임 기사)는 더 무겁게: 낮은 음 + 작은 충격파 + 방울 더 많이 + 약한 화면 흔들림
    // 소리는 화면 안일 때만, 여러 마리가 한꺼번에 뛰거나 떨어져도 0.08초에 한 번.
    public static class SlimeLandFx
    {
        private static readonly Color DropColor = new Color(0.85f, 1f, 0.85f, 0.85f);
        private const float SfxGap = 0.08f;
        private static float nextHopSfx;
        private static float nextPlopSfx;

        public static void Hop(Vector3 feet, bool heavy)
        {
            VfxPlayer.Dust(feet, heavy ? 0.7f : 0.4f);
            if (!SlimeSplitFx.OnScreen(feet) || Time.time < nextHopSfx) return;
            nextHopSfx = Time.time + SfxGap;
            GameAudio.PlaySfxPitched("SlimeHop", heavy ? 0.8f : 1f, heavy ? 0.6f : 0.45f);
        }

        public static void Landed(GameObject body, Vector3 feet, bool heavy)
        {
            EnemySquash.For(body).Land();
            VfxPlayer.Dust(feet, heavy ? 1f : 0.5f);

            int drops = heavy ? 6 : 2;
            Color color = heavy ? SlimeSplitFx.GooColor : DropColor;
            for (int i = 0; i < drops; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var v = new Vector3(side * Random.Range(1.5f, heavy ? 4f : 2.5f), Random.Range(1.5f, heavy ? 3.5f : 2.5f), 0f);
                SlimeSplitFx.Drop(feet + Vector3.up * 0.1f, v, color, Random.Range(0.08f, heavy ? 0.16f : 0.11f));
            }

            bool onScreen = SlimeSplitFx.OnScreen(feet);
            if (heavy)
            {
                VfxPlayer.Shockwave(feet, 0.6f);
                if (onScreen) IBlameYou.Core.CameraFollow.ShakeMain(0.08f, 0.05f);
            }

            if (!onScreen || Time.time < nextPlopSfx) return;
            nextPlopSfx = Time.time + SfxGap;
            GameAudio.PlaySfxPitched("SlimePlop", heavy ? 0.75f : 1f, heavy ? 0.7f : 0.5f);
        }
    }
}
