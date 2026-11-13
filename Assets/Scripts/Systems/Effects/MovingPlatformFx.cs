using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 움직이는 발판 도착·출발 연출 (그림·소리만, 속도·멈춤 시간·판정은 MovingPlatform 그대로, 2026-11-13).
    // - Arrive: 양 끝에 닿아 멈추는 순간 진행하던 쪽 끝 아래 작은 먼지 + 카메라 가까이면 작은 "탁" (PlatformStop, 0.2초 간격 제한)
    // - Depart: 멈춤이 끝나 떠나는 순간 뒤쪽 끝 아래 작은 먼지
    // 판자가 눌렸다 펴지는 것·출발 직전 밝아지는 것은 MovingPlatform이 직접 그린다 (판자 그림을 가진 곳).
    public static class MovingPlatformFx
    {
        private const float StopSfxGap = 0.2f;
        private static float lastStopSfx = -10f;

        // center: 발판 가운데, width: 칸 수, thickness: 발판 두께, side: 닿은 쪽 (+1 오른쪽 끝, -1 왼쪽 끝).
        // sound: false면 소리 없이 먼지만 (곧 잠드는 스위치 발판은 SwitchPlatformFx.Sleep 종소리가 대신 난다).
        public static void Arrive(Vector3 center, int width, float thickness, float side, bool sound = true)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            if (sound && (Time.time - lastStopSfx >= StopSfxGap || Time.time < lastStopSfx))
            {
                lastStopSfx = Time.time;
                GameAudio.PlaySfx("PlatformStop", 0.45f);
            }
            VfxPlayer.Dust(EdgeFoot(center, width, thickness, side), 0.25f);
        }

        // side: 떠나는 쪽의 반대 (뒤쪽 끝, +1 오른쪽 / -1 왼쪽).
        public static void Depart(Vector3 center, int width, float thickness, float side)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            VfxPlayer.Dust(EdgeFoot(center, width, thickness, side), 0.2f);
        }

        private static Vector3 EdgeFoot(Vector3 center, int width, float thickness, float side) =>
            new Vector3(center.x + side * (width / 2f - 0.2f), center.y - thickness / 2f, 0f);
    }
}
