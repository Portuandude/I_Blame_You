using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 스테이지에 어떤 청크를 어떤 순서로 놓을지 정한다 (마인크래프트처럼 시드 하나로 결정).
    // 청크 종류마다 예상 플레이 시간을 두고, 합이 목표 시간(스테이지 1은 30분)에 닿을 때까지 이동 구간과 전투 구역을 번갈아 붙인다.
    public static class StagePlanner
    {
        // 청크 하나를 지나는 데 걸릴 것으로 보는 시간(초). 이동 구간은 32유닛을 걸으면 6초 남짓이지만
        // 길 위의 적 1~3마리, 함정, 낭떠러지를 넘는 시간을 더한 가안이다. 실제 플레이 후 조정한다.
        public static float EstimatedSeconds(ChunkKind kind)
        {
            switch (kind)
            {
                case ChunkKind.Start: return 15f;
                case ChunkKind.Plains: return 25f;
                case ChunkKind.Hills: return 30f;
                case ChunkKind.Pits: return 35f;
                case ChunkKind.SpikeField: return 30f;
                case ChunkKind.Climb: return 35f;
                case ChunkKind.Cave: return 30f;
                case ChunkKind.CrumbleBridge: return 30f;
                case ChunkKind.Arena: return 70f;
                case ChunkKind.EliteArena: return 100f;
                case ChunkKind.Rest: return 20f;
                case ChunkKind.Boss: return 180f;
                default: return 30f;
            }
        }

        private const int ArenasPerRest = 3;
        private static readonly float[] EliteAt = { 0.4f, 0.72f }; // 진행도가 여기를 넘은 뒤 첫 전투 구역은 정예

        // 반환: (청크 종류, 그 청크가 시작될 때의 진행도 0~1) 목록.
        public static List<KeyValuePair<ChunkKind, float>> Plan(int seed, float targetSeconds)
        {
            var rng = new Random(seed);
            var plan = new List<KeyValuePair<ChunkKind, float>>();
            float elapsed = 0f;
            float reserve = EstimatedSeconds(ChunkKind.Rest) + EstimatedSeconds(ChunkKind.Boss);
            int elitesPlaced = 0;
            int arenasSinceRest = 0;
            ChunkKind last = ChunkKind.Start;

            Action<ChunkKind> add = kind =>
            {
                plan.Add(new KeyValuePair<ChunkKind, float>(kind, Math.Min(elapsed / targetSeconds, 1f)));
                elapsed += EstimatedSeconds(kind);
                last = kind;
            };

            add(ChunkKind.Start);
            add(ChunkKind.Plains); // 첫 전투 전에 조작을 익힐 평지 하나

            while (elapsed < targetSeconds - reserve)
            {
                int travelCount = rng.Next(2, 4);
                for (int i = 0; i < travelCount && elapsed < targetSeconds - reserve; i++)
                {
                    add(PickTravel(rng, elapsed / targetSeconds, last));
                }

                if (elapsed >= targetSeconds - reserve) break;

                bool elite = elitesPlaced < EliteAt.Length && elapsed / targetSeconds >= EliteAt[elitesPlaced];
                add(elite ? ChunkKind.EliteArena : ChunkKind.Arena);
                if (elite) elitesPlaced++;

                arenasSinceRest++;
                if (arenasSinceRest >= ArenasPerRest || elite)
                {
                    add(ChunkKind.Rest);
                    arenasSinceRest = 0;
                }
            }

            if (last != ChunkKind.Rest) add(ChunkKind.Rest);
            add(ChunkKind.Boss);
            return plan;
        }

        // 진행도가 높을수록 어려운 지형(낭떠러지, 무너지는 다리, 절벽)이 자주 나온다. 같은 종류가 연달아 나오지 않게 한다.
        private static ChunkKind PickTravel(Random rng, float progress, ChunkKind last)
        {
            var pool = new List<KeyValuePair<ChunkKind, float>>
            {
                new KeyValuePair<ChunkKind, float>(ChunkKind.Plains, 3f - 2f * progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Hills, 2f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.SpikeField, 0.5f + 1.5f * progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Pits, progress < 0.1f ? 0.5f : 1.5f + progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Cave, progress < 0.15f ? 0f : 1.5f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Climb, progress < 0.2f ? 0f : 1.2f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.CrumbleBridge, progress < 0.3f ? 0f : 0.8f + progress),
            };

            float total = 0f;
            foreach (var entry in pool) if (entry.Key != last) total += entry.Value;

            double roll = rng.NextDouble() * total;
            foreach (var entry in pool)
            {
                if (entry.Key == last) continue;
                roll -= entry.Value;
                if (roll <= 0) return entry.Key;
            }

            return ChunkKind.Plains;
        }
    }
}
