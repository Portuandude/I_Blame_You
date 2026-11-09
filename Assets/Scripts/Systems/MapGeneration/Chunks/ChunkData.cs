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
        Rest,          // 체크포인트 (회복)
        Boss           // 마지막 전투 + 출구
    }

    public enum FeatureKind
    {
        Enemy,
        Spikes,      // Width칸 가시. (X, Y)는 가시가 놓인 칸 (바닥 바로 위)
        FlameJet,    // 바닥 분출구. (X, Y)는 분출구 칸
        ArrowTrap,   // 벽에 박힌 화살 발사대. Direction 쪽으로 쏜다
        Crumble,     // Width칸 무너지는 발판. (X, Y)는 발판이 차지하는 칸 (윗면 = Y + 1)
        Checkpoint,
        PlayerStart,
        Arena,       // X ~ X + Width - 1 칸이 전투 구역. 이 청크의 Deferred 적이 그 안에서 나온다
        Exit,
        ElementGate,  // X 열을 바닥(Y)부터 천장까지 막는 봉인문 (씬에서만 생기는 벽이라 검증에서는 열린 것으로 본다). ElementIndex 속성 마법으로 열린다
        MovingPlatform // Width칸 발판이 Y 행(윗면 = Y + 1)에서 X부터 오른쪽으로 Direction칸까지 오간다. 검증에서는 지나는 모든 칸을 발판으로 본다
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
    }

    public class ChunkData
    {
        public const int Width = 32;
        public const int Height = 24;

        public readonly StageTile[,] Tiles = new StageTile[Width, Height];
        public readonly List<ChunkFeature> Features = new List<ChunkFeature>();

        public int Index;
        public ChunkKind Kind;
        public float Difficulty;  // 0(시작) ~ 1(끝)
        public int EntryHeight;   // 왼쪽 끝 바닥 높이 (= 서 있는 칸의 y)
        public int ExitHeight;    // 오른쪽 끝 바닥 높이 (다음 청크의 EntryHeight)
        public int Attempts = 1;  // 검증을 통과할 때까지 다시 만든 횟수

        public StageTile Get(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return StageTile.Empty;
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

        public void FillColumn(int x, int height)
        {
            for (int y = 0; y < Height; y++) Tiles[x, y] = y < height ? StageTile.Solid : StageTile.Empty;
        }

        public void SetRow(int x0, int x1, int y, StageTile tile)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height) Tiles[x, y] = tile;
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

        public int WidthTiles => Chunks.Count * ChunkData.Width;
    }
}
