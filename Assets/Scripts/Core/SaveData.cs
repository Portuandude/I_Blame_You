using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // PlayerPrefs에 남기는 저장 데이터: 설정값(개발자 모드, 볼륨)과 최고 기록(가장 빠른 클리어 시간, 최고 레벨).
    // 최고 기록은 개발자 모드(치트 사용 가능)와 일반 모드를 따로 둔다.
    public static class SaveData
    {
        private const string KeyDevMode = "Settings.DevMode";
        private const string KeyMusicVolume = "Settings.MusicVolume";
        private const string KeySfxVolume = "Settings.SfxVolume";
        private const string KeyBestClear = "Best.ClearTime";
        private const string KeyBestLevel = "Best.Level";

        private static bool settingsLoaded;

        // 처음 한 번만 저장된 설정을 GameSession/GameAudio에 적용한다 (없으면 지금 기본값 유지).
        public static void LoadSettings()
        {
            if (settingsLoaded) return;
            settingsLoaded = true;
            GameSession.DevMode = PlayerPrefs.GetInt(KeyDevMode, GameSession.DevMode ? 1 : 0) != 0;
            GameAudio.MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, GameAudio.MusicVolume));
            GameAudio.SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, GameAudio.SfxVolume));
        }

        public static void SaveSettings()
        {
            PlayerPrefs.SetInt(KeyDevMode, GameSession.DevMode ? 1 : 0);
            PlayerPrefs.SetFloat(KeyMusicVolume, GameAudio.MusicVolume);
            PlayerPrefs.SetFloat(KeySfxVolume, GameAudio.SfxVolume);
            PlayerPrefs.Save();
        }

        // 볼륨만 저장한다 (게임 중 일시정지 메뉴용: 이어하기로 바뀐 런 모드가 설정에 덮이지 않게).
        public static void SaveVolume()
        {
            PlayerPrefs.SetFloat(KeyMusicVolume, GameAudio.MusicVolume);
            PlayerPrefs.SetFloat(KeySfxVolume, GameAudio.SfxVolume);
            PlayerPrefs.Save();
        }

        private static string ModeKey(string key, bool devMode) => key + (devMode ? ".Dev" : ".Normal");

        // 가장 빠른 클리어 시간(초). 기록이 없으면 0.
        public static float BestClearTime(bool devMode) => PlayerPrefs.GetFloat(ModeKey(KeyBestClear, devMode), 0f);

        // 지금까지 도달한 가장 높은 레벨. 기록이 없으면 0.
        public static int BestLevel(bool devMode) => PlayerPrefs.GetInt(ModeKey(KeyBestLevel, devMode), 0);

        // 클리어 시간을 기록한다. 이전 기록보다 빠르면(또는 첫 기록이면) true.
        public static bool RecordClearTime(float seconds)
        {
            if (seconds <= 0f) return false;
            bool dev = GameSession.DevMode;
            float best = BestClearTime(dev);
            if (best > 0f && seconds >= best) return false;
            PlayerPrefs.SetFloat(ModeKey(KeyBestClear, dev), seconds);
            PlayerPrefs.Save();
            return true;
        }

        // 도달한 레벨을 기록한다. 이전 최고보다 높으면 true.
        public static bool RecordLevel(int level)
        {
            bool dev = GameSession.DevMode;
            if (level <= BestLevel(dev)) return false;
            PlayerPrefs.SetInt(ModeKey(KeyBestLevel, dev), level);
            PlayerPrefs.Save();
            return true;
        }

        // "12분 34초" 형식. 기록이 없으면 "-".
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "-";
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int rest = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes}분 {rest:00}초";
        }
    }
}
