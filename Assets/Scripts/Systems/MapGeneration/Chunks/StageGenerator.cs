namespace IBlameYou.Systems
{
    // 시드 하나로 스테이지 전체(청크 목록)를 만든다. 청크 데이터는 가벼워서 시작할 때 전부 만들어 두고,
    // 씬 오브젝트(콜라이더/그림/적)는 ChunkStreamer가 플레이어 근처 청크만 그때그때 만들고 치운다.
    public static class StageGenerator
    {
        public const int StartHeight = 5;
        private const int MaxAttempts = 24;

        public static StageData Generate(int seed, float targetSeconds)
        {
            var stage = new StageData { Seed = seed };
            var plan = StagePlanner.Plan(seed, targetSeconds);

            int height = StartHeight;
            for (int index = 0; index < plan.Count; index++)
            {
                var kind = plan[index].Key;
                float difficulty = plan[index].Value;

                ChunkData chunk = null;
                for (int attempt = 0; attempt < MaxAttempts && chunk == null; attempt++)
                {
                    var candidate = ChunkGenerator.Generate(seed, index, kind, difficulty, height, attempt);
                    if (StageValidator.Validate(candidate, out _)) chunk = candidate;
                }

                if (chunk == null) chunk = ChunkGenerator.Fallback(index, kind, difficulty, height);

                stage.Chunks.Add(chunk);
                stage.EstimatedSeconds += StagePlanner.EstimatedSeconds(kind);
                height = chunk.ExitHeight;
            }

            return stage;
        }
    }
}
