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
                case ChunkKind.Pillars: BuildPillars(chunk, heights, occupied, rng); break;
                case ChunkKind.TwinPath: BuildTwinPath(chunk, heights, occupied, rng); break;
                case ChunkKind.CrumbleBridge: BuildCrumbleBridge(chunk, heights, rng); break;
                case ChunkKind.ElementGate: BuildElementGate(chunk, heights, rng); break;
                case ChunkKind.MovingBridge: BuildMovingBridge(chunk, heights, rng); break;
                case ChunkKind.SwitchBridge: BuildDoubleMovingBridge(chunk, heights, rng, true); break;
                case ChunkKind.Arena: BuildArena(chunk, heights, rng, false); break;
                case ChunkKind.EliteArena: BuildArena(chunk, heights, rng, true); break;
                case ChunkKind.Rest: BuildRest(chunk, heights); break;
                case ChunkKind.LanternPuzzle: BuildLanternPuzzle(chunk, heights, rng); break;
                case ChunkKind.MirrorPuzzle: BuildMirrorPuzzle(chunk, heights, rng); break;
                case ChunkKind.ShieldBridge: BuildShieldBridge(chunk, heights, rng); break;
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
            if (kind == ChunkKind.LanternPuzzle) AddLanternPuzzle(chunk, entryHeight, new Random(Hash(index, 7, 0))); // 대체 청크에도 퍼즐은 둔다
            if (kind == ChunkKind.MirrorPuzzle) AddMirrorPuzzle(chunk, entryHeight, new Random(Hash(index, 7, 0)));
            if (kind == ChunkKind.ShieldBridge) AddShieldBridge(chunk, entryHeight, new Random(Hash(index, 7, 0)));
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

        // 난이도(깊이)가 오를수록 슬라임 대신 고블린/궁수/병사가 섞인다.
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
            PlaceBounceLedge(chunk, heights, occupied);
        }

        // 통통 버섯 덤 길 (2026-11-14, 레벨 재설계: 평지가 심심하다는 플레이테스트 의견).
        // 평지 가운데 쪽 평평한 5칸 위, 바닥 + BounceLedgeRise 높이에 5칸 발판 선반을 걸고 가운데에 버섯, 선반 위에 동전 무더기.
        // 보통 점프(약 2.4칸)로는 못 닿고 버섯을 밟아 튕겨야 오른다. 지나가는 길은 그대로라 검증과 무관한 덤이다.
        // 난수를 쓰지 않아 기존 시드의 지형·적 배치가 바뀌지 않는다 (가운데에서 가까운 열부터 찾는다).
        public const int BounceLedgeRise = 6;    // 선반 서는 칸 = 바닥 + 6
        public const int BounceLedgeHalf = 2;    // 선반 = 버섯 열 ± 2
        public const float BounceMinDifficulty = 0.1f;
        public const int BounceReachGuard = 6;   // 선반 양끝에서 이 칸 안의 언덕이 바닥 + 3보다 높으면 걸어 올라 점프로 닿으므로 놓지 않는다

        // 이미 만든 청크에 통통 버섯 덤 길을 더한다 (기능 테스트 맵용). 바닥 높이는 타일에서 읽고, 기능이 있는 열은 피한다.
        // 놓았으면 true. 문턱·자리 규칙은 PlaceBounceLedge 그대로.
        public static bool AddBounceLedge(ChunkData chunk)
        {
            var heights = new int[W];
            var occupied = new bool[W];
            for (int x = 0; x < W; x++) heights[x] = chunk.SurfaceAt(x);
            foreach (var f in chunk.Features) if (f.X >= 0 && f.X < W) Mark(occupied, f.X, 1);
            int before = chunk.Features.Count;
            PlaceBounceLedge(chunk, heights, occupied, false);
            return chunk.Features.Count > before;
        }

        // 바람 선반 (2026-11-14): 청크 번호가 홀수면 버섯 대신 선반 오른쪽 바로 옆에 2칸 바람 구멍을 둔다.
        // 바람 기둥 꼭대기 = 선반 서는 칸 + RoomShaper.WindTopRise. 기둥 아래 2칸과 그 오른쪽 한 칸도 평평한 같은 바닥이어야 한다.
        // 자리가 없으면 버섯으로 놓는다. 번호만 보고 고르므로 난수열은 그대로다. 기능 테스트 맵(AddBounceLedge)은 늘 버섯.
        public static bool WantsWindLedge(ChunkData chunk) => (chunk.Index & 1) == 1;

        private static void PlaceBounceLedge(ChunkData chunk, int[] heights, bool[] occupied, bool allowWind = true)
        {
            if (chunk.Difficulty < BounceMinDifficulty) return;
            if (allowWind && WantsWindLedge(chunk) && TryPlaceLedge(chunk, heights, occupied, true)) return;
            TryPlaceLedge(chunk, heights, occupied, false);
        }

        private static bool TryPlaceLedge(ChunkData chunk, int[] heights, bool[] occupied, bool wind)
        {
            int center = W / 2;
            for (int k = 0; k < W; k++)
            {
                int x = center + ((k & 1) == 0 ? k / 2 : -(k / 2 + 1));
                int flatLo = x - BounceLedgeHalf;
                int flatHi = x + BounceLedgeHalf + (wind ? RoomShaper.WindVentWidth : 0); // 바람이면 선반 오른쪽 기둥 2칸까지
                if (flatLo - 1 < 4 || flatHi + 1 > W - 5) continue;
                int g = heights[x];
                int stand = g + BounceLedgeRise;
                if (stand + 2 > ChunkData.Height - 2) continue; // 선반 위로 몸이 들어갈 자리 (천장 줄 아래)
                if (wind && stand + RoomShaper.WindTopRise > ChunkData.Height - 2) continue;
                bool ok = true;
                for (int c = flatLo - 1; c <= flatHi + 1 && ok; c++)
                {
                    // 선반(과 바람 기둥) 아래는 평평한 같은 바닥, 양옆 한 칸도 바닥보다 높지 않게 (언덕에서 걸어 올라 닿지 않게)
                    if (c >= flatLo && c <= flatHi ? heights[c] != g : heights[c] > g) ok = false;
                    if (occupied[c]) ok = false; // 가시·적이 있는 곳은 피한다
                }
                // 근처 높은 언덕 위에서 보통 점프(약 2.4칸)로 선반에 닿지 않게 (언덕 청크용. 평지는 한 칸씩만 오르내려 늘 통과)
                for (int c = Math.Max(0, x - BounceLedgeHalf - BounceReachGuard); c <= Math.Min(W - 1, x + BounceLedgeHalf + BounceReachGuard) && ok; c++)
                    if (heights[c] > g + 3) ok = false;
                if (!ok) continue;

                for (int c = x - BounceLedgeHalf; c <= x + BounceLedgeHalf; c++) chunk.Tiles[c, stand - 1] = StageTile.Platform;
                if (wind)
                {
                    int ventX = x + BounceLedgeHalf + 1;
                    chunk.AddFeature(FeatureKind.WindVent, ventX, g, RoomShaper.WindVentWidth, stand + RoomShaper.WindTopRise - g);
                    chunk.AddFeature(FeatureKind.CoinPile, x - 1, stand);
                    Mark(occupied, ventX, RoomShaper.WindVentWidth);
                }
                else
                {
                    chunk.AddFeature(FeatureKind.BounceMushroom, x, g);
                    chunk.AddFeature(FeatureKind.CoinPile, x + 1, stand);
                }
                Mark(occupied, x, BounceLedgeHalf);
                return true;
            }
            return false;
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
            PlaceBounceLedge(chunk, heights, occupied); // 2026-11-14: 언덕에도 통통 버섯 덤 길 (평평한 골짜기에만, 난수 안 씀)
        }

        // 낭떠러지(바닥 없는 구멍) 사이의 섬을 건넌다. 뒤로 갈수록 섬 일부가 무너지는 발판으로 바뀐다.
        private static void BuildPits(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int current = chunk.EntryHeight;
            int x = 0;
            for (; x < 4; x++) heights[x] = current;

            var crumbles = new List<int[]>();
            int[] lastIsland = null; // 마지막 단단한 섬 (시작 X, 폭) — 동전 무더기 자리
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
                else lastIsland = new[] { x, width };
                for (int i = 0; i < width; i++) heights[x++] = crumble ? 0 : current;
            }

            for (; x < W; x++) heights[x] = current;
            ApplyHeights(chunk, heights);

            foreach (var c in crumbles)
            {
                chunk.AddFeature(FeatureKind.Crumble, c[0], c[1], c[2]);
                for (int i = 0; i < c[2]; i++) occupied[c[0] + i] = true;
            }

            // 동전 무더기: 가장 깊이 들어간(마지막) 단단한 섬 가운데. 낭떠러지를 건넌 보상. 적보다 먼저 자리를 잡는다 (난수를 쓰지 않음)
            if (chunk.Difficulty >= RiskyCoinMinDifficulty && lastIsland != null)
            {
                int coinX = lastIsland[0] + (lastIsland[1] - 1) / 2;
                chunk.AddFeature(FeatureKind.CoinPile, coinX, heights[coinX]);
                occupied[coinX] = true;
            }

            if (chunk.Difficulty > 0.3f) PlaceEnemies(chunk, heights, occupied, rng, 1);
        }

        // 섬·절벽 오르기 동전 무더기 문턱 (기둥 숲 PillarCoinMinDifficulty와 같은 값)
        public const float RiskyCoinMinDifficulty = 0.3f;

        // 기둥 숲: 바닥 없는 낭떠러지 위로 폭 1~2칸 기둥이 높낮이를 바꿔 가며 솟는다 (섬(Pits)보다 좁고 들쭉날쭉).
        // 간격 2칸이면 한 칸 오르거나 두 칸까지 내려가고, 3칸이면 같은 높이이거나 한 칸 내려간다 (점프 규칙: 오르며 건너면 2칸).
        // 난이도 PillarCrumbleMinDifficulty 이상이면 기둥 머리 하나가 무너지는 발판: 무너지면 한 칸 낮은 기둥 몸통에 내려선다.
        public const float PillarCrumbleMinDifficulty = 0.5f;
        public const int PillarMinTop = MinGround + 2; // 기둥이 너무 낮으면 낭떠러지 같지 않다

        private static void BuildPillars(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int current = chunk.EntryHeight;
            int x = 0;
            for (; x < 4; x++) heights[x] = current;

            var pillars = new List<int[]>(); // 시작 열, 폭, 높이
            while (W - 4 - x >= 5)
            {
                int gap = chunk.Difficulty >= 0.4f && rng.NextDouble() < 0.4 ? 3 : 2;
                int delta = gap == 3 ? PickDelta(rng, -1, 0) : PickDelta(rng, -2, -1, 0, 1, 1);
                int prev = current;
                // 낮게 들어왔으면 PillarMinTop까지 한 칸씩만 오른다. 오르는 칸이면 간격은 2 (오르며 3칸은 못 건넌다)
                current = Math.Max(Math.Min(PillarMinTop, prev + 1), Math.Min(MaxGround, prev + delta));
                if (current > prev) gap = 2;
                for (int i = 0; i < gap; i++) heights[x++] = 0;

                int width = Math.Min(chunk.Difficulty >= 0.35f && rng.NextDouble() < 0.5 ? 1 : 2, W - 4 - x);
                pillars.Add(new[] { x, width, current });
                for (int i = 0; i < width; i++) heights[x++] = current;
            }

            // 마지막 기둥 뒤 낭떠러지 한 번 더 (2칸) 건너 출구 평지
            if (x <= W - 6) { heights[x++] = 0; heights[x++] = 0; }
            for (; x < W; x++) heights[x] = current;

            // 무너지는 머리: 맨 처음·맨 끝이 아닌 폭 2 기둥 하나
            int[] crumbled = null;
            if (chunk.Difficulty >= PillarCrumbleMinDifficulty && pillars.Count >= 3)
            {
                var candidates = new List<int[]>();
                for (int i = 1; i < pillars.Count - 1; i++) if (pillars[i][1] == 2) candidates.Add(pillars[i]);
                if (candidates.Count > 0)
                {
                    crumbled = candidates[rng.Next(candidates.Count)];
                    for (int i = 0; i < crumbled[1]; i++) heights[crumbled[0] + i] = crumbled[2] - 1;
                }
            }

            ApplyHeights(chunk, heights);
            if (crumbled != null)
            {
                chunk.AddFeature(FeatureKind.Crumble, crumbled[0], crumbled[2] - 1, crumbled[1]);
                for (int i = 0; i < crumbled[1]; i++) occupied[crumbled[0] + i] = true;
            }

            // 동전 무더기: 가장 높은 기둥 머리 (무너지는 머리는 빼고, 같은 높이면 왼쪽 것). 낭떠러지 위를 건너는 보상
            if (chunk.Difficulty >= PillarCoinMinDifficulty)
            {
                int[] top = null;
                foreach (var p in pillars)
                    if (p != crumbled && (top == null || p[2] > top[2])) top = p;
                if (top != null)
                {
                    chunk.AddFeature(FeatureKind.CoinPile, top[0] + top[1] / 2, top[2]);
                    occupied[top[0] + top[1] / 2] = true;
                }
            }
        }

        public const float PillarCoinMinDifficulty = 0.3f;

        // 두 갈래 길: 평평한 아래 길에는 가시 줄과 적, 위에는 발판 계단(2칸씩)으로 오르는 발판 줄이 출구 앞까지 이어진다.
        // 위 길은 적이 없는 대신 발판 사이 틈(1~2칸)과 무너지는 발판(난이도가 오를수록 많이)을 건너야 한다. 떨어지면 아래 길로 이어 간다.
        // 아래 길의 머리 위 여유: 발판 줄 칸이 바닥 + TwinPathWalkRise - 1행이라 점프 최고 높이(2.45칸)에 머리가 닿지 않는다.
        public const int TwinPathWalkRise = 6;   // 위 길 윗면 = 바닥 + 6
        public const int TwinPathWalkStart = 9;  // 위 길 첫 발판 열 (앞은 계단)
        public const int TwinPathWalkEnd = W - 5; // 위 길 마지막 발판 열 (뒤 4칸은 내려설 출구 평지)

        private static void BuildTwinPath(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int ground = Math.Min(chunk.EntryHeight, MaxGround);
            Flat(heights, ground);
            ApplyHeights(chunk, heights);

            // 계단: 윗면 바닥 + 2, + 4 (한 번에 2칸씩)
            chunk.SetRow(3, 5, ground + 1, StageTile.Platform);
            chunk.SetRow(6, 8, ground + 3, StageTile.Platform);

            int row = ground + TwinPathWalkRise - 1;
            float crumbleChance = 0.25f + 0.5f * chunk.Difficulty;
            int x = TwinPathWalkStart;
            bool first = true;
            int coinX = -1; // 위 길의 마지막 단단한 발판 가운데 (동전 무더기 자리)
            while (x <= TwinPathWalkEnd)
            {
                int width = Math.Min(rng.Next(3, 6), TwinPathWalkEnd - x + 1);
                // 첫 발판은 계단에서 올라서는 곳이라 늘 단단하다. 너무 짧게 남은 끝 조각도 단단한 발판
                bool crumble = !first && width >= 2 && rng.NextDouble() < crumbleChance;
                if (crumble) chunk.AddFeature(FeatureKind.Crumble, x, row, width);
                else
                {
                    chunk.SetRow(x, x + width - 1, row, StageTile.Platform);
                    coinX = x + (width - 1) / 2;
                }
                first = false;
                x += width;
                if (x > TwinPathWalkEnd) break;
                x += rng.Next(1, 3); // 틈 1~2칸 (같은 높이라 걸어 점프로 넘는다)
            }

            // 위 길 끝 보상: 마지막 단단한 발판 위 동전 무더기 (적 없는 대신 틈·무너지는 발판을 건넌 몫). 첫 발판뿐이면 두지 않는다
            if (coinX > TwinPathWalkStart + 4) chunk.AddFeature(FeatureKind.CoinPile, coinX, row + 1);

            // 아래 길: 계단·출구를 피해 가시 줄, 그다음 적 (PlaceEnemies는 가시 근처를 피한다)
            Mark(occupied, 4, 4);
            int strips = 1 + (chunk.Difficulty >= 0.5f ? 1 : 0);
            for (int i = 0; i < strips; i++) PlaceSpikeStrip(chunk, heights, occupied, rng, 2);
            PlaceEnemies(chunk, heights, occupied, rng, TravelEnemyCount(rng, chunk.Difficulty, 1));
            PlaceTwinPathMushroom(chunk, ground, occupied);
        }

        // 두 갈래 길 아래 길의 통통 버섯 (2026-11-14): 위 길에서 떨어져도 버섯을 밟아 다시 오를 수 있다.
        // 가시·적을 다 놓은 뒤 비어 있는 열에 두어 난수·기존 배치를 바꾸지 않는다 (가운데에서 가까운 열부터).
        // 튕김(갓 윗면 + 7.5칸)이 위 길 윗면(바닥 + 6)보다 넉넉히 높고, 위 길 발판은 아래에서 통과해 올라선다.
        public const int TwinPathMushroomMinX = TwinPathWalkStart + 2;
        public const int TwinPathMushroomMaxX = TwinPathWalkEnd - 2;

        private static void PlaceTwinPathMushroom(ChunkData chunk, int ground, bool[] occupied)
        {
            if (chunk.Difficulty < BounceMinDifficulty) return;
            int center = (TwinPathMushroomMinX + TwinPathMushroomMaxX) / 2;
            for (int k = 0; k <= TwinPathMushroomMaxX - TwinPathMushroomMinX; k++)
            {
                int x = center + ((k & 1) == 0 ? k / 2 : -(k / 2 + 1));
                if (x < TwinPathMushroomMinX || x > TwinPathMushroomMaxX) continue;
                if (IsNear(occupied, x, 1)) continue; // 가시·적 곁은 피한다
                chunk.AddFeature(FeatureKind.BounceMushroom, x, ground);
                Mark(occupied, x, 1);
                return;
            }
        }

        // 평지에 가시 줄이 이어진다. 3칸짜리 가시 위에는 발판을 놓아 밟고 건널 수 있게 한다.
        private static void BuildSpikeField(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);

            int coinX = -1; // 마지막 가시 위 발판의 가운데 (동전 무더기 자리)
            for (int x = 4; x < W - 6;)
            {
                x += rng.Next(2, 5);
                int width = chunk.Difficulty >= 0.5f && rng.NextDouble() < 0.5 ? 3 : 2;
                if (x + width > W - 4) break;

                chunk.AddFeature(FeatureKind.Spikes, x, ground, width);
                for (int i = 0; i < width; i++) occupied[x + i] = true;

                // 발판 칸은 바닥보다 한 칸 위 (윗면 = 바닥 + 2, 점프 최고 높이 2.45 안쪽).
                if (width == 3 || rng.NextDouble() < 0.4)
                {
                    chunk.SetRow(x - 1, x + width, ground + 1, StageTile.Platform);
                    coinX = x + (width - 1) / 2;
                }
                x += width;
            }

            // 동전 무더기: 마지막 가시 위 발판 한가운데 (가시 바로 위 좁은 발판에 올라선 보상). 난수를 쓰지 않아 기존 지형 그대로
            if (chunk.Difficulty >= RiskyCoinMinDifficulty && coinX >= 0)
                chunk.AddFeature(FeatureKind.CoinPile, coinX, ground + 2);

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

            // 동전 무더기: 절벽 위 평지 끝 (지키는 적 뒤). 오른 보상. 난수를 쓰지 않아 기존 지형 그대로
            int climbCoinX = plateauEnd - 2;
            if (chunk.Difficulty >= RiskyCoinMinDifficulty && climbCoinX > guardX && heights[climbCoinX] == top)
                chunk.AddFeature(FeatureKind.CoinPile, climbCoinX, top);
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

            PlaceStalactites(chunk, heights, occupied, rng);

            // 동전 무더기: 마지막 종유석 바로 아래 바닥 (주우러 가면 종유석이 흔들린다 — 피하며 줍는 보상).
            // 종유석이 없는 동굴(난이도 문턱 아래)에는 두지 않는다. 난수·occupied를 건드리지 않아 기존 지형·적 그대로
            int stalactiteX = -1;
            foreach (var f in chunk.Features) if (f.Kind == FeatureKind.Stalactite) stalactiteX = f.X;
            if (stalactiteX >= 0) chunk.AddFeature(FeatureKind.CoinPile, stalactiteX, heights[stalactiteX]);

            PlaceEnemies(chunk, heights, occupied, rng, TravelEnemyCount(rng, chunk.Difficulty, 1));
        }

        // 떨어지는 종유석: 난이도 StalactiteMinDifficulty 이상 동굴 천장에 1개, StalactiteSecondDifficulty 이상이면 2개.
        // 분출구와 2칸 넘게 떨어진 열, 서로 6칸 이상 떨어진 열. 매달린 칸 = 천장 바로 아래 빈 칸.
        public const float StalactiteMinDifficulty = 0.35f;
        public const float StalactiteSecondDifficulty = 0.7f;

        private static void PlaceStalactites(ChunkData chunk, int[] heights, bool[] occupied, Random rng)
        {
            if (chunk.Difficulty < StalactiteMinDifficulty) return;
            int count = chunk.Difficulty >= StalactiteSecondDifficulty ? 2 : 1;
            int lastX = -100;
            for (int placed = 0, tries = 0; placed < count && tries < 40; tries++)
            {
                int x = rng.Next(5, W - 5);
                if (IsNear(occupied, x, 2) || Math.Abs(x - lastX) < 6) continue;
                int y = heights[x];
                while (y < H && !chunk.IsSolid(x, y)) y++;
                if (y >= H || y - 1 <= heights[x]) continue; // 천장 없음 / 너무 낮음
                chunk.AddFeature(FeatureKind.Stalactite, x, y - 1);
                lastX = x;
                placed++;
            }
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

            // 변형: 깜빡 발판 다리 (Docs/ContentIdeas.md 퍼즐 1). 난이도 40% 이상에서 절반 확률, 발판이 왼쪽부터 차례로 깜빡인다
            bool blink = chunk.Difficulty >= BlinkBridgeMinDifficulty && rng.Next(2) == 0;
            for (int i = 0; i < crumbles.Count; i++)
            {
                var c = crumbles[i];
                if (blink) chunk.AddFeature(FeatureKind.BlinkPlatform, c[0], c[1], c[2], i);
                else chunk.AddFeature(FeatureKind.Crumble, c[0], c[1], c[2]);
            }
        }

        public const float BlinkBridgeMinDifficulty = 0.4f;

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
            // 깊이 후반에는 두 속성을 순서대로 맞혀야 열리는 봉인문 (B가 영창을 두 번, 서로 다른 속성으로).
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
            // 멈춘 발판은 흐릿한 잠든 발판이라 밟을 수 없고(그 위에서 뛰어 거리를 줄이지 못하게, MovingPlatform), 낭떠러지에는
            // 마력 안개가 깔려 순간이동·대시로 건너뛰지 못한다 (안개 근처 공중 포함, BlinkWard). 폭 9~10칸 > 달리기 점프(약 7.3칸) (2026-11-12 플레이테스트: 대시·순간이동으로 건너짐).
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
        public const int SwitchBridgeOneIn = 2; // 깊이 0.5 이후 한 발판 낭떠러지의 절반

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

        // 퍼즐 방 "방어막 다리" (2026-11-14, Docs/PuzzleRoomDesign.md 2절 2번). 바닥 길은 비워 두고(문은 막지 않음), 위에 단단한 선반:
        //   계단 발판(3~6열, 선 칸 바닥 + 2) → 선반(8~27열, 선 칸 바닥 + 4). 선반 위 11~15열·18~22열에 서로 다른 속성 분출 통로 둘,
        //   사이 16~17열은 숨 돌리는 칸(방어막을 바꿔 두른다), 끝 24~26열에 동전 무더기 셋.
        // 분출은 쉬지 않는다: 그 속성 방어막(같은 속성 마법 피해 무효)을 두른 A만 지나가고, 아니면 피해 조금 + 통로 입구(9열)로 튕겨 돌아간다.
        // 선반이 단단해서 아래에서 뚫고 오를 수 없고, 마력 안개(11~27열)가 순간이동·대시로 건너뛰는 것을 막는다.
        // 방어막은 3초·마나 25라 둘째 통로 앞에서 다시 둘러야 한다 → B가 마나를 아껴야 하는 협동 퍼즐.
        public const int ShieldStairX0 = 3, ShieldStairX1 = 6;    // 계단 발판 (행 = 바닥 + 1)
        public const int ShieldLedgeX0 = 8, ShieldLedgeX1 = 27;   // 선반 (행 = 바닥 + 3, 단단함)
        public const int ShieldLedgeRise = 4;                     // 선반 위 선 칸 = 바닥 + 4
        public const int ShieldJet1X = 11, ShieldJet2X = 18, ShieldJetWidth = 5;
        public const int ShieldReturnX = 9;
        public const int ShieldCoinX0 = 24, ShieldCoinCount = 3;

        private static void BuildShieldBridge(ChunkData chunk, int[] heights, Random rng)
        {
            int ground = chunk.EntryHeight;
            Flat(heights, ground);
            ApplyHeights(chunk, heights);
            AddShieldBridge(chunk, ground, rng);
        }

        private static void AddShieldBridge(ChunkData chunk, int ground, Random rng)
        {
            int stand = ground + ShieldLedgeRise;
            chunk.SetRow(ShieldStairX0, ShieldStairX1, ground + 1, StageTile.Platform);
            chunk.SetRow(ShieldLedgeX0, ShieldLedgeX1, stand - 1, StageTile.Solid);
            int first = rng.Next(GateElementCount);
            int second = (first + 1 + rng.Next(GateElementCount - 1)) % GateElementCount;
            chunk.Features.Add(new ChunkFeature
            {
                Kind = FeatureKind.ElementJet, X = ShieldJet1X, Y = stand, Width = ShieldJetWidth, ElementIndex = first, Direction = ShieldReturnX
            });
            chunk.Features.Add(new ChunkFeature
            {
                Kind = FeatureKind.ElementJet, X = ShieldJet2X, Y = stand, Width = ShieldJetWidth, ElementIndex = second, Direction = ShieldReturnX
            });
            chunk.AddFeature(FeatureKind.BlinkWard, ShieldJet1X, 0, ShieldLedgeX1 - ShieldJet1X + 1);
            for (int i = 0; i < ShieldCoinCount; i++) chunk.AddFeature(FeatureKind.CoinPile, ShieldCoinX0 + i, stand);
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
