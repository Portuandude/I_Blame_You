using System;
using UnityEngine;

namespace IBlameYou.Core
{
    // 런 진행 저장: 시작 화면의 "이어하기"로 저장한 방에서 다시 시작한다. 쓰러지거나 클리어하거나 새 런을 시작하면 지운다.
    // 2026-11-10 체크포인트 제거(사람 결정)로 지금은 저장하는 곳이 없다 (스테이지가 하나뿐이라 시작 지점 이어하기는 새 런과 같음).
    // 스테이지 2가 생기면 스테이지 전환 때 저장하는 용도로 남긴다. Version 2: 옛 체크포인트 저장(1)은 읽지 않는다 → 이어하기 버튼 안 나옴.
    // PlayerPrefs 한 칸에 JSON으로 넣는다. 형식이 바뀌면 Version을 올리고, 버전이 다른 저장은 버린다.
    public static class RunSave
    {
        public const int Version = 3; // 3: 넓은 방(2026-11-12)으로 같은 시드의 방 배치·원점 간격이 바뀌어 예전 저장은 읽지 않는다
        private const string Key = "Run.Save";

        [Serializable]
        public class Data
        {
            public int version;
            public int seed;
            public float targetSeconds;
            public bool devMode;
            public int checkpointChunk;     // 방 격자 모드에서는 방 번호
            public bool roomGrid;           // 방 격자 모드로 저장한 런인지 (모드가 다르면 같은 시드라도 맵이 달라 이어 할 수 없다)
            public float spawnX;
            public float spawnY;
            public float elapsed;           // 런 시작부터 저장까지 걸린 시간(초)
            public float progress;          // 저장 시점 진행도 (시작 화면 표시용)

            public int level;
            public float experience;
            public float totalExperience;
            public int points;              // 아직 찍지 않은 특성 포인트
            public int[] traitRanks = new int[0];
            public int[] relics = new int[0];
            public float health;
            public int deathSaves = -1;     // 남은 "쓰러짐 버티기" 횟수 (생명 20). -1이면 모름(특성대로 둔다)
            public int freeFalls = -1;      // 남은 무료 낙하 복귀 횟수 (되감기 모래시계). -1이면 모름(옛 저장 → 유물 기본값)

            public int kills;
            public float damageTaken;
            public int arenasCleared;
            public int gatesOpened;

            public IBlameYou.Systems.StageRecords records = new IBlameYou.Systems.StageRecords();
        }

        public static bool Exists => Load() != null;

        // 저장이 없거나 읽을 수 없으면(형식이 다름, 깨짐) null.
        public static Data Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<Data>(json);
                return data != null && data.version == Version ? data : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static void Save(Data data)
        {
            if (GameSession.FeatureTest) return; // 기능 테스트 모드는 이어하던 런을 건드리지 않는다
            if (data == null) return;
            data.version = Version;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        // 일시정지에서 시작 화면으로 나갈 때: 걸린 시간은 되돌아가지 않게 지금 시간으로 올린다
        // (저장 시점으로 시간을 되감아 최고 클리어 기록이 짧아지는 것 방지).
        public static void UpdateElapsed(float elapsed)
        {
            if (GameSession.FeatureTest) return; // 기능 테스트 모드는 이어하던 런을 건드리지 않는다
            var data = Load();
            if (data == null || elapsed <= data.elapsed) return;
            data.elapsed = elapsed;
            Save(data);
        }

        public static void Clear()
        {
            if (GameSession.FeatureTest) return; // 기능 테스트 모드는 이어하던 런을 건드리지 않는다
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
