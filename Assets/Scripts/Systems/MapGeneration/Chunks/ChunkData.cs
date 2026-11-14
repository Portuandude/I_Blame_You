using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 청크식 스테이지 생성의 순수 데이터 (UnityEngine 없이 돌아가서 Tools/StageGen에서 그대로 검증/미리보기한다).
    // 스테이지는 가로로 이어 붙인 청크들이고, 청크 하나는 Width × Height 타일(1타일 = 1유닛) 격자다. y = 0이 맨 아래.

    public enum StageTile : byte
    {
        Empty,
        Solid,    // 밟을 수 있고 통과할 수 없는 땅
        Platform  // 아래에서 뚫고 올라갈 수 있는 발판 (윗면만 밟힘)
    }

    public enum ChunkKind
    {
        Start,
        Plains,        // 완만한 언덕
        Hills,         // 높낮이 차가 큰 언덕 + 화살 함정
        Pits,          // 낭떠러지 사이를 건너는 섬
        SpikeField,    // 가시 밭 + 발판
        Climb,         // 발판을 밟고 올라가는 절벽
        Cave,          // 천장이 낮은 동굴 + 화염 분출구
        CrumbleBridge, // 밟으면 무너지는 발판 다리
        ElementGate,   // 맵 퍼즐: 정해진 속성 마법을 맞혀야 열리는 봉인문
        MovingBridge,  // 컨트롤 퍼즐: 점프로 못 건너는 낭떠러지를 오가는 움직이는 발판을 타고 건넌다
        Arena,         // 들어가면 문이 닫히고 적이 나오는 전투 구역
        EliteArena,    // 정예 전투 구역
        Rest,          // 숨 고르기 평지 (체크포인트는 2026-11-10 제거). 곁방 지형으로도 쓴다
        Boss,          // 마지막 전투 + 출구
        SwitchBridge,  // 이중 스위치 퍼즐 (갈아타기 발판 + 룬 두 개). 판마다 하나 보장 (StagePlanner, 2026-11-12 플레이테스트: 80% 이후에만 나와 못 봄)
        Pillars,       // 기둥 숲: 낭떠러지 위 폭 1~2칸 기둥을 높낮이 바꿔 가며 건넌다 (진행도 20%+, 레벨 재설계 2차)
        TwinPath       // 두 갈래 길: 아래는 가시 + 적, 위는 발판 계단 → 발판 줄(무너지는 발판 섞임). 싸울지 뛸지 고른다 (진행도 25%+)
    }

    public enum FeatureKind
    {
        Enemy,
        Spikes,      // Width칸 가시. (X, Y)는 가시가 놓인 칸 (바닥 바로 위)
        FlameJet,    // 바닥 분출구. (X, Y)는 분출구 칸
        ArrowTrap,   // 벽에 박힌 화살 발사대. Direction 쪽으로 쏜다
        Crumble,     // Width칸 무너지는 발판. (X, Y)는 발판이 차지하는 칸 (윗면 = Y + 1)
        PlayerStart,
        Arena,       // X ~ X + Width - 1 칸이 전투 구역. 이 청크의 Deferred 적이 그 안에서 나온다
        Exit,
        ElementGate,  // X 열을 바닥(Y)부터 천장까지 막는 봉인문 (씬에서만 생기는 벽이라 검증에서는 열린 것으로 본다). ElementIndex 속성 마법으로 열린다
        MovingPlatform, // Width칸 발판이 Y 행(윗면 = Y + 1)에서 X부터 오른쪽으로 Direction칸까지 오간다. 검증에서는 지나는 모든 칸을 발판으로 본다
        PlatformSwitch, // 마법 스위치 룬. (X, Y)는 룬이 선 칸 (바닥 바로 위). ElementIndex 속성 마법을 맞히면 짝 발판이 잠시 움직인다.
                        // 짝: 청크의 k번째 PlatformSwitch ↔ k번째 SwitchDriven MovingPlatform (Features 순서)
                        // 몸이 지나가는 트리거라 길을 막지 않는다 (검증에서는 무시)
        BlinkWard,      // 마력 안개: X ~ X + Width - 1 열 전체(바닥~천장). 안에서는 순간이동·대시가 흩어진다 (스위치 발판 낭떠러지를 건너뛰지 못하게).
                        // 검증기는 걷기 점프 기준이라 무시한다
        Reward,         // 회복 구슬 (ArenaReward와 같은 것). (X, Y)는 구슬이 놓이는 서는 칸. 순간이동 지름길 선반에 놓인다
        BlinkPlatform,  // 깜빡 발판: Width칸 발판 (Crumble과 같은 자리 규칙, 윗면 = Y + 1). 박자에 맞춰 나타났다 사라진다.
                        // Direction = 박자 순번 (0, 1, 2…: 왼쪽부터 차례로 늦게). 검증기는 발판으로 본다 (보일 때 밟으면 되므로)
        Stalactite,     // 동굴 종유석: (X, Y)는 천장 바로 아래 매달린 칸. 플레이어가 아래로 오면 떨어진다. 몸이 없어 길을 막지 않는다 (검증에서는 무시)
        CoinPile,       // 동전 무더기: (X, Y)는 무더기가 놓이는 서는 칸. 닿으면 골드(GoldDropTable.CoinPileGold)를 받는다. 방마다 한 번만. 몸이 없다 (검증에서는 무시)
        BounceMushroom, // 통통 버섯: (X, Y)는 버섯이 선 서는 칸. 위에서 내려앉으면 높이 튕겨 올린다. 몸이 없는 덤 길이라 검증에서는 무시
        WindVent        // 바람 구멍: (X, Y)는 환풍구 왼쪽 서는 칸, X ~ X + Width - 1 열에서 Y부터 Direction칸 높이까지 바람 기둥이 플레이어를 띄운다.
                        // 몸이 없는 덤 길이라 검증에서는 무시
    }

    public enum StageEnemy
    {
        Slime,
        Goblin,
        GoblinArcher,
        GoblinSoldier,
        MotherSlime,
        GoblinWarrior,
        SlimeKing,   // 스테이지 1 보스
        SlimeKnightFire,  // 중간보스 쌍둥이 슬라임 기사 (불: 돌진)
        SlimeKnightWater  // 중간보스 쌍둥이 슬라임 기사 (물: 방패)
    }

    public struct ChunkFeature
    {
        public FeatureKind Kind;
        public int X;
        public int Y;
        public int Width;
        public int Direction;   // ArrowTrap: -1 왼쪽, 1 오른쪽
        public StageEnemy Enemy;
        public bool Deferred;   // 전투 구역 적: 처음부터 두지 않고 구역에 들어가면 나온다
        public int Id;          // 스테이지 전체에서 유일 (적 처치 기록용)
        public int ElementIndex; // ElementGate: 여는 속성 (Element 순서: 0 불, 1 물, 2 바람. 저장·생성 데이터라 int로 둔다. ChunkStreamer에서 Element로 바꾼다)
        public bool HasSecondElement;  // ElementGate: 두 속성 봉인문 (ElementIndex → SecondElementIndex 순서로 맞혀야 열림)
        public int SecondElementIndex; // ElementGate: 둘째 속성 (HasSecondElement일 때만, ElementIndex와 다름)
        public bool SwitchDriven;      // MovingPlatform: 처음엔 멈춰 있고 같은 청크의 PlatformSwitch를 맞혀야 움직인다
        public bool ShieldFirst;       // ElementGate: 협동 봉인문. 첫 속성(ElementIndex)은 A가 그 속성 방어막을 두르고 문에 닿아서, 둘째(SecondElementIndex)는 B 마법으로 푼다
    }

    public class ChunkData
    {
        // 구간 하나(화면 하나)의 폭. 넓은 방은 구간 여러 개를 이어 붙인 청크라 실제 폭은 Cols (Stitch, 2026-11-12 "큰 방").
        public const int Width = 32;
        public const int Height = 24;

        public readonly int Cols;
        public readonly StageTile[,] Tiles;
        public readonly List<ChunkFeature> Features = new List<ChunkFeature>();

        public ChunkData() : this(Width) { }

        public ChunkData(int cols)
        {
            Cols = cols;
            Tiles = new StageTile[cols, Height];
        }

        public int Index;
        public ChunkKind Kind;
        public float Difficulty;  // 0(시작) ~ 1(끝)
        public int EntryHeight;   // 왼쪽 끝 바닥 높이 (= 서 있는 칸의 y)
        public int ExitHeight;    // 오른쪽 끝 바닥 높이 (다음 청크의 EntryHeight)
        public int Attempts = 1;  // 검증을 통과할 때까지 다시 만든 횟수

        public StageTile Get(int x, int y)
        {
            if (x < 0 || x >= Cols || y < 0 || y >= Height) return StageTile.Empty;
            return Tiles[x, y];
        }

        public bool IsSolid(int x, int y) => Get(x, y) == StageTile.Solid;

        // x열의 맨 위 땅 바로 위 칸 (땅이 없으면 0 = 낭떠러지).
        public int SurfaceAt(int x)
        {
            for (int y = Height - 1; y >= 0; y--)
            {
                if (Tiles[x, y] == StageTile.Solid) return y + 1;
            }

            return 0;
        }

        // 봉인문 높이: (x, floorY)부터 위로 처음 막힌 칸 바로 아래까지 (방 지역 좌표, 칸 수).
        // 위가 끝까지 비어 있으면 청크 위 GateOverTop칸까지 막아 점프로 넘지 못하게 한다 (방 사이 간격 8유닛 안).
        // 월드 높이가 아니라 방 안 높이로 재야 한다: 방 격자에서는 방마다 원점 y가 달라서 위·아래 방 지형까지 뚫고 나간다.
        public const int GateOverTop = 2;

        public int GateHeight(int x, int floorY)
        {
            for (int y = Math.Max(0, floorY); y < Height; y++)
                if (IsSolid(x, y)) return y - floorY;
            return Height - floorY + GateOverTop;
        }

        public void FillColumn(int x, int height)
        {
            for (int y = 0; y < Height; y++) Tiles[x, y] = y < height ? StageTile.Solid : StageTile.Empty;
        }

        public void SetRow(int x0, int x1, int y, StageTile tile)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (x >= 0 && x < Cols && y >= 0 && y < Height) Tiles[x, y] = tile;
            }
        }

        public void AddFeature(FeatureKind kind, int x, int y, int width = 1, int direction = 0)
        {
            Features.Add(new ChunkFeature { Kind = kind, X = x, Y = y, Width = width, Direction = direction });
        }

        public void AddEnemy(StageEnemy enemy, int x, int y, bool deferred = false)
        {
            // 지금 있는 적 번호 다음 번호 (중간보스 방처럼 적을 뺐다가 다시 넣어도 겹치지 않게)
            int id = Index * 100;
            foreach (var feature in Features) if (feature.Kind == FeatureKind.Enemy) id = Math.Max(id, feature.Id + 1);
            Features.Add(new ChunkFeature
            {
                Kind = FeatureKind.Enemy, X = x, Y = y, Width = 1, Enemy = enemy, Deferred = deferred, Id = id
            });
        }

        // 구간들을 왼쪽부터 이어 붙여 넓은 방 청크 하나로 만든다 (RoomShaper.BuildHorizontal).
        // 타일은 그대로 옮기고 기능 X는 그 구간이 시작하는 열만큼 민다. 입구 높이 = 첫 구간, 출구 높이 = 마지막 구간.
        // 적 번호는 Index*100부터 다시 매긴다 (구간마다 같은 방 번호로 만들어 번호가 겹치므로).
        // 종류·난이도·번호·시도 횟수는 main(주 구간: 전투·보스 등 방 종류를 정하는 구간)의 것을 쓴다.
        public static ChunkData Stitch(IList<ChunkData> parts, ChunkData main)
        {
            int cols = 0;
            foreach (var part in parts) cols += part.Cols;
            var chunk = new ChunkData(cols)
            {
                Index = main.Index, Kind = main.Kind, Difficulty = main.Difficulty, Attempts = main.Attempts,
                EntryHeight = parts[0].EntryHeight, ExitHeight = parts[parts.Count - 1].ExitHeight
            };
            int offset = 0;
            int enemyId = main.Index * 100;
            foreach (var part in parts)
            {
                for (int x = 0; x < part.Cols; x++)
                    for (int y = 0; y < Height; y++)
                        chunk.Tiles[offset + x, y] = part.Tiles[x, y];
                foreach (var feature in part.Features)
                {
                    var f = feature;
                    f.X += offset;
                    if (f.Kind == FeatureKind.Enemy) f.Id = enemyId++;
                    chunk.Features.Add(f);
                }
                offset += part.Cols;
            }
            return chunk;
        }

        public bool HasFeature(FeatureKind kind)
        {
            foreach (var feature in Features) if (feature.Kind == kind) return true;
            return false;
        }
    }

    // 생성된 스테이지 전체.
    public class StageData
    {
        public readonly List<ChunkData> Chunks = new List<ChunkData>();
        public int Seed;
        public float EstimatedSeconds;

        public int WidthTiles
        {
            get
            {
                int total = 0;
                foreach (var chunk in Chunks) total += chunk.Cols;
                return total;
            }
        }
    }
}
