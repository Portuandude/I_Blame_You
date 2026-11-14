namespace IBlameYou.Systems
{
    // 기능 테스트 모드 맵 (2026-11-12 플레이테스트 피드백: 7·8·11·19번 "테스트 불가").
    // 적이 없는 짧은 외길: 텅 빈 방 → 통통 버섯·바람 구멍 방(2026-11-14) → 협동 봉인문(A 방어막 + B 마법) → 이중 스위치 발판 → 버섯 탑 방(오른쪽 벽, 2026-11-14).
    // 몬스터·유물은 이 맵 위에서 FeatureTestTools(키)로 불러 시험한다. 순수 C#이라 EditMode 테스트에서 그대로 검증한다.
    public static class FeatureTestStage
    {
        public const int Seed = 4242; // 항상 같은 맵 (지형 모양만 시드를 탄다)
        public const int Height = StageGenerator.StartHeight;
        private const int MaxAttempts = 24;

        // 순서 = 청크 번호. FeatureTestTools의 "구역으로 이동" 키(1~5)가 이 순서를 쓴다.
        public static readonly ChunkKind[] Order =
        {
            ChunkKind.Start, ChunkKind.Rest, ChunkKind.ElementGate, ChunkKind.SwitchBridge, ChunkKind.Rest
        };

        public const int BounceArea = 1; // 통통 버섯 방 (청크 번호)
        public const int TowerArea = 4;  // 버섯 탑 방 (청크 번호)

        public static readonly string[] Names = { "텅 빈 방", "통통 버섯·바람 방", "협동 봉인문", "이중 스위치 발판", "버섯 탑 방" };

        public static StageData Build()
        {
            var stage = new StageData { Seed = Seed };
            int height = Height;
            for (int index = 0; index < Order.Length; index++)
            {
                var kind = Order[index];
                ChunkData chunk = null;
                for (int attempt = 0; attempt < MaxAttempts && chunk == null; attempt++)
                {
                    var candidate = ChunkGenerator.Generate(Seed, index, kind, 1f, height, attempt);
                    candidate.Features.RemoveAll(f => f.Kind == FeatureKind.Enemy); // 적은 키로만 부른다
                    if (StageValidator.Validate(candidate, out _)) chunk = candidate;
                }

                if (chunk == null) chunk = ChunkGenerator.Fallback(index, kind, 1f, height);
                if (kind == ChunkKind.ElementGate) MakeCoopGate(chunk);
                if (index == BounceArea)
                {
                    ChunkGenerator.AddBounceLedge(chunk); // 평평한 빈 방 가운데에 버섯 + 높은 선반 + 동전
                    AddWindVent(chunk);                   // 오른쪽에 바람 구멍 + 선반 + 동전
                }
                if (index == TowerArea) AddMushroomTower(chunk); // 세로 방 버섯 탑을 가로 방에서 바로 시험 (무너지는 P2 + 저격 선반)
                if (index == Order.Length - 1) chunk.FillColumn(ChunkData.Width - 1, ChunkData.Height); // 끝 벽

                stage.Chunks.Add(chunk);
                height = chunk.ExitHeight;
            }

            return stage;
        }

        // 바람 구멍 시험 자리 (2026-11-14): 오른쪽 벽 쪽 바닥에 환풍구(2칸), 그 왼쪽에 버섯 선반과 같은 높이의 선반 + 동전.
        // 바람을 타고 오르다 왼쪽으로 붙으면 선반에 내려앉는다. 바닥이 평평하지 않으면 두지 않는다.
        public const int WindVentX = ChunkData.Width - 7;
        public const int WindLedgeWidth = 4;
        public const int WindTopRise = 3;

        private static void AddWindVent(ChunkData chunk)
        {
            int g = chunk.SurfaceAt(WindVentX);
            for (int x = WindVentX - WindLedgeWidth; x < WindVentX + RoomShaper.WindVentWidth; x++)
                if (chunk.SurfaceAt(x) != g) return;
            int stand = g + ChunkGenerator.BounceLedgeRise;
            if (stand + WindTopRise >= ChunkData.Height - 1) return;
            chunk.SetRow(WindVentX - WindLedgeWidth, WindVentX - 1, stand - 1, StageTile.Platform);
            chunk.AddFeature(FeatureKind.WindVent, WindVentX, g, RoomShaper.WindVentWidth, stand + WindTopRise - g);
            chunk.AddFeature(FeatureKind.CoinPile, WindVentX - 2, stand);
        }

        // 버섯 탑 시험 자리 (2026-11-14): 세로 방 "버섯 탑"(RoomShaper)과 같은 열·높이로 왼쪽에 바닥 버섯 → P1(바닥 + 7) → P1 위 버섯 → P2(무너지는 발판)를 놓고,
        // P2 벽 쪽 끝에 동전, 반대쪽(19~22열) P1 높이에 저격 선반(궁수는 F8로 불러 세운다). 위 문이 없으니 P2가 꼭대기다.
        // 길 버섯(Direction = 1)이라 검증기도 오르는 길로 본다. 바닥이 평평하지 않으면 두지 않는다.
        private static void AddMushroomTower(ChunkData chunk)
        {
            int g = chunk.SurfaceAt(RoomShaper.TowerShelf1X0);
            for (int x = RoomShaper.TowerShelf1X0; x <= RoomShaper.TowerSniperX1; x++)
                if (chunk.SurfaceAt(x) != g) return;
            int stand1 = g + RoomShaper.TowerStep;
            int stand2 = stand1 + RoomShaper.TowerStep;
            if (stand2 + 2 > ChunkData.Height - 2) return;
            chunk.SetRow(RoomShaper.TowerShelf1X0, RoomShaper.TowerShelf1X1, stand1 - 1, StageTile.Platform);
            chunk.AddFeature(FeatureKind.Crumble, RoomShaper.TowerShelf2X0, stand2 - 1, RoomShaper.TowerShelf2X1 - RoomShaper.TowerShelf2X0 + 1);
            chunk.AddFeature(FeatureKind.BounceMushroom, RoomShaper.TowerMushroom1X, g, 1, 1);
            chunk.AddFeature(FeatureKind.BounceMushroom, RoomShaper.TowerMushroom2X, stand1, 1, 1);
            chunk.AddFeature(FeatureKind.CoinPile, RoomShaper.TowerShelf2X0, stand2);
            chunk.SetRow(RoomShaper.TowerSniperX0, RoomShaper.TowerSniperX1, stand1 - 1, StageTile.Platform);
        }

        // 봉인문을 협동 봉인문으로: 첫 속성은 A 방어막, 둘째는 B 마법 (RoomShaper 보물 곁방 문과 같은 규칙).
        private static void MakeCoopGate(ChunkData chunk)
        {
            for (int i = 0; i < chunk.Features.Count; i++)
            {
                var f = chunk.Features[i];
                if (f.Kind != FeatureKind.ElementGate) continue;
                f.ShieldFirst = true;
                f.HasSecondElement = true;
                f.SecondElementIndex = (f.ElementIndex + 1) % ChunkGenerator.GateElementCount;
                chunk.Features[i] = f;
            }
        }
    }
}
