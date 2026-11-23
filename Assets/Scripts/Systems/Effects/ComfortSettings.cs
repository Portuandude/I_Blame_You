using UnityEngine;

namespace IBlameYou.Systems
{
    // 눈 편의 설정 (2026-11-23 스팀 출시 준비): 화면 흔들림 세기·화면 번쩍임 세기. 0~100%를 25% 단위로 고른다.
    // 화면 설정 창(Core/DisplaySettingsWindow)에서 바꾸고 PlayerPrefs에 저장. 처음 쓸 때 읽는다 (저장이 없으면 100% = 지금까지와 같음).
    // 흔들림: CameraFollow가 흔들 때 곱한다. 번쩍임: ScreenFlash.Pulse(마법 적중 흰 번쩍임)에 곱하고,
    // 가장자리 경고(EdgeWarn — 정예·보스 등장 등 알림)는 정보라 0%여도 40% 세기는 남긴다. 방 전환 어둡게 덮기는 장면 전환이라 그대로.
    public static class ComfortSettings
    {
        private const string KeyShake = "Settings.ShakePercent";
        private const string KeyFlash = "Settings.FlashPercent";
        public const int Step = 25;
        private const float EdgeFloor = 0.4f;

        private static bool loaded;
        private static int shakePercent = 100;
        private static int flashPercent = 100;

        public static int ShakePercent { get { Load(); return shakePercent; } }
        public static int FlashPercent { get { Load(); return flashPercent; } }
        public static float ShakeScale => ShakePercent / 100f;
        public static float FlashScale => FlashPercent / 100f;
        public static float EdgeScale => Mathf.Lerp(EdgeFloor, 1f, FlashScale);

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            shakePercent = Sanitize(PlayerPrefs.GetInt(KeyShake, 100));
            flashPercent = Sanitize(PlayerPrefs.GetInt(KeyFlash, 100));
        }

        // 0~100, 25 단위로 반올림
        public static int Sanitize(int percent)
        {
            int clamped = Mathf.Clamp(percent, 0, 100);
            return Mathf.RoundToInt(clamped / (float)Step) * Step;
        }

        public static void Set(int shake, int flash)
        {
            Load();
            shakePercent = Sanitize(shake);
            flashPercent = Sanitize(flash);
            PlayerPrefs.SetInt(KeyShake, shakePercent);
            PlayerPrefs.SetInt(KeyFlash, flashPercent);
            PlayerPrefs.Save();
        }
    }
}
