// 스테이지 생성기 검증/미리보기 (Unity 없이 실행). Assets/Scripts/Systems/MapGeneration/Chunks의 순수 C# 코드를 그대로 컴파일한다.
//   mcs -out:/tmp/StagePreview.exe Tools/StageGen/StagePreview.cs Assets/Scripts/Systems/MapGeneration/Chunks/*.cs
//   mono /tmp/StagePreview.exe <시드> <미리보기 텍스트 경로>   → python3 Tools/StageGen/render_stage.py <텍스트> <png>
using System;
using System.IO;
using System.Linq;
using System.Text;
using IBlameYou.Systems;

public static class StagePreview
{
    private const float TargetSeconds = 30f * 60f;

    public static int Main(string[] args)
    {
        int previewSeed = args.Length > 0 ? int.Parse(args[0]) : 0;
        string previewPath = args.Length > 1 ? args[1] : null;

        // 여러 시드로 돌려서 모든 청크가 검증을 통과하는지, 대체 청크가 몇 번 나오는지 본다.
        int stages = 300, chunks = 0, fallbacks = 0, invalid = 0, maxAttempts = 0;
        double minMinutes = double.MaxValue, maxMinutes = 0;
        var kindCounts = new int[Enum.GetValues(typeof(ChunkKind)).Length];
        for (int seed = 0; seed < stages; seed++)
        {
            var stage = StageGenerator.Generate(seed, TargetSeconds);
            minMinutes = Math.Min(minMinutes, stage.EstimatedSeconds / 60.0);
            maxMinutes = Math.Max(maxMinutes, stage.EstimatedSeconds / 60.0);
            for (int i = 0; i < stage.Chunks.Count; i++)
            {
                var chunk = stage.Chunks[i];
                chunks++;
                kindCounts[(int)chunk.Kind]++;
                if (chunk.Attempts == 0) fallbacks++;
                maxAttempts = Math.Max(maxAttempts, chunk.Attempts);
                if (!StageValidator.Validate(chunk, out string reason))
                {
                    invalid++;
                    Console.WriteLine($"seed {seed} chunk {i} {chunk.Kind}: {reason}");
                }
                if (i > 0 && stage.Chunks[i - 1].ExitHeight != chunk.EntryHeight)
                {
                    invalid++;
                    Console.WriteLine($"seed {seed} chunk {i}: 이음새 높이가 다름");
                }
            }
        }

        Console.WriteLine($"스테이지 {stages}개, 청크 {chunks}개 (평균 {chunks / (double)stages:0.0}), 대체 청크 {fallbacks}, 검증 실패 {invalid}, 최대 시도 {maxAttempts}");
        Console.WriteLine($"예상 플레이 시간 {minMinutes:0.0}~{maxMinutes:0.0}분");
        foreach (ChunkKind kind in Enum.GetValues(typeof(ChunkKind)))
        {
            Console.WriteLine($"  {kind}: {kindCounts[(int)kind]}");
        }

        if (previewPath != null) WritePreview(StageGenerator.Generate(previewSeed, TargetSeconds), previewPath);
        return invalid == 0 ? 0 : 1;
    }

    // 첫 줄: 청크 종류 목록, 다음 줄부터 맨 위 행부터 한 글자 = 한 타일.
    private static void WritePreview(StageData stage, string path)
    {
        int width = stage.WidthTiles;
        var grid = new char[width, ChunkData.Height];
        for (int x = 0; x < width; x++) for (int y = 0; y < ChunkData.Height; y++) grid[x, y] = '.';

        foreach (var chunk in stage.Chunks)
        {
            int ox = chunk.Index * ChunkData.Width;
            for (int x = 0; x < ChunkData.Width; x++)
            {
                for (int y = 0; y < ChunkData.Height; y++)
                {
                    if (chunk.Tiles[x, y] == StageTile.Solid) grid[ox + x, y] = '#';
                    else if (chunk.Tiles[x, y] == StageTile.Platform) grid[ox + x, y] = '=';
                }
            }

            foreach (var f in chunk.Features)
            {
                switch (f.Kind)
                {
                    case FeatureKind.Spikes: for (int i = 0; i < f.Width; i++) grid[ox + f.X + i, f.Y] = '^'; break;
                    case FeatureKind.Crumble: for (int i = 0; i < f.Width; i++) grid[ox + f.X + i, f.Y] = '~'; break;
                    case FeatureKind.FlameJet: grid[ox + f.X, f.Y] = 'F'; break;
                    case FeatureKind.ArrowTrap: grid[ox + f.X, f.Y] = f.Direction < 0 ? '<' : '>'; break;
                    case FeatureKind.Checkpoint: grid[ox + f.X, f.Y] = 'C'; break;
                    case FeatureKind.PlayerStart: grid[ox + f.X, f.Y] = 'P'; break;
                    case FeatureKind.Exit: grid[ox + f.X, f.Y] = 'X'; break;
                    case FeatureKind.Enemy: grid[ox + f.X, f.Y] = EnemyChar(f.Enemy, f.Deferred); break;
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", stage.Chunks.Select(c => c.Kind.ToString())));
        for (int y = ChunkData.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++) sb.Append(grid[x, y]);
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"미리보기: 시드 {stage.Seed}, 청크 {stage.Chunks.Count}개, 예상 {stage.EstimatedSeconds / 60f:0.0}분 → {path}");
    }

    private static char EnemyChar(StageEnemy enemy, bool deferred)
    {
        char c;
        switch (enemy)
        {
            case StageEnemy.Slime: c = 's'; break;
            case StageEnemy.Goblin: c = 'g'; break;
            case StageEnemy.GoblinArcher: c = 'a'; break;
            case StageEnemy.GoblinSoldier: c = 'o'; break;
            case StageEnemy.MotherSlime: c = 'm'; break;
            default: c = 'w'; break;
        }

        return deferred ? char.ToUpper(c) : c;
    }
}
