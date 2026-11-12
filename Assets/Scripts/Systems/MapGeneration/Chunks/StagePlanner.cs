using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 스테이지에 어떤 청크를 어떤 순서로 놓을지 정한다 (마인크래프트처럼 시드 하나로 결정).
    // 청크 종류마다 예상 플레이 시간을 두고, 합이 목표 시간(스테이지 1은 15분 안팎, 2026-11-10 사람 결정)에 닿을 때까지 이동 구간과 전투 구역을 번갈아 붙인다.
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
                case ChunkKind.Pillars: return 35f;
                case ChunkKind.TwinPath: return 35f;
                case ChunkKind.CrumbleBridge: return 30f;
                case ChunkKind.ElementGate: return 30f;
                case ChunkKind.MovingBridge: return 30f;
                case ChunkKind.SwitchBridge: return 40f; // 룬 두 번 쏘기 + 발판 두 번 기다리기
                case ChunkKind.Arena: return 70f;
                case ChunkKind.EliteArena: return 100f;
                case ChunkKind.Rest: return 20f;
                case ChunkKind.Boss: return 180f;
                default: return 30f;
            }
        }

        public const int ArenasPerRest = 3;            // 전투 구역 이만큼마다 휴식 (정예 뒤에는 항상)
        private const int MaxGates = 1;                 // 스테이지당 봉인문 퍼즐 최대 개수
        public const float GateMinProgress = 0.25f;     // 이 진행도 구간에서만 봉인문이 나온다 (초반 조작 연습 뒤, 보스 앞 휴식 전)
        public const float GateMaxProgress = 0.85f;
        // 이중 스위치 퍼즐(SwitchBridge)은 진행도가 이 값을 넘은 뒤 첫 이동 방으로 꼭 한 번 나온다.
        // (예전에는 무작위 발판 낭떠러지의 80% 이후 변형뿐이라, 이동 방이 78% 언저리에서 끝나는 15분 판에서는 거의 안 나왔다.)
        public const float SwitchBridgeDepth = 0.5f;
        private static readonly float[] EliteAt = { 0.3f }; // 진행도가 여기를 넘은 뒤 첫 전투 구역은 정예 (15분: 정예 0.3 → 중간보스 0.55 → 보스)
        public static readonly IReadOnlyList<float> EliteProgress = Array.AsReadOnly(EliteAt);

        // 반환: (청크 종류, 그 청크가 시작될 때의 진행도 0~1) 목록.
        public static List<KeyValuePair<ChunkKind, float>> Plan(int seed, float targetSeconds)
        {
            var rng = new Random(seed);
            var plan = new List<KeyValuePair<ChunkKind, float>>();
            float elapsed = 0f;
            float reserve = EstimatedSeconds(ChunkKind.Rest) + EstimatedSeconds(ChunkKind.Boss);
            int elitesPlaced = 0;
            int arenasSinceRest = 0;
            int gatesPlaced = 0;
            bool switchBridgePlaced = false;
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
                int travelCount = rng.Next(1, 4); // 전투 사이 이동 방 1~3개 (15분에서 전투 비율을 25% 안팎으로)
                for (int i = 0; i < travelCount && elapsed < targetSeconds - reserve; i++)
                {
                    var travel = PickTravel(rng, elapsed / targetSeconds, last, gatesPlaced < MaxGates);
                    if (!switchBridgePlaced && (float)(elapsed / targetSeconds) >= SwitchBridgeDepth)
                    {
                        travel = ChunkKind.SwitchBridge; // 난수열은 그대로 소비해 다른 방 배치가 덜 흔들리게
                        switchBridgePlaced = true;
                    }
                    if (travel == ChunkKind.ElementGate) gatesPlaced++;
                    add(travel);
                }

                if (elapsed >= targetSeconds - reserve) break;

                // (float) 변환으로 반올림을 고정한다: 런타임에 따라 나눗셈이 더 높은 정밀도로 남아 있으면
                // 진행도가 정확히 0.4일 때 Mono에서는 정예, Unity에서는 일반 구역이 되는 식으로 결과가 갈렸다.
                float progressNow = (float)(elapsed / targetSeconds);
                bool elite = elitesPlaced < EliteAt.Length && progressNow >= EliteAt[elitesPlaced];
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
        // internal: EditMode 테스트가 반올림 대비 경로를 직접 확인한다 (AssemblyInfo.cs의 InternalsVisibleTo).
        public const float PillarsMinProgress = 0.2f;
        public const float TwinPathMinProgress = 0.25f;

        internal static ChunkKind PickTravel(Random rng, float progress, ChunkKind last, bool allowGate)
        {
            var pool = new List<KeyValuePair<ChunkKind, float>>
            {
                new KeyValuePair<ChunkKind, float>(ChunkKind.Plains, 3f - 2f * progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Hills, 2f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.SpikeField, 0.5f + 1.5f * progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Pits, progress < 0.1f ? 0.5f : 1.5f + progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Cave, progress < 0.15f ? 0f : 1.5f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Climb, progress < 0.2f ? 0f : 1.2f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.Pillars, progress < PillarsMinProgress ? 0f : 1f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.TwinPath, progress < TwinPathMinProgress ? 0f : 1f),
                new KeyValuePair<ChunkKind, float>(ChunkKind.CrumbleBridge, progress < 0.3f ? 0f : 0.8f + progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.MovingBridge, progress < 0.2f ? 0f : 0.6f + 0.4f * progress),
                new KeyValuePair<ChunkKind, float>(ChunkKind.ElementGate,
                    allowGate && progress >= GateMinProgress && progress <= GateMaxProgress ? 0.7f : 0f),
            };

            float total = 0f;
            foreach (var entry in pool) if (entry.Key != last) total += entry.Value;

            double roll = rng.NextDouble() * total;
            var fallback = ChunkKind.Hills;
            foreach (var entry in pool)
            {
                if (entry.Key == last || entry.Value <= 0f) continue;
                fallback = entry.Key;
                roll -= entry.Value;
                if (roll <= 0) return entry.Key;
            }

            // 부동소수 반올림으로 roll이 조금 남은 경우: 마지막 후보 (직전 종류와 겹치지 않게)
            return fallback;
        }
    }
}
