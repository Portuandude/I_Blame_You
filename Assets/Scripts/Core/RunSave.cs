using System;
using UnityEngine;

namespace IBlameYou.Core
{
    // 런 진행 저장: 휴식 청크의 체크포인트를 처음 켤 때 한 번 저장하고, 시작 화면의 "이어하기"로 그 체크포인트에서 다시 시작한다.
    // 쓰러지거나 클리어하거나 새 런을 시작하면 지운다 (체크포인트 직전으로 되돌려 쓰러짐을 무르는 용도가 아님).
    // PlayerPrefs 한 칸에 JSON으로 넣는다. 형식이 바뀌면 Version을 올리고, 버전이 다른 저장은 버린다.
    public static class RunSave
    {
        public const int Version = 1;
        private const string Key = "Run.Save";

        [Serializable]
        public class Data
        {
            public int version;
            public int seed;
            public float targetSeconds;
            public bool devMode;
            public int checkpointChunk;
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
            if (data == null) return;
            data.version = Version;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
