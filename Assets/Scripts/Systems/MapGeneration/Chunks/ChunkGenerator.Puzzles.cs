using System;

namespace IBlameYou.Systems
{
    // ChunkGenerator 나눈 파일: 퍼즐 방 지형 — 등불 순서·거울 룬·바람 상자 (Docs/PuzzleRoomDesign.md).
    public static partial class ChunkGenerator
    {
        // 퍼즐 방 "등불 순서" (2026-11-14, Docs/PuzzleRoomDesign.md 3절). 평지 + 아래에서 뚫고 오르는 발판 둘(계단):
        // 바닥(낮음) · 가운데 발판(선 칸 = 바닥 + 2) · 높은 발판(바닥 + 4)에 등불 하나씩. 속성과 켜는 순서는 난수로 섞는다.
        // 벽화는 오른쪽 벽 쪽에 켜는 순서대로 등불 그림을 높이로 그린다 (색 없음 → 높이로 속성을 알아내는 추리).
        // 등불 판정은 선 칸 바닥 위 0.1~1.8유닛이라 그 높이에 서서 쏘아야 맞는다. 문은 막지 않는다 (못 풀어도 지나간다).
        public const int LanternLowX = 4;
        public const int LanternMidX = 11;
        public const int LanternHighX = 18;
        public const int LanternMidPlatformX0 = 8, LanternMidPlatformX1 = 13;   // 발판 행 = 바닥 + 1 (선 칸 바닥 + 2)
        public const int LanternHighPlatformX0 = 15, LanternHighPlatformX1 = 20; // 발판 행 = 바닥 + 3 (선 칸 바닥 + 4)
        public const int LanternMidRise = 2;
        public const int LanternHighRise = 4;
        public const int LanternChestX = 24;
        public const int LanternMuralX = 28;
        public const int LanternCount = 3;

