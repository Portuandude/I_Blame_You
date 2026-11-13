using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 가시·불기둥 함정 연출 (그림·소리만, 피해·튕김·쿨다운·불 시간은 SpikeTrap·FlameJetTrap 그대로, 2026-11-13).
    // - SpikeStab: 가시를 밟은 자리에서 흰·붉은 쇳조각이 튀고 "찡" (SpikeStab), 튕겨 오르는 발밑 작은 먼지
    // - FlameTouch: 불기둥에 닿은 플레이어 몸에서 주황 불똥이 튀고 "치익" (FlameSizzle)
    // - FlameBurst: 불이 솟는 순간 분출구에서 주황·노랑 불똥이 위로 터진다
    // - FlameOut: 불이 꺼질 때 분출구에서 남은 불씨 몇 개가 작게 튄다
    // 소리는 0.1초에 한 번 (협동 둘이 같이 밟아도 겹치지 않게).
    public static class TrapHitFx
    {
        private static readonly Color SteelColor = new Color(0.92f, 0.94f, 1f, 0.95f);
        private static readonly Color BloodRed = new Color(1f, 0.35f, 0.35f, 0.9f);
        private static readonly Color EmberOrange = new Color(1f, 0.55f, 0.15f, 0.95f);
        private static readonly Color EmberYellow = new Color(1f, 0.88f, 0.35f, 0.95f);
        private const float SfxGap = 0.1f;

        private static float nextSpikeSfx;
        private static float nextSizzleSfx;

        // feet: 밟은 플레이어 발밑 (가시 끝 높이쯤).
        public static void SpikeStab(Vector3 feet)
        {
            for (int i = 0; i < 5; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var v = new Vector3(side * Random.Range(0.8f, 2.5f), Random.Range(2f, 4.5f), 0f);
                SlimeSplitFx.Drop(feet, v, i < 3 ? SteelColor : BloodRed, Random.Range(0.05f, 0.09f));
            }
            VfxPlayer.Dust(feet, 0.35f);

            if (!SlimeSplitFx.OnScreen(feet) || Time.time < nextSpikeSfx) return;
            nextSpikeSfx = Time.time + SfxGap;
            GameAudio.PlaySfx("SpikeStab", 0.55f);
        }

        // body: 불에 닿은 플레이어 몸 가운데.
        public static void FlameTouch(Vector3 body)
        {
            for (int i = 0; i < 4; i++)
            {
                var v = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(2f, 4f), 0f);
                SlimeSplitFx.Drop(body + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.3f, 0.3f), 0f), v,
                    i % 2 == 0 ? EmberOrange : EmberYellow, Random.Range(0.05f, 0.09f));
            }

            if (!SlimeSplitFx.OnScreen(body) || Time.time < nextSizzleSfx) return;
            nextSizzleSfx = Time.time + SfxGap;
            GameAudio.PlaySfx("FlameSizzle", 0.5f);
        }

        // vent: 분출구 윗면 가운데.
        public static void FlameBurst(Vector3 vent)
        {
            if (!SlimeSplitFx.OnScreen(vent)) return; // 그림만이라도 화면 밖이면 만들지 않는다
            for (int i = 0; i < 6; i++)
            {
                var v = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(4f, 7f), 0f);
                SlimeSplitFx.Drop(vent, v, i % 2 == 0 ? EmberOrange : EmberYellow, Random.Range(0.06f, 0.11f));
            }
        }

        public static void FlameOut(Vector3 vent)
        {
            if (!SlimeSplitFx.OnScreen(vent)) return;
            for (int i = 0; i < 3; i++)
            {
                var v = new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 3f), 0f);
                SlimeSplitFx.Drop(vent, v, EmberOrange, Random.Range(0.04f, 0.07f));
            }
        }
    }
}
