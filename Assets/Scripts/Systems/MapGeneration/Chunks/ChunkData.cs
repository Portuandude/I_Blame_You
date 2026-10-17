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
        Exit
    }

    public enum StageEnemy
    {
        Slime,
        Goblin,
        GoblinArcher,
        GoblinSoldier,
        MotherSlime,
        GoblinWarrior
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
            int count = 0;
            foreach (var feature in Features) if (feature.Kind == FeatureKind.Enemy) count++;
            Features.Add(new ChunkFeature
            {
                Kind = FeatureKind.Enemy, X = x, Y = y, Width = 1, Enemy = enemy, Deferred = deferred, Id = Index * 100 + count
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
