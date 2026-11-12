using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 청크 하나의 지형/함정/적 배치를 만든다. 같은 (스테이지 시드, 청크 번호, 시도 횟수)면 항상 같은 청크가 나온다.
    //
    // 지형 규칙은 플레이어 점프 능력에서 나왔다 (중력 3배, 점프 속도 12 → 최고 높이 약 2.45, 체공 0.82초, 걷기 초당 5유닛):
    // - 한 번에 오르는 높이는 최대 2칸, 낭떠러지 폭은 최대 3칸 (한 칸 오르며 건너면 2칸).
    // - 발판(아래에서 뚫고 올라가는 판)은 서 있는 높이보다 최대 2칸 위.
    // 그래도 어긋날 수 있어서 StageValidator가 다시 확인하고, 통과 못 하면 다른 시도 번호로 다시 만든다.
    public static class ChunkGenerator
    {
        public const int MinGround = 3;
        public const int MaxGround = 13;

        private const int W = ChunkData.Width;
        private const int H = ChunkData.Height;

        public static ChunkData Generate(int stageSeed, int index, ChunkKind kind, float difficulty, int entryHeight, int attempt)
        {
            var rng = new Random(Hash(stageSeed, index, attempt));
            var chunk = new ChunkData
            {
                Index = index,
                Kind = kind,
                Difficulty = difficulty,
                EntryHeight = entryHeight,
                Attempts = attempt + 1
            };

            var heights = new int[W];
            var occupied = new bool[W]; // 함정/특수 오브젝트가 있는 열 (적을 그 위에 두지 않는다)

            switch (kind)
            {
                case ChunkKind.Start: BuildStart(chunk, heights); break;
                case ChunkKind.Plains: BuildPlains(chunk, heights, occupied, rng); break;
                case ChunkKind.Hills: BuildHills(chunk, heights, occupied, rng); break;
                case ChunkKind.Pits: BuildPits(chunk, heights, occupied, rng); break;
                case ChunkKind.SpikeField: BuildSpikeField(chunk, heights, occupied, rng); break;
                case ChunkKind.Climb: BuildClimb(chunk, heights, occupied, rng); break;
                case ChunkKind.Cave: BuildCave(chunk, heights, occupied, rng); break;
                case ChunkKind.CrumbleBridge: BuildCrumbleBridge(chunk, heights, rng); break;
                case ChunkKind.ElementGate: BuildElementGate(chunk, heights, rng); break;
                case ChunkKind.MovingBridge: BuildMovingBridge(chunk, heights, rng); break;
                case ChunkKind.SwitchBridge: BuildDoubleMovingBridge(chunk, heights, rng, true); break;
                case ChunkKind.Arena: BuildArena(chunk, heights, rng, false); break;
                case ChunkKind.EliteArena: BuildArena(chunk, heights, rng, true); break;
                case ChunkKind.Rest: BuildRest(chunk, heights); break;
                case ChunkKind.Boss: BuildBoss(chunk, heights); break;
                default: Flat(heights, entryHeight); break;
            }

            chunk.ExitHeight = heights[W - 1];
            return chunk;
        }

        // 검증을 끝내 통과하지 못했을 때 쓰는 평평한 청크 (항상 지나갈 수 있다).
        public static ChunkData Fallback(int index, ChunkKind kind, float difficulty, int entryHeight)
        {
            var chunk = new ChunkData { Index = index, Kind = kind, Difficulty = difficulty, EntryHeight = entryHeight, Attempts = 0 }; // Attempts 0 = 대체 청크
            var heights = new int[W];
            Flat(heights, entryHeight);
            ApplyHeights(chunk, heights);
            if (kind == ChunkKind.Start) AddLeftWall(chunk);
            if (kind == ChunkKind.Boss)
            {
                AddRightWall(chunk);
                chunk.AddFeature(FeatureKind.Exit, W - 4, entryHeight);
            }
            if (kind == ChunkKind.Start) chunk.AddFeature(FeatureKind.PlayerStart, 4, entryHeight);
            chunk.ExitHeight = entryHeight;
            return chunk;
        }

        private static int Hash(int seed, int index, int attempt)
        {
            unchecked
            {
                int h = seed * 73856093;
                h ^= index * 19349663;
                h ^= attempt * 83492791;
                return h;
            }
        }

        private static int Clamp(int value) => Math.Max(MinGround, Math.Min(MaxGround, value));

        private static int PickDelta(Random rng, params int[] options) => options[rng.Next(options.Length)];

        private static void Flat(int[] heights, int height)
        {
            for (int x = 0; x < W; x++) heights[x] = height;
        }

        private static void ApplyHeights(ChunkData chunk, int[] heights)
        {
            for (int x = 0; x < W; x++) chunk.FillColumn(x, heights[x]);
        }

        private static void AddLeftWall(ChunkData chunk) => chunk.FillColumn(0, H);

        private static void AddRightWall(ChunkData chunk) => chunk.FillColumn(W - 1, H);

        // ------------------------------------------------------------------ 적

        // 진행도가 오를수록 슬라임 대신 고블린/궁수/병사가 섞인다.
        internal static StageEnemy PickEnemy(Random rng, float difficulty)
        {
            double r = rng.NextDouble();
            if (difficulty < 0.2f) return StageEnemy.Slime;
            if (difficulty < 0.45f) return r < 0.5 ? StageEnemy.Slime : r < 0.8 ? StageEnemy.Goblin : StageEnemy.GoblinArcher;
            if (difficulty < 0.7f)
                return r < 0.3 ? StageEnemy.Slime : r < 0.6 ? StageEnemy.Goblin : r < 0.85 ? StageEnemy.GoblinArcher : StageEnemy.GoblinSoldier;
            return r < 0.2 ? StageEnemy.Slime : r < 0.45 ? StageEnemy.Goblin : r < 0.7 ? StageEnemy.GoblinArcher : StageEnemy.GoblinSoldier;
        }

        // 평평한(양옆 높이가 같은) 땅 위, 함정에서 떨어진 곳에 적을 세운다.
        private static void PlaceEnemies(ChunkData chunk, int[] heights, bool[] occupied, Random rng, int count)
        {
            for (int placed = 0, tries = 0; placed < count && tries < 40; tries++)
            {
                int x = rng.Next(5, W - 5);
                if (heights[x] <= 0 || heights[x - 1] != heights[x] || heights[x + 1] != heights[x]) continue;
                if (IsNear(occupied, x, 2)) continue;

                chunk.AddEnemy(PickEnemy(rng, chunk.Difficulty), x, heights[x]);
                Mark(occupied, x, 1);
                placed++;
            }
        }

        private static int TravelEnemyCount(Random rng, float difficulty, int baseCount)
        {
            return baseCount + (rng.NextDouble() < difficulty ? 1 : 0);
        }

        private static bool IsNear(bool[] occupied, int x, int radius)
        {
            for (int i = Math.Max(0, x - radius); i <= Math.Min(W - 1, x + radius); i++) if (occupied[i]) return true;
            return false;
        }

        private static void Mark(bool[] occupied, int x, int radius)
        {
            for (int i = Math.Max(0, x - radius); i <= Math.Min(W - 1, x + radius); i++) occupied[i] = true;
        }

        // ------------------------------------------------------------------ 청크 종류별 지형

        private static void BuildStart(ChunkData chunk, int[] heights)
        {
            Flat(heights, chunk.EntryHeight);
            ApplyHeights(chunk, heights);
            AddLeftWall(chunk);
            chunk.AddFeature(FeatureKind.PlayerStart, 4, chunk.EntryHeight);
        }

        // 4~8칸마다 한 칸씩 오르내리는 완만한 언덕. 가끔 2칸짜리 가시.
        private static void BuildPlains(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int current = chunk.EntryHeight;
            for (int x = 0; x < W;)
            {
                int length = rng.Next(4, 9);
                if (x > 0) current = Clamp(current + PickDelta(rng, -1, 0, 0, 1));
                for (int i = 0; i < length && x < W; i++) heights[x++] = current;
            }

            heights[W - 1] = heights[W - 2];
            ApplyHeights(chunk, heights);

            if (rng.NextDouble() < 0.25 + 0.4 * chunk.Difficulty) PlaceSpikeStrip(chunk, heights, occupied, rng, 2);
            PlaceEnemies(chunk, heights, occupied, rng, TravelEnemyCount(rng, chunk.Difficulty, 1));
        }

        // 높낮이 차가 1~2칸인 언덕. 높은 계단 벽에 화살 발사대가 박혀 낮은 쪽(플레이어가 오는 왼쪽)을 쏜다.
        private static void BuildHills(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int current = chunk.EntryHeight;
            for (int x = 0; x < W;)
            {
                int length = rng.Next(3, 7);
                if (x > 0) current = Clamp(current + PickDelta(rng, -2, -1, 1, 2, 2));
                for (int i = 0; i < length && x < W; i++) heights[x++] = current;
            }

            heights[W - 1] = heights[W - 2];
            ApplyHeights(chunk, heights);

            if (rng.NextDouble() < 0.3 + 0.5 * chunk.Difficulty)
            {
                for (int x = 9; x < W - 3; x++)
                {
                    int low = heights[x - 1];
                    // 벽 앞으로 4칸 이상 평지가 있어야 화살을 보고 피할 수 있다.
                    if (heights[x] - low < 2 || heights[x - 2] != low || heights[x - 3] != low || heights[x - 4] != low) continue;
                    chunk.AddFeature(FeatureKind.ArrowTrap, x, low, 1, -1);
                    Mark(occupied, x - 1, 1);
                    break;
                }
            }

            PlaceEnemies(chunk, heights, occupied, rng, TravelEnemyCount(rng, chunk.Difficulty, 1 + (chunk.Difficulty > 0.6f ? 1 : 0)));
        }

        // 낭떠러지(바닥 없는 구멍) 사이의 섬을 건넌다. 뒤로 갈수록 섬 일부가 무너지는 발판으로 바뀐다.
        private static void BuildPits(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int current = chunk.EntryHeight;
            int x = 0;
            for (; x < 4; x++) heights[x] = current;

            var crumbles = new List<int[]>();
            while (x < W - 4)
            {
                int remaining = W - 4 - x;
                if (remaining < 5) break;

                int gap = rng.Next(2, chunk.Difficulty > 0.5f ? 4 : 3);
                int delta = PickDelta(rng, -1, 0, 0, 1);
                if (gap >= 3 && delta > 0) delta = 0;
                current = Clamp(current + delta);

                for (int i = 0; i < gap; i++) heights[x++] = 0;

                int width = Math.Min(rng.Next(3, 6), W - 4 - x);
                bool crumble = chunk.Difficulty > 0.3f && width <= 4 && rng.NextDouble() < 0.35;
                if (crumble) crumbles.Add(new[] { x, current - 1, width });
                for (int i = 0; i < width; i++) heights[x++] = crumble ? 0 : current;
            }

            for (; x < W; x++) heights[x] = current;
            ApplyHeights(chunk, heights);

            foreach (var c in crumbles)
            {
                chunk.AddFeature(FeatureKind.Crumble, c[0], c[1], c[2]);
                for (int i = 0; i < c[2]; i++) occupied[c[0] + i] = true;
            }

            if (chunk.Difficulty > 0.3f) PlaceEnemies(chunk, heights, occupied, rng, 1);
        }

        // 평지에 가시 줄이 이어진다. 3칸짜리 가시 위에는 발판을 놓아 밟고 건널 수 있게 한다.
        private static void BuildSpikeField(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);

            for (int x = 4; x < W - 6;)
            {
                x += rng.Next(2, 5);
                int width = chunk.Difficulty >= 0.5f && rng.NextDouble() < 0.5 ? 3 : 2;
                if (x + width > W - 4) break;

                chunk.AddFeature(FeatureKind.Spikes, x, ground, width);
                for (int i = 0; i < width; i++) occupied[x + i] = true;

                // 발판 칸은 바닥보다 한 칸 위 (윗면 = 바닥 + 2, 점프 최고 높이 2.45 안쪽).
                if (width == 3 || rng.NextDouble() < 0.4) chunk.SetRow(x - 1, x + width, ground + 1, StageTile.Platform);
                x += width;
            }

            PlaceEnemies(chunk, heights, occupied, rng, chunk.Difficulty > 0.4f ? 1 : 0);
        }

        // 발판을 지그재그로 밟고 6~8칸 높이의 절벽 위로 올라갔다가 계단으로 내려간다. 절벽 위에는 적이 지킨다.
        private static void BuildClimb(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int current = chunk.EntryHeight;
            int x = 0;
            heights[x++] = current;
            heights[x++] = current;

            // 이미 높은 곳에서 시작하면 먼저 2칸씩 내려가서 절벽이 화면 위로 넘치지 않게 한다.
            while (current > 9)
            {
                current -= 2;
                heights[x++] = current;
                heights[x++] = current;
            }

            int approachStart = x;
            int cliffX = approachStart + 8;
            for (; x < cliffX; x++) heights[x] = current;

            int baseHeight = current;
            int rise = rng.NextDouble() < 0.5 ? 6 : 8;
            int top = baseHeight + rise;
            int plateauEnd = Math.Min(cliffX + 8, W - 2);
            for (; x < plateauEnd; x++) heights[x] = top;

            int exitTarget = Clamp(baseHeight + rng.Next(-1, 3));
            current = top;
            while (x < W)
            {
                if (current > exitTarget && x <= W - 2)
                {
                    current = Math.Max(current - 2, exitTarget);
                    heights[x++] = current;
                    if (x < W) heights[x++] = current;
                }
                else
                {
                    heights[x++] = current;
                }
            }

            heights[W - 1] = heights[W - 2];
            ApplyHeights(chunk, heights);

            // 발판은 절벽 앞 평지 위에 지그재그로, 맨 위 발판이 절벽에 붙도록 위에서부터 번갈아 놓는다.
            int platformCount = rise / 2 - 1;
            for (int k = 1; k <= platformCount; k++)
            {
                int row = baseHeight + 2 * k - 1;
                bool nearCliff = (platformCount - k) % 2 == 0;
                int start = nearCliff ? cliffX - 3 : cliffX - 6;
                chunk.SetRow(start, start + 2, row, StageTile.Platform);
            }

            Mark(occupied, cliffX - 1, 0);
            int guardX = cliffX + 4;
            var enemy = chunk.Difficulty > 0.3f && rng.NextDouble() < 0.6 ? StageEnemy.GoblinArcher : PickEnemy(rng, chunk.Difficulty);
            chunk.AddEnemy(enemy, guardX, top);
        }

        // 천장이 낮은 동굴. 바닥의 화염 분출구가 주기적으로 불기둥을 뿜는다.
        private static void BuildCave(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int current = chunk.EntryHeight;
            for (int x = 0; x < W;)
            {
                int length = rng.Next(4, 8);
                if (x > 0) current = Math.Min(Clamp(current + PickDelta(rng, -1, 0, 1)), 12);
                for (int i = 0; i < length && x < W; i++) heights[x++] = current;
            }

            heights[W - 1] = heights[W - 2];
            ApplyHeights(chunk, heights);

            // 천장: 바닥에서 5~6칸 위 (점프해도 머리가 닿지 않는 높이). 양 끝 두 칸은 동굴 입구라 비운다.
            int extra = 0;
            for (int x = 2; x < W - 2; x++)
            {
                if (x % 4 == 2) extra = rng.NextDouble() < 0.3 ? 1 : 0;
                int ceiling = heights[x] + 5 + extra;
                for (int y = ceiling; y < H; y++) chunk.Tiles[x, y] = StageTile.Solid;
            }

            int jets = 1 + (chunk.Difficulty > 0.5f ? 1 : 0) + (chunk.Difficulty > 0.8f ? 1 : 0);
            for (int placed = 0, tries = 0; placed < jets && tries < 40; tries++)
            {
                int x = rng.Next(5, W - 5);
                if (heights[x - 1] != heights[x] || heights[x + 1] != heights[x] || IsNear(occupied, x, 4)) continue;
                chunk.AddFeature(FeatureKind.FlameJet, x, heights[x]);
                Mark(occupied, x, 1);
                placed++;
            }

            PlaceEnemies(chunk, heights, occupied, rng, TravelEnemyCount(rng, chunk.Difficulty, 1));
        }

        // 긴 낭떠러지 위에 무너지는 발판만 이어진다. 멈추면 떨어진다.
        private static void BuildCrumbleBridge(ChunkData chunk, int[] heights, Random rng)
        {
            int current = chunk.EntryHeight;
            int x = 0;
            for (; x < 4; x++) heights[x] = current;

            var crumbles = new List<int[]>();
            while (x + 2 + 3 <= W - 4)
            {
                x += rng.Next(1, 3);
                current = Math.Max(MinGround, Math.Min(MaxGround - 1, current + PickDelta(rng, -1, 0, 0, 1)));
                int width = rng.Next(2, 4);
                crumbles.Add(new[] { x, current - 1, width });
                x += width;
            }

            x += rng.Next(1, 3);
            int pitEnd = x;
            for (int i = 4; i < pitEnd; i++) heights[i] = 0;
            for (; x < W; x++) heights[x] = current;
            ApplyHeights(chunk, heights);

            foreach (var c in crumbles) chunk.AddFeature(FeatureKind.Crumble, c[0], c[1], c[2]);
        }

        // 평평한 전투 구역. 들어가면 양쪽이 막히고 적이 나온다. 발판 두 개로 위아래 동선을 만든다.
        private static void BuildArena(ChunkData chunk, int[] heights, Random rng, bool elite)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);
            chunk.SetRow(7, 11, ground + 1, StageTile.Platform);
            chunk.SetRow(20, 24, ground + 1, StageTile.Platform);
            chunk.AddFeature(FeatureKind.Arena, 2, ground, W - 4);

            if (elite)
            {
                var boss = chunk.Index % 2 == 0 ? StageEnemy.MotherSlime : StageEnemy.GoblinWarrior;
                chunk.AddEnemy(boss, 22, ground, true);
                chunk.AddEnemy(PickEnemy(rng, chunk.Difficulty), 14, ground, true);
                chunk.AddEnemy(PickEnemy(rng, chunk.Difficulty), 26, ground, true);
                return;
            }

            int count = 3 + (int)Math.Round(3 * chunk.Difficulty);
            for (int i = 0; i < count; i++)
            {
                int x = 8 + (int)Math.Round(i * (18.0 / Math.Max(1, count - 1)));
                chunk.AddEnemy(PickEnemy(rng, chunk.Difficulty), x, ground, true);
            }
        }

        public const int GateX = 16;        // 봉인문 열
        public const int GateElementCount = 3; // 불/물/바람 (투사체 마법만. 땅은 바닥에서 솟는 가시, 무는 평타라 제외)

        // 맵 퍼즐: 가운데 봉인문이 길을 막는다. B가 문 색과 같은 속성 마법을 맞혀야 열린다.
        // 문 앞(왼쪽)은 평평한 사격장으로 비워 두고(함정/적 없음), 높은 곳에서도 쏠 수 있게 발판 하나를 둔다.
        private static void BuildElementGate(ChunkData chunk, int[] heights, Random rng)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            int step = PickDelta(rng, -1, 0, 1);
            int stepAt = rng.Next(22, W - 3);
            for (int x = stepAt; x < W; x++) heights[x] = Clamp(ground + step);
            ApplyHeights(chunk, heights);
            chunk.SetRow(8, 10, ground + 1, StageTile.Platform);

            var gate = new ChunkFeature
            {
                Kind = FeatureKind.ElementGate, X = GateX, Y = ground, Width = 1, ElementIndex = rng.Next(GateElementCount)
            };
            // 진행도 후반에는 두 속성을 순서대로 맞혀야 열리는 봉인문 (B가 영창을 두 번, 서로 다른 속성으로).
            if (chunk.Difficulty >= TwoElementGateMinDifficulty)
            {
                gate.HasSecondElement = true;
                gate.SecondElementIndex = (gate.ElementIndex + 1 + rng.Next(GateElementCount - 1)) % GateElementCount;
            }
            chunk.Features.Add(gate);
        }

        public const float TwoElementGateMinDifficulty = 0.6f;

        public const int MovingPlatformWidth = 3;
        public const int MovingBridgeLeft = 8; // 낭떠러지가 시작되는 열

        // 컨트롤 퍼즐: 점프(최대 4~5칸)로는 못 건너는 7~10칸 낭떠러지. 3칸짜리 발판이 양쪽 절벽 끝을 오가므로
        // 발판이 가까이 왔을 때 올라타고, 건너편에 닿을 때 내린다. 떨어지면 낭떠러지 복귀 규칙대로 체력 15%를 잃고 돌아온다.
        private static void BuildMovingBridge(ChunkData chunk, int[] heights, Random rng)
        {
            if (chunk.Difficulty >= DoubleBridgeMinDifficulty && rng.Next(2) == 0)
            {
                BuildDoubleMovingBridge(chunk, heights, rng);
                return;
            }

            int ground = chunk.EntryHeight;
            int gap = rng.Next(7, chunk.Difficulty > 0.6f ? 11 : 9);
            // 스위치 변형은 같은 난수열을 먼저 정해 둔다 (폭이 달라져야 하므로).
            bool switched = chunk.Difficulty >= SwitchBridgeMinDifficulty && rng.Next(SwitchBridgeOneIn) == 0;
            if (switched) gap = rng.Next(SwitchBridgeMinGap, SwitchBridgeMaxGap + 1);
            int right = MovingBridgeLeft + gap; // 건너편 절벽이 시작되는 열
            int far = Clamp(ground + PickDelta(rng, -1, 0, 0));
            for (int x = 0; x < W; x++) heights[x] = x < MovingBridgeLeft ? ground : x < right ? 0 : far;
            ApplyHeights(chunk, heights);

            int row = Math.Max(ground, far) - 1; // 발판 윗면 = 더 높은 쪽 절벽 높이 (낮은 쪽에서는 한 칸 올라탄다)
            chunk.AddFeature(FeatureKind.MovingPlatform, MovingBridgeLeft, row, MovingPlatformWidth, gap - MovingPlatformWidth);

            // 변형: 마법 스위치 발판. 발판은 멈춰 있고, 건너편 절벽 첫 칸에 선 룬에 B가 같은 속성 마법을 맞히면 잠시 오간다.
            // 룬은 몸이 지나가는 트리거라 길을 막지 않고, 출발 쪽 절벽 끝에서 수평으로 쏜 마법(서 있는 높이 +0.7)이 닿는 높이에 있다.
            // 발판은 건너편 끝에 멈춰 있고(가까운 쪽 발판 위에서 뛰어 거리를 줄이지 못하게), 낭떠러지에는 마력 안개가 깔려
            // 순간이동·대시로 건너뛰지 못한다. 폭 9~10칸 > 달리기 점프(약 7.3칸) (2026-11-12 플레이테스트: 대시·순간이동으로 건너짐).
            if (switched)
            {
                int last = chunk.Features.Count - 1;
                var platform = chunk.Features[last];
                platform.SwitchDriven = true;
                chunk.Features[last] = platform;
                chunk.Features.Add(new ChunkFeature
                {
                    Kind = FeatureKind.PlatformSwitch, X = right, Y = far, Width = 1, ElementIndex = rng.Next(GateElementCount)
                });
                chunk.AddFeature(FeatureKind.BlinkWard, MovingBridgeLeft, 0, gap);
            }
        }

        public const int SwitchBridgeMinGap = 9;
        public const int SwitchBridgeMaxGap = 10; // 출발 쪽 땅 끝에서 룬까지 gap + 1칸 ≤ 물 마법 사거리 12

        public const float SwitchBridgeMinDifficulty = 0.5f;
        public const int SwitchBridgeOneIn = 2; // 진행도 50% 이후 한 발판 낭떠러지의 절반

        public const float DoubleBridgeMinDifficulty = 0.7f;
        public const int DoubleBridgePillarWidth = 2;
        public const int DoubleBridgeGap = 9; // 8 + 9 + 2 + 9 = 28열, 건너편 절벽 4칸

        // 변형: 낭떠러지 두 개 사이에 좁은 기둥(2칸). 발판 두 개가 반 박자 엇갈려 오가므로, 첫 발판이 기둥에 닿아 멈춘 동안
        // 둘째 발판이 기둥 반대편에서 기다리고 있다 (놓치면 기둥에서 한 바퀴 기다린다).
        // 두 낭떠러지 폭이 같아 두 발판의 주기가 같다 (엇갈림이 유지된다). 폭 9칸 = 달리기 점프(약 7.3칸)·걷기 점프+공중 대쉬(약 8.1칸)로도
        // 못 건너는 폭 (검증기는 걷기 점프 기준이라 이 여유는 여기서 따로 잡는다). 달리기 점프+대쉬(약 11칸)는 막지 못한다.
        // forceSwitch: 이중 스위치로 고정 (ChunkKind.SwitchBridge).
        private static void BuildDoubleMovingBridge(ChunkData chunk, int[] heights, Random rng, bool forceSwitch = false)
        {
            int ground = chunk.EntryHeight;
            int gap = DoubleBridgeGap;
            int pillar = MovingBridgeLeft + gap;               // 기둥이 시작되는 열
            int secondLeft = pillar + DoubleBridgePillarWidth; // 둘째 낭떠러지가 시작되는 열
            int right = secondLeft + gap;                      // 건너편 절벽이 시작되는 열
            int far = Clamp(ground + PickDelta(rng, -1, 0, 0));
            int top = Math.Max(ground, far);                   // 기둥과 발판 윗면 높이
            for (int x = 0; x < W; x++)
            {
                if (x < MovingBridgeLeft) heights[x] = ground;
                else if (x < pillar) heights[x] = 0;
                else if (x < secondLeft) heights[x] = top;
                else if (x < right) heights[x] = 0;
                else heights[x] = far;
            }
            ApplyHeights(chunk, heights);

            int travel = gap - MovingPlatformWidth;
            chunk.AddFeature(FeatureKind.MovingPlatform, MovingBridgeLeft, top - 1, MovingPlatformWidth, travel);
            chunk.AddFeature(FeatureKind.MovingPlatform, secondLeft, top - 1, MovingPlatformWidth, travel);

            // 변형: 이중 스위치 발판. 두 발판 모두 멈춰 있고, 각 발판 건너편 첫 칸(기둥 첫 칸, 건너편 절벽 첫 칸)에 서로 다른 속성 룬.
            // 첫 룬은 출발 쪽 절벽에서, 둘째 룬은 기둥 위에서 쏴서 맞힌다 (둘 다 10칸 거리).
            if (forceSwitch || (chunk.Difficulty >= DoubleSwitchBridgeMinDifficulty && rng.Next(DoubleSwitchBridgeOneIn) == 0))
            {
                int count = chunk.Features.Count;
                for (int i = count - 2; i < count; i++)
                {
                    var platform = chunk.Features[i];
                    platform.SwitchDriven = true;
                    chunk.Features[i] = platform;
                }
                int first = rng.Next(GateElementCount);
                int second = (first + 1 + rng.Next(GateElementCount - 1)) % GateElementCount;
                chunk.Features.Add(new ChunkFeature { Kind = FeatureKind.PlatformSwitch, X = pillar, Y = top, Width = 1, ElementIndex = first });
                chunk.Features.Add(new ChunkFeature { Kind = FeatureKind.PlatformSwitch, X = right, Y = far, Width = 1, ElementIndex = second });
                chunk.AddFeature(FeatureKind.BlinkWard, MovingBridgeLeft, 0, gap);
                chunk.AddFeature(FeatureKind.BlinkWard, secondLeft, 0, gap);
            }
        }

        public const float DoubleSwitchBridgeMinDifficulty = 0.8f;
        public const int DoubleSwitchBridgeOneIn = 2;

        private static void BuildRest(ChunkData chunk, int[] heights)
        {
            Flat(heights, chunk.EntryHeight);
            ApplyHeights(chunk, heights);
        }

        // 마지막 전투: 보스 슬라임 킹. 이기면 오른쪽 끝 출구가 열린다.
        private static void BuildBoss(ChunkData chunk, int[] heights)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);
            AddRightWall(chunk);
            chunk.SetRow(9, 13, ground + 1, StageTile.Platform);
            chunk.SetRow(18, 22, ground + 1, StageTile.Platform);
            chunk.AddFeature(FeatureKind.Arena, 2, ground, W - 6);
            chunk.AddEnemy(StageEnemy.SlimeKing, 20, ground, true); // 보스 슬라임 킹 (부하는 킹이 직접 소환한다)
            chunk.AddFeature(FeatureKind.Exit, W - 3, ground);
        }

        // 평평한 구간 안에 가시 줄 하나 (양옆으로 밟을 땅을 남긴다).
        private static void PlaceSpikeStrip(ChunkData chunk, int[] heights, bool[] occupied, Random rng, int width)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                int x = rng.Next(5, W - 5 - width);
                int h = heights[x - 1];
                bool flat = true;
                for (int i = -1; i <= width; i++) flat &= heights[x + i] == h;
                if (!flat || IsNear(occupied, x, 3)) continue;

                chunk.AddFeature(FeatureKind.Spikes, x, h, width);
                for (int i = 0; i < width; i++) occupied[x + i] = true;
                return;
            }
        }
    }
}
