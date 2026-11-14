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
        private const string KeyBestGold = "Best.Gold"; // 한 판에 모은 골드(RunStats.GoldEarned) 최고
        private const string KeyRecentClears = "Recent.Clears";
        private const string KeyRecentDefeats = "Recent.DefeatRooms"; // 2026-11-12 진행도(%) "Recent.Defeats" → 방 거리. 예전 키는 읽지 않는다
        private const string KeyRelicCodex = "Codex.Relics";      // 0~30번 (예전 int 저장 그대로)
        private const string KeyRelicCodexHigh = "Codex.Relics2"; // 31~61번 (2026-11-12 유물 31종 한도 풀기)

        private static bool settingsLoaded;

        // 처음 한 번만 저장된 설정을 GameSession/GameAudio에 적용한다 (없으면 지금 기본값 유지).
        public static void LoadSettings()
        {
            if (settingsLoaded) return;
            settingsLoaded = true;
            GameSession.DevMode = PlayerPrefs.GetInt(KeyDevMode, GameSession.DevMode ? 1 : 0) != 0;
            GameAudio.MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, GameAudio.MusicVolume));
            GameAudio.SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, GameAudio.SfxVolume));
            DisplaySettings.Load(); // 해상도·창 모드 적용, 16:9 레터박스
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

        // 한 판에 모은 골드(쓴 것 포함, RunStats.GoldEarned) 최고. 기록이 없으면 0.
        public static int BestGold(bool devMode) => PlayerPrefs.GetInt(ModeKey(KeyBestGold, devMode), 0);

        // 이번 판에 모은 골드를 기록한다. 이전 최고보다 많으면 true. 기능 테스트 모드는 기록하지 않는다.
        public static bool RecordGold(int earned)
        {
            if (GameSession.FeatureTest) return false;
            bool dev = GameSession.DevMode;
            if (earned <= BestGold(dev)) return false;
            PlayerPrefs.SetInt(ModeKey(KeyBestGold, dev), earned);
            PlayerPrefs.Save();
            return true;
        }

        // 클리어 시간을 기록한다. 이전 기록보다 빠르면(또는 첫 기록이면) true.
        public static bool RecordClearTime(float seconds)
        {
            if (GameSession.FeatureTest) return false; // 기능 테스트 모드는 기록하지 않는다
            if (seconds <= 0f) return false;
            bool dev = GameSession.DevMode;
            float best = BestClearTime(dev);
            if (best > 0f && seconds >= best) return false;
            PlayerPrefs.SetFloat(ModeKey(KeyBestClear, dev), seconds);
            PlayerPrefs.Save();
            return true;
        }

        // 최근 클리어 기록 5개 (걸린 시간·목표 시간·방 수, ClearPace.AddRecent 형식). 모드별로 따로.
        public static void RecordRecentClear(float seconds, float targetSeconds, int rooms)
        {
            if (GameSession.FeatureTest) return;
            if (seconds <= 0f) return;
            string key = ModeKey(KeyRecentClears, GameSession.DevMode);
            PlayerPrefs.SetString(key, ClearPace.AddRecent(PlayerPrefs.GetString(key, ""), seconds, targetSeconds, rooms));
            PlayerPrefs.Save();
        }

        // 시작 화면용 "최근 N판 평균 ..." 한 줄. 기록이 없으면 빈 문자열.
        public static string RecentClearSummary(bool devMode) =>
            ClearPace.RecentSummary(PlayerPrefs.GetString(ModeKey(KeyRecentClears, devMode), ""));

        // 최근 쓰러진 곳 5개 (그 판에 간 가장 먼 방 거리, ClearPace.AddRecentDefeat 형식). 모드별로 따로. 어려운 구간 찾기용.
        // 방 격자가 아닌 스테이지(거리 -1)와 기능 테스트는 기록하지 않는다.
        public static void RecordRecentDefeat(int farthest)
        {
            if (GameSession.FeatureTest || farthest < 0) return;
            string key = ModeKey(KeyRecentDefeats, GameSession.DevMode);
            PlayerPrefs.SetString(key, ClearPace.AddRecentDefeat(PlayerPrefs.GetString(key, ""), farthest));
            PlayerPrefs.Save();
        }

        // 시작 화면용 "최근 쓰러진 곳 N판 평균 시작 방에서 M칸까지" 한 줄. 기록이 없으면 빈 문자열.
        public static string RecentDefeatSummary(bool devMode) =>
            ClearPace.RecentDefeatSummary(PlayerPrefs.GetString(ModeKey(KeyRecentDefeats, devMode), ""));

        // 최근 쓰러진 판들이 간 가장 먼 방 거리 평균. 기록이 없으면 -1. 게임오버 창 "평소보다 더/덜 갔다" 비교용 (기록하기 전에 읽는다).
        public static int RecentDefeatAverage(bool devMode) =>
            ClearPace.RecentDefeatAverage(PlayerPrefs.GetString(ModeKey(KeyRecentDefeats, devMode), ""));

        // 유물 도감: 한 번이라도 고른 유물 (RelicCodex 비트 마스크). 모드별로 따로.
        public static long RelicCodexMask(bool devMode) =>
            RelicCodex.Join(PlayerPrefs.GetInt(ModeKey(KeyRelicCodex, devMode), 0), PlayerPrefs.GetInt(ModeKey(KeyRelicCodexHigh, devMode), 0));

        // 유물을 고르면 도감에 남긴다. 처음 고른 유물이면 true.
        public static bool RecordRelic(int relicId)
        {
            if (GameSession.FeatureTest) return false; // 기능 테스트 모드는 기록하지 않는다
            bool dev = GameSession.DevMode;
            long mask = RelicCodexMask(dev);
            if (RelicCodex.Has(mask, relicId)) return false;
            long next = RelicCodex.With(mask, relicId);
            PlayerPrefs.SetInt(ModeKey(KeyRelicCodex, dev), RelicCodex.Low(next));
            PlayerPrefs.SetInt(ModeKey(KeyRelicCodexHigh, dev), RelicCodex.High(next));
            PlayerPrefs.Save();
            return true;
        }

        public static string RelicCodexSummary(bool devMode, int total) => RelicCodex.Summary(RelicCodexMask(devMode), total);

        public static bool RelicCodexComplete(bool devMode, int total) => RelicCodex.Complete(RelicCodexMask(devMode), total);

        // 도달한 레벨을 기록한다. 이전 최고보다 높으면 true.
        public static bool RecordLevel(int level)
        {
            if (GameSession.FeatureTest) return false; // 기능 테스트 모드는 기록하지 않는다
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
