using System;
using UnityEngine;

namespace IBlameYou.Core
{
    // 런 진행 저장: 쉼터에서만 저장한다 (2026-11-24 사람 결정, Chunks/RestRule). 시작 화면 "이어하기"는 저장한 쉼터 다음 스테이지 첫 방에서 시작.
    // 쓰러지거나 스테이지 5를 깨거나 새 런을 시작하면 지운다. 스테이지 도중에 나가도 쉼터 저장은 남는다 (그 스테이지 처음부터 다시).
    // 저장 슬롯(GameSession.SaveSlot, SaveSlotRule)마다 키가 따로다. 슬롯 1은 예전 키 그대로.
    // PlayerPrefs 한 칸에 JSON으로 넣는다. 형식이 바뀌면 Version을 올리고, 버전이 다른 저장은 버린다.
    public static class RunSave
    {
        public const int Version = 6; // 6: 쉼터 저장 (스테이지 시작·골드·장비·마법 집계를 넘긴다, 2026-11-24). 5 5: 방 구성 14~18방·마을 1(2026-11-14 23:37). 4: 방 구성 변경(2026-11-14, 5×4 격자·이동 방 삭제). 3: 넓은 방(2026-11-12)으로 같은 시드의 방 배치·원점 간격이 바뀌어 예전 저장은 읽지 않는다
        private const string BaseKey = "Run.Save";
        private static string Key => SaveData.SlotKey(BaseKey);

        [Serializable]
        public class Data
        {
            public int version;
            public int seed;                // 판 시드 (스테이지 맵 시드는 RunFlowRule.StageSeed(seed, stage))
            public bool atStageStart;       // 쉼터 저장: 그 스테이지 첫 방에서 시작 (방·위치·records는 쓰지 않는다)
            public float targetSeconds;
            public bool devMode;
            public int checkpointChunk;     // 방 격자 모드에서는 방 번호
            public int stage;               // 스테이지 번호 (0 = 옛 저장 → 1)
            public bool roomGrid;           // 방 격자 모드로 저장한 런인지 (모드가 다르면 같은 시드라도 맵이 달라 이어 할 수 없다)
            public float spawnX;
            public float spawnY;
            public float elapsed;           // 런 시작부터 저장까지 걸린 시간(초)
            public float progress;          // 저장 시점 방 깊이 (StageEvents.Depth, 이어할 때 곡 고르기. 화면에는 안 보임. 예전 저장과 맞추려고 이름 유지)

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

            // 스테이지를 넘어 이어지는 판 기록 (쉼터 저장, Version 6)
            public float maxHealth;         // 쉼터 창 표시용 (복원은 장비·유물·탑이 다시 만든 최대치를 쓴다)
            public int gold;
            public int goldEarned;
            public int goldSpent;
            public int heartDust;           // 이번 판에 얻은 영혼석 (결과 창 표시, 누적 저장은 이미 SaveData에)
            public int picks;
            public int[] spellCasts = new int[0];
            public int[] equipment = new int[0]; // 낀 장비 EquipmentId

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
            catch (Exception) // 깨진 저장이 어떤 예외를 내도 시작 메뉴가 멈추지 않게 (이어하기만 안 보인다)
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