        private static void BuildLanternPuzzle(ChunkData chunk, int[] heights, Random rng)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);
            AddLanternPuzzle(chunk, ground, rng);
        }

        // 지형이 평평한 청크에 발판 둘 + 등불 셋 + 벽화 + 상자 자리를 놓는다 (대체 청크도 같은 모양).
        private static void AddLanternPuzzle(ChunkData chunk, int ground, Random rng)
        {
            chunk.SetRow(LanternMidPlatformX0, LanternMidPlatformX1, ground + LanternMidRise - 1, StageTile.Platform);
            chunk.SetRow(LanternHighPlatformX0, LanternHighPlatformX1, ground + LanternHighRise - 1, StageTile.Platform);

            // 속성: 불·물·바람을 섞어 낮음·가운데·높음에. 순서: 0~2를 섞어 낮음·가운데·높음이 몇 번째로 켜지는지.
            var elements = Shuffled(rng, LanternCount);
            var order = Shuffled(rng, LanternCount);
            int[] xs = { LanternLowX, LanternMidX, LanternHighX };
            int[] rises = { 0, LanternMidRise, LanternHighRise };
            for (int i = 0; i < LanternCount; i++)
                chunk.Features.Add(new ChunkFeature
                {
                    Kind = FeatureKind.Lantern, X = xs[i], Y = ground + rises[i], Width = 1,
                    ElementIndex = elements[i], Direction = order[i]
                });
            chunk.AddFeature(FeatureKind.LanternMural, LanternMuralX, ground);
            chunk.AddFeature(FeatureKind.PuzzleChest, LanternChestX, ground);
        }

        // 퍼즐 방 "거울 룬" (2026-11-14, Docs/PuzzleRoomDesign.md 2절 3번): 평지 바닥에 등불 셋(왼쪽·가운데·오른쪽, 속성·순서 섞임).
        // 방 가운데 위에 걸린 거울이 켜는 순서를 "좌우가 뒤집힌 자리"의 작은 등불 그림을 차례로 깜빡여 보인다 (색 없음).
        // → 거울에서 오른쪽 그림이 먼저 깜빡이면 실제로는 왼쪽 등불이 먼저. 기억 + 거울 뒤집기 추리. 등불 순서와 같은 판정(LanternPuzzle).
        // 바닥 등불은 마법이 처음 닿는 트리거라, 먼 등불을 쏘려면 A가 가까운 등불을 지나 그 옆으로 가야 한다.
        public const int MirrorLeftX = 5;
        public const int MirrorCenterX = 16;
        public const int MirrorRightX = 26;
        public const int MirrorChestX = 11;

        private static void BuildMirrorPuzzle(ChunkData chunk, int[] heights, Random rng)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);
            AddMirrorPuzzle(chunk, ground, rng);
        }

        private static void AddMirrorPuzzle(ChunkData chunk, int ground, Random rng)
        {
            var elements = Shuffled(rng, LanternCount);
            var order = Shuffled(rng, LanternCount);
            int[] xs = { MirrorLeftX, MirrorCenterX, MirrorRightX };
            for (int i = 0; i < LanternCount; i++)
                chunk.Features.Add(new ChunkFeature
                {
                    Kind = FeatureKind.Lantern, X = xs[i], Y = ground, Width = 1, ElementIndex = elements[i], Direction = order[i]
                });
            chunk.AddFeature(FeatureKind.MirrorMural, MirrorCenterX, ground);
            chunk.AddFeature(FeatureKind.PuzzleChest, MirrorChestX, ground);
        }

        // 퍼즐 방 "바람 상자" (2026-11-22, Docs/PuzzleRoomDesign.md 2절 4번, 기본안 = 칸 단위로 미는 상자):
        // 평지 한쪽 위에 걸린 한쪽 발판 선반(서는 칸 = 바닥 + WindLedgeRise)에 회복 구슬 + 동전 무더기. 걷기 점프(약 2.45칸)로는 못 오른다.
        // 반대쪽 바닥에 돌 상자 하나: B의 바람 마법에 맞으면 맞은 반대쪽으로 한 칸 미끄러진다 (WindBoxRules).
        // 상자를 선반 아래나 바로 옆까지 밀면 A가 상자 위(바닥 + 1)에서 뛰어 선반에 오른다. 선반 둘레는 마력 안개라 순간이동·대시로 못 건너뛴다.
        // 선반은 한쪽 발판이라 아래로 걸어 지나가고, 상자가 선반 밑에 들어가도 길을 막지 않는다 (상자 1칸 + 위가 발판).
        // 상자 없이도 방은 지나갈 수 있다 (검증기는 상자를 무시). 선반이 왼쪽/오른쪽인지는 시드로.
        public const int WindLedgeWidth = 4;
        public const int WindLedgeRise = 3;      // 선반 위 서는 칸 = 바닥 + 3 (발판 행 = 바닥 + 2)
        public const int WindLedgeRightX0 = 20;  // 오른쪽 선반의 왼쪽 열 (왼쪽 선반은 좌우 거울 자리)
        public const int WindBoxStartGap = 7;    // 상자 처음 자리 = 선반 가까운 끝에서 이만큼 떨어진 열 (바람 6번이면 선반 옆)
        public const int WindWardMargin = 5;     // 선반 양옆 마력 안개 폭 (순간이동 4칸 + 낙하 중 옆으로 흐르는 거리(약 2.3칸)보다 넉넉히. 4면 여유 0.8칸뿐이라 5)

        public static int WindLedgeX0(bool right) => right ? WindLedgeRightX0 : W - WindLedgeRightX0 - WindLedgeWidth;

        private static void BuildWindBoxPuzzle(ChunkData chunk, int[] heights, Random rng)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);
            AddWindBoxPuzzle(chunk, ground, rng);
        }

        private static void AddWindBoxPuzzle(ChunkData chunk, int ground, Random rng)
        {
            bool right = rng.Next(2) == 0;
            int x0 = WindLedgeX0(right);
            int x1 = x0 + WindLedgeWidth - 1;
            chunk.SetRow(x0, x1, ground + WindLedgeRise - 1, StageTile.Platform);
            chunk.AddFeature(FeatureKind.BlinkWard, x0 - WindWardMargin, 0, WindLedgeWidth + 2 * WindWardMargin);
            chunk.AddFeature(FeatureKind.Reward, x0 + 1, ground + WindLedgeRise);
            chunk.AddFeature(FeatureKind.CoinPile, x0 + 2, ground + WindLedgeRise);
            int boxX = right ? x0 - WindBoxStartGap : x1 + WindBoxStartGap;
            chunk.AddFeature(FeatureKind.WindBox, boxX, ground, WindLedgeWidth, x0);
        }

        // 0 ~ count-1을 섞은 배열 (Fisher-Yates).
        private static int[] Shuffled(Random rng, int count)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++) values[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = values[i];
                values[i] = values[j];
                values[j] = t;
            }
            return values;
        }
    }
}
