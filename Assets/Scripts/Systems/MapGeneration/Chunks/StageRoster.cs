using System;

namespace IBlameYou.Systems
{
    // 스테이지별 적 구성 (2026-11-24 사람 결정 C1·OpenDecisions F5·F6).
    // 방 지형·적 자리는 스테이지 1과 같은 생성기로 만들고, 스테이지 2+는 만들어진 적 종류만 이 규칙으로 바꿔 끼운다.
    // 그래서 스테이지 1 결과(같은 시드 = 같은 판)는 전혀 바뀌지 않고, 지형 난수도 건드리지 않는다.
    //  - 스테이지 2 숲: 슬라임 없음. 고블린 + 코볼트, 코볼트가 상위라 깊을수록(난이도↑) 코볼트가 많다.
    //    그물 코볼트(원거리)는 궁수 자리에서 KoboldNetterMinDifficulty 이상일 때만. 정예 마더 슬라임 → 고블린 대전사.
    //    보스 슬라임 킹 → 코볼트 족장 + 와르그 둘. 중간보스는 후보 미정이라 스테이지 1 것 그대로 (BLOCKED, 사람 결정).
    public static class StageRoster
    {
        public const int FirstStage = 1;
        public const int LastStage = 5;   // 한 판 = 스테이지 1→5 (A1·S1). 3~5 테마는 미정이라 지금은 숲 구성을 쓴다
        public const int ForestStage = 2;

        public const float KoboldChanceMin = 0.25f;  // 난이도 0에서 근접 적이 코볼트일 확률
        public const float KoboldChanceMax = 0.65f;  // 난이도 1에서
        public const float KoboldNetterMinDifficulty = 0.3f;
        public const float NetterChance = 0.5f;      // 그 깊이 이상 궁수 자리가 그물 코볼트일 확률
        public const int WargOffset = 6;             // 보스 방 와르그: 족장 자리에서 양옆 칸 수

        public static int Clamp(int stage) => Math.Max(FirstStage, Math.Min(LastStage, stage));

        public static bool IsForest(int stage) => Clamp(stage) >= ForestStage;

        public static float KoboldChance(float difficulty)
        {
            float d = Math.Max(0f, Math.Min(1f, difficulty));
            return KoboldChanceMin + (KoboldChanceMax - KoboldChanceMin) * d;
        }

        // 스테이지 1 적 종류 → 그 스테이지의 적. roll: 0~1 균등값 (적마다 고정, Roll 참고). 스테이지 1이면 그대로.
        public static StageEnemy Adapt(StageEnemy kind, int stage, float difficulty, double roll)
        {
            if (!IsForest(stage)) return kind;
            switch (kind)
            {
                case StageEnemy.Slime:
                case StageEnemy.Goblin:
                    return roll < KoboldChance(difficulty) ? StageEnemy.Kobold : StageEnemy.Goblin;
                case StageEnemy.GoblinSoldier:
                    return roll < KoboldChance(difficulty) ? StageEnemy.Kobold : StageEnemy.GoblinSoldier;
                case StageEnemy.GoblinArcher:
                    return difficulty >= KoboldNetterMinDifficulty && roll < NetterChance ? StageEnemy.KoboldNetter : StageEnemy.GoblinArcher;
                case StageEnemy.MotherSlime:
                    return StageEnemy.GoblinWarrior;
                case StageEnemy.SlimeKing:
                    return StageEnemy.KoboldChief;
                default:
                    return kind; // 중간보스·보물 고블린·이미 바뀐 것
            }
        }

        // 적 하나마다 고정된 0~1 값 (시드·적 번호로). 같은 판이면 늘 같은 적이 나온다.
        public static double Roll(int seed, int id)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)id * 2246822519u ^ 0x9E3779B9u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        // 방 하나의 적을 그 스테이지 구성으로 바꾼다 (RoomShaper.Build 끝). 보스(코볼트 족장)에는 와르그 둘을 붙인다.
        public static void AdaptChunk(int seed, ChunkData chunk)
        {
            if (!IsForest(chunk.Stage)) return;
            int chiefIndex = -1;
            for (int i = 0; i < chunk.Features.Count; i++)
            {
                var feature = chunk.Features[i];
                if (feature.Kind != FeatureKind.Enemy) continue;
                feature.Enemy = Adapt(feature.Enemy, chunk.Stage, chunk.Difficulty, Roll(seed, feature.Id));
                chunk.Features[i] = feature;
                if (feature.Enemy == StageEnemy.KoboldChief) chiefIndex = i;
            }

            if (chiefIndex < 0) return;
            var chief = chunk.Features[chiefIndex];
            int arenaX0 = 1, arenaX1 = chunk.Cols - 2;
            foreach (var feature in chunk.Features)
                if (feature.Kind == FeatureKind.Arena) { arenaX0 = feature.X; arenaX1 = feature.X + feature.Width - 1; }
            foreach (int side in new[] { -1, 1 })
            {
                int x = Math.Max(arenaX0 + 1, Math.Min(arenaX1 - 1, chief.X + side * WargOffset));
                chunk.AddEnemy(StageEnemy.Warg, x, chief.Y, true);
            }
        }

        // 저주 상자 무리처럼 씬에서 부르는 적 (ChunkStreamer). id는 방·자리마다 다른 값.
        public static StageEnemy AdaptSpawn(StageEnemy kind, int stage, float difficulty, int seed, int id) =>
            Adapt(kind, stage, difficulty, Roll(seed, id));

        public static bool IsBoss(StageEnemy kind) => kind == StageEnemy.SlimeKing || kind == StageEnemy.KoboldChief;

        // 보스 방에서 보스와 함께 쓰러뜨려야 출구가 열리는 짝 (와르그)
        public static bool IsBossPartner(StageEnemy kind) => kind == StageEnemy.Warg;
    }
}
