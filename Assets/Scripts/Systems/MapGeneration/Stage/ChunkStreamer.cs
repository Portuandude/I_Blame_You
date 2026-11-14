using System;
using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마인크래프트처럼 플레이어 주변 청크만 씬에 만들어 두고, 멀어진 청크는 치운다.
    // 청크 데이터(StageData)는 시드로 미리 다 만들어져 있어서, 다시 다가가면 같은 지형이 그대로 다시 생긴다.
    // 방 격자 모드(InitializeRooms, Docs/LevelDesign.md): 청크 하나 = 방 하나. 지금 방만 만들어 두고, 문(방 가장자리·천장/바닥 구멍)을
    // 지나면 옆 방으로 옮긴다 (던그리드식). 방들은 월드에서 서로 떨어진 자리에 놓이고, 카메라는 지금 방 안에서만 움직인다.
    // 청크를 치워도 남는 기록: 쓰러뜨린 적, 돌파한 전투 구역, 주운 구역 보상과 유물 상자, 연 봉인문, 보스 돌파 여부.
    public class ChunkStreamer : MonoBehaviour, IEnemyGroup
    {
        public const int LoadRadius = 2;   // 플레이어가 있는 청크 좌우로 몇 청크까지 만들어 둘지
        public const int UnloadRadius = 3; // 이보다 멀어지면 치운다 (경계에서 만들고 치우기를 반복하지 않게 한 칸 여유)
        private const float SweepInterval = 0.5f;
        private const float MidBossHealRatio = 0.5f; // 중간보스 처치 보상 회복 (최대 체력의 50%)
        private const float RelicChestOffset = 3f; // 유물 상자는 회복 구슬에서 왼쪽으로 이만큼
        public const float SpawnLift = 1.2f; // 서 있는 칸 바닥에서 이만큼 띄워 만들고 떨어뜨린다 (몸이 땅에 박히지 않게)
        public const float RoomGap = 8f;     // 방 격자 모드: 월드에서 방 사이 빈 간격 (이웃 방 지형이 겹치지 않게)
        private const float DoorEdge = 0.5f; // 방 가장자리에서 이만큼 안쪽을 넘으면 문을 지난 것으로 본다
        private const float DoorCooldown = 0.35f;

        private struct TrackedEnemy
        {
            public HealthSystem Health;
            public int Chunk;
        }

        private readonly Dictionary<int, Transform> loaded = new Dictionary<int, Transform>();
        private readonly Dictionary<int, TrackedEnemy> alive = new Dictionary<int, TrackedEnemy>();
        private readonly List<HealthSystem> extras = new List<HealthSystem>(); // 분열 등으로 생긴 적
        private readonly HashSet<int> killed = new HashSet<int>();
        private readonly HashSet<int> clearedArenas = new HashSet<int>();
        private readonly HashSet<int> collectedRewards = new HashSet<int>(); // 전투 구역 보상을 주운 청크
        private readonly HashSet<int> collectedCoins = new HashSet<int>(); // 주운 동전 무더기 (CoinPileKey: 청크 번호 × 16 + 청크 안 순번)
        private readonly HashSet<int> takenRelics = new HashSet<int>(); // 정예 구역 유물 상자에서 유물을 고른 청크
        private readonly Dictionary<int, IBlameYou.Player.EquipmentItem> groundEquipment = new Dictionary<int, IBlameYou.Player.EquipmentItem>(); // 유물 상자 옆에 남은 장비 (청크 → 장비)
        private readonly HashSet<int> openedGates = new HashSet<int>(); // 봉인문을 연 청크
        private readonly Dictionary<int, int> gateSteps = new Dictionary<int, int>(); // 두 속성 봉인문: 이미 맞힌 속성 수
        private readonly Dictionary<int, StageExit> exits = new Dictionary<int, StageExit>();

        private StageData stage;
        private Sprite groundTile;
        private Func<StageEnemy, Vector3, Transform, HealthSystem> spawnEnemy;
        private Transform chunkRoot;
        private Transform player;
        private bool bossCleared;
        private float nextSweep;

        // 방 격자 모드
        private RoomLayoutData rooms;
        private List<RoomGeometry> geometries;
        private int currentRoom;
        private float doorReadyTime;
        private bool reapplyCameraBounds; // 떠난 방의 잠긴 전투 구역이 프레임 끝에 사라지며 옛 카메라 영역을 되돌려 놓는 경우 대비
        private readonly HashSet<int> visitedRooms = new HashSet<int>();

        public RoomLayoutData Rooms => rooms;
        public int CurrentRoom => currentRoom;
        public bool IsRoomMode => rooms != null;
        public bool HasVisited(int index) => visitedRooms.Contains(index);
        public int VisitedCount => visitedRooms.Count; // 일시정지 창 "방 N/M 탐험"

        // 이 스테이지에 마을 방이 있으면 가 봤는지, 없으면(기능 테스트 빈 방 등) null.
        public bool? VillageFound
        {
            get
            {
                if (rooms == null) return null;
                foreach (var room in rooms.Rooms)
                    if (room.Kind == RoomKind.Village) return visitedRooms.Contains(room.Index);
                return null;
            }
        }
        // 미니맵 자물쇠: 아직 안 연 협동 봉인문이 있는 곁방 (RoomShaper.HasPairGate)
        public bool HasClosedPairGate(int index) =>
            rooms != null && index >= 0 && index < rooms.Rooms.Count && RoomShaper.HasPairGate(rooms.Rooms[index]) && !openedGates.Contains(index);
        // 미니맵 돌파 체크: 그 방의 전투 구역을 돌파했는지 (방 격자 모드에서 청크 번호 = 방 번호)
        public bool HasClearedArena(int index) => rooms != null && clearedArenas.Contains(index);
        public event Action<int> RoomEntered;

        // 미니맵 룬 점: 그 방에 선 마법 스위치 룬들의 속성 (청크 Features 순서). 없으면 빈 목록. 처음 물을 때 방마다 한 번 모은다.
        private List<Element>[] switchElements;
        private static readonly List<Element> NoSwitches = new List<Element>();
        public IReadOnlyList<Element> SwitchElements(int index)
        {
            if (rooms == null || stage == null || index < 0 || index >= stage.Chunks.Count) return NoSwitches;
            if (switchElements == null) switchElements = new List<Element>[stage.Chunks.Count];
            if (switchElements[index] == null)
            {
                var list = new List<Element>();
                foreach (var f in stage.Chunks[index].Features)
                    if (f.Kind == FeatureKind.PlatformSwitch) list.Add((Element)f.ElementIndex);
                switchElements[index] = list;
            }
            return switchElements[index];
        }

        // 미니맵 동전 점: 그 방에 아직 안 주운 동전 무더기가 남았는지 (방 격자 모드, 청크 번호 = 방 번호).
        // 무더기 수는 청크 Features에서 처음 물을 때 방마다 한 번 센다. 주운 기록은 CoinPileKey(방, 순번) — 놓는 순서와 같은 순번
        private int[] coinPileCounts;
        public bool HasCoinsLeft(int index)
        {
            if (rooms == null || stage == null || index < 0 || index >= stage.Chunks.Count) return false;
            EnsureCoinPileCounts();
            for (int k = 0; k < coinPileCounts[index]; k++)
                if (!collectedCoins.Contains(CoinPileKey(index, k))) return true;
            return false;
        }

        private void EnsureCoinPileCounts()
        {
            if (coinPileCounts != null) return;
            coinPileCounts = new int[stage.Chunks.Count];
            for (int i = 0; i < coinPileCounts.Length; i++)
                foreach (var f in stage.Chunks[i].Features)
                    if (f.Kind == FeatureKind.CoinPile) coinPileCounts[i]++;
        }

        // 결과 창 "동전 무더기 a/b": 이번 스테이지 전체 무더기 수와 주운 수. 스테이지가 없으면 0.
        public int CoinPilesTotal
        {
            get
            {
                if (stage == null) return 0;
                EnsureCoinPileCounts();
                int total = 0;
                foreach (int n in coinPileCounts) total += n;
                return total;
            }
        }

        public int CoinPilesCollected => Mathf.Min(collectedCoins.Count, CoinPilesTotal);

        public StageData Stage => stage;

        public Rect WorldBounds
        {
            get
            {
                if (rooms == null) return new Rect(0f, -1f, stage.WidthTiles, ChunkData.Height + 1f);
                var min = Origin(0);
                var max = min;
                for (int i = 0; i < rooms.Rooms.Count; i++)
                {
                    var o = Origin(i);
                    min = Vector2.Min(min, o);
                    max = Vector2.Max(max, o + new Vector2(rooms.Rooms[i].Cols, 0f)); // 넓은 방은 오른쪽 끝까지
                }

                return new Rect(min.x, min.y - 1f, max.x - min.x, max.y - min.y + ChunkData.Height + 1f);
            }
        }

        // 카메라가 머물 영역: 외길 모드는 스테이지 전체, 방 격자 모드는 지금 방.
        public Rect CameraBounds => rooms == null ? WorldBounds : RoomRect(currentRoom);

        public Rect RoomRect(int index)
        {
            var o = Origin(index);
            return new Rect(o.x, o.y - 1f, RoomCols(index), ChunkData.Height + 1f);
        }

        // 청크(방)의 월드 원점 (왼쪽 아래). 외길 모드는 가로로 이어 붙이고, 방 격자 모드는 격자 칸마다 간격을 두고 놓는다.
        public Vector2 Origin(int index)
        {
            if (rooms == null) return new Vector2(index * ChunkData.Width, 0f);
            var room = rooms.Rooms[index];
            return new Vector2(room.X * (RoomLayout.CellCols + RoomGap), room.Y * (ChunkData.Height + RoomGap)); // 칸 폭 = 가장 넓은 방 (넓은 방도 겹치지 않게)
        }

        // 방(청크)의 가로 칸 수: 넓은 방은 구간 여러 개 (RoomData.Leads). 외길 모드는 늘 한 화면.
        public int RoomCols(int index) => rooms == null ? ChunkData.Width : rooms.Rooms[index].Cols;

        // 낭떠러지로 떨어졌다고 보는 높이 (지금 방 바닥 기준).
        public float KillLine => (rooms == null ? 0f : Origin(currentRoom).y) + FallRecovery.KillY;

        // 시작 청크의 PlayerStart 칸 (서 있는 칸의 바닥 가운데).
        public Vector3 PlayerStart
        {
            get
            {
                var first = stage.Chunks[0];
                foreach (var feature in first.Features)
                {
                    if (feature.Kind == FeatureKind.PlayerStart) return new Vector3(feature.X + 0.5f, feature.Y + SpawnLift, 0f);
                }

                return new Vector3(4.5f, first.EntryHeight + SpawnLift, 0f);
            }
        }

        // spawnEnemy: (종류, 위치, 부모) → 만든 적의 HealthSystem. 적 종류별 생성은 Core가 맡는다.
        public void Initialize(StageData data, Sprite ground, Func<StageEnemy, Vector3, Transform, HealthSystem> spawner)
        {
            stage = data;
            groundTile = ground;
            spawnEnemy = spawner;
            chunkRoot = new GameObject("Chunks").transform;
            chunkRoot.SetParent(transform, false);
        }

        // 플레이어를 정하고 주변 청크를 바로 만든다 (첫 프레임에 바닥이 없어 떨어지지 않게).
        public void SetPlayer(Transform target)
        {
            player = target;
            if (rooms != null) EnterRoom(currentRoom);
            Refresh();
        }

        // 방 격자 모드로 준비한다. geometries[i]는 rooms.Rooms[i]의 지형 (RoomShaper.Build).
        public void InitializeRooms(RoomLayoutData layout, List<RoomGeometry> roomGeometries, Sprite ground,
            Func<StageEnemy, Vector3, Transform, HealthSystem> spawner)
        {
            rooms = layout;
            geometries = roomGeometries;
            var data = new StageData { Seed = layout.Seed };
            foreach (var geometry in roomGeometries) data.Chunks.Add(geometry.Chunk);
            Initialize(data, ground, spawner);
        }

        // 이어하기: 저장한 방에서 시작한다 (SetPlayer 전에 부를 것). 체크포인트 제거(2026-11-10) 뒤로는 저장 시점이 없어 쓰이지 않는다.
        public void SetCurrentRoom(int index)
        {
            if (rooms != null && index >= 0 && index < rooms.Rooms.Count) currentRoom = index;
        }

        public void RegisterEnemy(HealthSystem enemy)
        {
            if (enemy != null && !enemy.IsDead) extras.Add(enemy);
        }

        private void Update()
        {
            if (player == null || stage == null) return;

            Refresh();
            if (rooms != null)
            {
                if (reapplyCameraBounds)
                {
                    // 방을 옮긴 다음 프레임에 한 번 더: 그 사이 치워진 전투 구역(ArenaZone.OnDestroy)이 카메라 영역을 바꿨을 수 있다
                    reapplyCameraBounds = false;
                    var follow = Camera.main != null ? Camera.main.GetComponent<IBlameYou.Core.CameraFollow>() : null;
                    if (follow != null) follow.SetBounds(RoomRect(currentRoom));
                }

                CheckDoors();
                StageEvents.Depth = RoomDepth(currentRoom);
            }
            else
            {
                StageEvents.Depth = Mathf.Clamp01(player.position.x / stage.WidthTiles); // 외길 모드: 가로 위치 비율 (화면에는 안 보임)
            }

            if (Time.time >= nextSweep)
            {
                nextSweep = Time.time + SweepInterval;
                SweepEnemies();
            }
        }

        // 외길 모드 전용 (청크 폭이 늘 32). 방 격자 모드는 RoomRect/RoomCols를 쓴다.
        private int ChunkAt(float x) => Mathf.Clamp(Mathf.FloorToInt(x / ChunkData.Width), 0, stage.Chunks.Count - 1);

        private void Refresh()
        {
            if (rooms != null)
            {
                if (!loaded.ContainsKey(currentRoom)) Load(currentRoom);
                List<int> others = null;
                foreach (int index in loaded.Keys) if (index != currentRoom) (others ?? (others = new List<int>())).Add(index);
                if (others != null) foreach (int index in others) Unload(index);
                return;
            }

            int center = ChunkAt(player.position.x);
            for (int i = center - LoadRadius; i <= center + LoadRadius; i++)
            {
                if (i >= 0 && i < stage.Chunks.Count && !loaded.ContainsKey(i)) Load(i);
            }

            List<int> far = null;
            foreach (int index in loaded.Keys)
            {
                if (Mathf.Abs(index - center) > UnloadRadius) (far ?? (far = new List<int>())).Add(index);
            }

            if (far != null) foreach (int index in far) Unload(index);
        }

        private void Load(int index)
        {
            var chunk = stage.Chunks[index];
            var origin = Origin(index);
            var root = ChunkBuilder.Build(chunk, chunkRoot, groundTile, origin);
            loaded[index] = root;
            float ox = origin.x;
            float oy = origin.y;

            var arenaSpawns = new List<ArenaZone.Spawn>();
            foreach (var f in chunk.Features)
            {
                if (f.Kind == FeatureKind.Enemy && f.Deferred)
                    arenaSpawns.Add(new ArenaZone.Spawn { Kind = f.Enemy, Position = EnemyPosition(origin, f) });
            }

            int platformOrdinal = 0;
            int coinOrdinal = 0;
            var switchPlatforms = new List<MovingPlatform>();
            var switchFeatures = new List<ChunkFeature>();
            foreach (var f in chunk.Features)
            {
                switch (f.Kind)
                {
                    case FeatureKind.Spikes:
                        SpikeTrap.Create(root, new Vector2(ox + f.X, oy + f.Y), f.Width);
                        break;
                    case FeatureKind.FlameJet:
                        FlameJetTrap.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), f.X * 0.37f + index * 0.61f);
                        break;
                    case FeatureKind.ArrowTrap:
                        ArrowTrap.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y + 0.5f), f.Direction);
                        break;
                    case FeatureKind.Stalactite:
                        Stalactite.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y + 0.5f));
                        break;
                    case FeatureKind.MovingPlatform:
                        // 같은 청크의 발판 두 개(갈아타기)는 반 박자 엇갈려 움직인다. 스위치 발판은 멈춘 채로 만들고 아래에서 룬에 잇는다.
                        var moving = MovingPlatform.Create(root, new Vector2(ox + f.X, oy + f.Y + 1f), f.Width, f.Direction,
                            index * 0.37f + platformOrdinal++ * 0.5f, f.SwitchDriven);
                        if (f.SwitchDriven) switchPlatforms.Add(moving);
                        break;
                    case FeatureKind.PlatformSwitch:
                        switchFeatures.Add(f);
                        break;
                    case FeatureKind.Crumble:
                        CrumblingPlatform.Create(root, new Vector2(ox + f.X, oy + f.Y + 1f), f.Width);
                        break;
                    case FeatureKind.BlinkPlatform:
                        BlinkPlatform.Create(root, new Vector2(ox + f.X, oy + f.Y + 1f), f.Width, f.Direction);
                        break;
                    case FeatureKind.BlinkWard:
                        // 낭떠러지 열 전체 (낙사선 아래부터 천장 위까지): 순간이동·대시로 건너뛰지 못하게
                        BlinkWard.Create(root, new Vector2(ox + f.X, oy + f.Y - 6f), f.Width, ChunkData.Height + 8f);
                        break;
                    case FeatureKind.Reward:
                        // 순간이동 지름길 선반의 회복 구슬. 전투 구역 보상과 같은 기록(방 번호)을 쓴다: 세로 방에는 전투 구역이 없다
                        SpawnReward(root, index, new Vector2(ox + f.X + 0.5f, oy + f.Y), false);
                        break;
                    case FeatureKind.CoinPile:
                        SpawnCoinPile(root, CoinPileKey(index, coinOrdinal++), new Vector2(ox + f.X + 0.5f, oy + f.Y), chunk.Difficulty);
                        break;
                    case FeatureKind.BounceMushroom:
                        BounceMushroom.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), f.Direction == 1); // 버섯 탑 길 버섯이면 꺾쇠 표시
                        break;
                    case FeatureKind.WindVent:
                        WindVent.Create(root, new Vector2(ox + f.X, oy + f.Y), f.Width, f.Direction);
                        break;
                    case FeatureKind.Exit:
                        exits[index] = StageExit.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), bossCleared);
                        break;
                    case FeatureKind.ElementGate:
                        int gateChunk = index;
                        var gateElements = f.HasSecondElement
                            ? new[] { (Element)f.ElementIndex, (Element)f.SecondElementIndex }
                            : new[] { (Element)f.ElementIndex };
                        IBlameYou.Puzzles.ElementGate.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), chunk.GateHeight(f.X, f.Y), gateElements,
                            openedGates.Contains(index), () =>
                            {
                                openedGates.Add(gateChunk);
                                gateSteps.Remove(gateChunk);
                                IBlameYou.Core.RunStats.AddGateOpened();
                            },
                            gateSteps.TryGetValue(index, out int gateStep) ? gateStep : 0,
                            step => gateSteps[gateChunk] = step, f.ShieldFirst);
                        break;
                    case FeatureKind.Arena:
                        int arenaChunk = index;
                        bool boss = chunk.Kind == ChunkKind.Boss;
                        bool elite = chunk.Kind == ChunkKind.EliteArena;
                        bool midBoss = RoomShaper.IsMidBossArena(chunk); // 보상: 체력 50% 즉시 회복 + 유물 상자 확정 (회복 구슬 없음, 2026-11-10 사람 결정)
                        var arenaRoot = root;
                        var rewardPosition = new Vector2(ox + f.X + f.Width / 2f, oy + f.Y);
                        var relicPosition = rewardPosition + Vector2.left * RelicChestOffset; // 구역 가운데 발판(7~11, 20~24칸) 사이 바닥
                        // 매복 (2026-11-14 사람 결정): 방 격자 모드의 일반 전투 방 절반은 적이 천장 쪽에서 떨어진다 (Chunks/AmbushRule, 난수 안 씀)
                        bool ambush = rooms != null && AmbushRule.IsAmbush(stage.Seed, index, RoomDepth(index), chunk.Kind == ChunkKind.Arena && !midBoss);
                        var arenaZone = ArenaZone.Create(root, ox + f.X, ox + f.X + f.Width, oy + f.Y, arenaSpawns, clearedArenas.Contains(index), spawnEnemy,
                            () =>
                            {
                                bool medalHealed = OnArenaCleared(arenaChunk, boss, midBoss && arenaRoot != null); // 중간보스 알림을 띄울 때만 훈장 알림을 합친다
                                if (boss || arenaRoot == null) return;
                                if (midBoss)
                                {
                                    // 용사의 훈장 회복과 같은 순간이면 알림 한 줄로 (따로 띄우면 뒤 알림이 앞을 덮는다)
                                    HealPlayer(MidBossHealRatio, medalHealed
                                        ? $"중간보스 처치! 체력 +50% · 훈장 +{MedalPercent}%, 유물 상자 등장"
                                        : "중간보스 처치! 체력 50% 회복, 유물 상자가 나타났다");
                                    SpawnRelicChest(arenaRoot, arenaChunk, rewardPosition, true);
                                    return;
                                }
                                SpawnReward(arenaRoot, arenaChunk, rewardPosition, true);
                                if (elite) SpawnRelicChest(arenaRoot, arenaChunk, relicPosition, true);
                            });
                        arenaZone.Ambush = ambush;
                        // 돌파했지만 보상을 줍지 않고 떠났던 구역이면 다시 왔을 때 보상을 그대로 둔다.
                        if (midBoss && clearedArenas.Contains(index))
                            SpawnRelicChest(root, index, rewardPosition, false); // 회복은 처치 순간에 이미 받았다
                        else if (!boss && clearedArenas.Contains(index))
                        {
                            SpawnReward(root, index, rewardPosition, false);
                            if (elite) SpawnRelicChest(root, index, relicPosition, false);
                        }
                        break;
                    case FeatureKind.Enemy:
                        if (!f.Deferred) SpawnTracked(index, origin, f);
                        break;
                }
            }

            // 마법 스위치 룬: k번째 룬이 k번째 스위치 발판을 움직인다 (발판을 먼저 다 만든 뒤에 잇는다).
            for (int k = 0; k < switchFeatures.Count; k++)
            {
                var f = switchFeatures[k];
                var linked = k < switchPlatforms.Count ? new[] { switchPlatforms[k] } : new MovingPlatform[0];
                IBlameYou.Puzzles.PlatformSwitch.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), (Element)f.ElementIndex, linked);
            }

            if (rooms != null) LoadRoomExtras(index, root, origin);
        }

        // 방 격자 모드의 곁방: 보물 방 = 유물 상자, 샘 방 = 회복 구슬 (가져가면 기록돼 다시 오면 없다), 마을 = 식당·장비 상점 건물.
        private void LoadRoomExtras(int index, Transform root, Vector2 origin)
        {
            var room = rooms.Rooms[index];
            RoomDoorMarkers.Create(root, origin, room, geometries[index], rooms, HasVisited);
            // 좌우 문 곁방은 평평한 휴식 지형 가운데, 세로 곁방은 바닥 구멍을 피해 바닥 칸 옆
            var floorSpot = geometries[index].Floor;
            var floor = floorSpot.HasValue
                ? new Vector2(origin.x + floorSpot.Value.x + 2.5f, origin.y + floorSpot.Value.y)
                : new Vector2(origin.x + RoomCols(index) / 2f, origin.y + stage.Chunks[index].EntryHeight);
            // 협동 봉인문이 선 세로 곁방은 상자를 봉인문(8열) 왼쪽으로 (RoomShaper.HasPairGate)
            if (room.Kind == RoomKind.Treasure && room.IsVertical && RoomShaper.HasPairGate(room))
                floor = new Vector2(origin.x + RoomShaper.ShaftPairChestX + 0.5f, origin.y + RoomShaper.ShaftGround);
            if (room.Kind == RoomKind.Treasure) SpawnRelicChest(root, index, floor, false);
            else if (room.Kind == RoomKind.Spring) SpawnReward(root, index, floor, false);
            else if (room.Kind == RoomKind.Village)
            {
                // 마을: 평평한 휴식 지형 위에 왼쪽 식당, 오른쪽 장비 상점 (RoomLayout이 좌우 한 화면 방으로 고른다)
                float ground = origin.y + stage.Chunks[index].EntryHeight;
                int cols = RoomCols(index);
                VillageBuilding.Create(root, new Vector2(origin.x + cols * VillageRestaurantAt, ground), VillageService.Restaurant);
                VillageBuilding.Create(root, new Vector2(origin.x + cols * VillageShopAt, ground), VillageService.Shop);
                VillageDecor.Build(root, origin.x, cols, ground, VillageRestaurantAt); // 따뜻한 색조·울타리·가로등·꽃·분수·김·반딧불
                // 식당 주인·상인은 각 건물 문 오른쪽 벽 앞 (문 앞 W 범위 ±1.2 안이라 말을 걸며 창을 연다)
                VillageNpc.Create(root, new Vector2(origin.x + cols * VillageRestaurantAt + VillageNpcOffset, ground), VillageService.Restaurant);
                VillageNpc.Create(root, new Vector2(origin.x + cols * VillageShopAt + VillageNpcOffset, ground), VillageService.Shop);
            }
        }

        private const float VillageRestaurantAt = 0.3f; // 방 가로 비율 (문 앞을 비워 두려고 양 끝에서 떨어뜨림)
        private const float VillageShopAt = 0.7f;
        private const string VillageMusic = "Village"; // Resources/Audio/Music/Village (Tools/AudioGen village_theme)
        private const float VillageNpcOffset = 1.3f; // 건물 가운데에서 NPC까지 (문 폭 ±0.6 밖, 건물 폭 ±1.75 안)

        private static Vector3 EnemyPosition(Vector2 origin, ChunkFeature f) =>
            new Vector3(origin.x + f.X + 0.5f, origin.y + f.Y + SpawnLift, 0f);

        private void SpawnTracked(int chunkIndex, Vector2 origin, ChunkFeature f)
        {
            if (killed.Contains(f.Id) || alive.ContainsKey(f.Id)) return;

            var health = spawnEnemy(f.Enemy, EnemyPosition(origin, f), transform);
            if (health == null) return;

            int id = f.Id;
            alive[id] = new TrackedEnemy { Health = health, Chunk = chunkIndex };
            health.HealthChanged += (current, max) =>
            {
                if (current > 0f) return;
                killed.Add(id);
                alive.Remove(id);
            };
        }

        // 용사의 훈장으로 회복했으면 true. 보스·중간보스 구역은 그쪽 알림에 훈장 회복을 합쳐 한 줄로 띄운다.
        private bool OnArenaCleared(int chunkIndex, bool boss, bool midBoss)
        {
            clearedArenas.Add(chunkIndex);
            IBlameYou.Core.RunStats.AddArenaCleared();
            var relics = player != null ? IBlameYou.Player.PlayerRelics.For(player.gameObject) : null;
            bool medalHealed = relics != null && relics.Has(IBlameYou.Player.RelicId.HeroMedal) // 용사의 훈장: 돌파 보상 회복 + 머리 위 금 메달
                && HealPlayer(IBlameYou.Player.RelicBook.HeroMedalHealRatio, boss || midBoss ? null : $"용사의 훈장: 체력 +{MedalPercent}%");
            if (medalHealed) VfxPlayer.MedalGlint(player, MedalGlintOffset);
            if (!boss) return medalHealed;

            bossCleared = true;
            foreach (var exit in exits.Values) if (exit != null) exit.SetOpen(true);
            StageEvents.PostNotice(medalHealed ? $"출구가 열렸다! · 용사의 훈장 +{MedalPercent}%" : "출구가 열렸다!");
            return medalHealed;
        }

        private static string MedalPercent => $"{IBlameYou.Player.RelicBook.HeroMedalHealRatio * 100f:0}";

        // 일반 전투 구역 보상 (보스 구역은 출구가 보상이라 없음). 이미 주웠으면 만들지 않는다.
        private void SpawnReward(Transform root, int chunkIndex, Vector2 position, bool dropped)
        {
            if (collectedRewards.Contains(chunkIndex)) return;
            ArenaReward.Create(root, position, dropped, () => collectedRewards.Add(chunkIndex));
        }

        // 동전 무더기: 청크마다 여럿일 수 있어서(넓은 방은 구간 여러 개) 청크 번호와 청크 안 순번으로 기록한다
        private static int CoinPileKey(int chunkIndex, int ordinal) => chunkIndex * 16 + ordinal;

        private void SpawnCoinPile(Transform root, int key, Vector2 position, float difficulty)
        {
            if (collectedCoins.Contains(key)) return;
            CoinPile.Create(root, position, GoldDropTable.CoinPileGold(difficulty), () => collectedCoins.Add(key));
        }

        // 즉시 회복 (최대 체력 비율, 중간보스 처치·용사의 훈장). 쓰러진 뒤면 아무 일도 하지 않고 false. notice가 null이면 알림 없이.
        private bool HealPlayer(float ratio, string notice)
        {
            var health = player != null ? player.GetComponent<HealthSystem>() : null;
            if (health == null || health.IsDead) return false;
            float before = health.Current;
            health.Heal(health.Max * ratio);
            DamageNumbers.ShowHeal(player.position, health.Current - before);
            EffectLibrary.PlayHit(Element.Water, player.position + Vector3.up, 1.5f);
            GameAudio.PlaySfx("Pickup", 0.8f);
            if (notice != null) StageEvents.PostNotice(notice);
            return true;
        }

        private static readonly Vector3 MedalGlintOffset = new Vector3(0f, 1.5f, 0f); // 용사의 훈장 메달 시작 높이 (발밑 기준, 머리 위)

        // 정예 구역 유물 상자. 이미 유물을 골랐으면 만들지 않는다.
        private void SpawnRelicChest(Transform root, int chunkIndex, Vector2 position, bool dropped)
        {
            var equipmentPosition = position + new Vector2(EquipmentOffsetX, 0f);
            if (takenRelics.Contains(chunkIndex))
            {
                if (groundEquipment.TryGetValue(chunkIndex, out var left)) SpawnEquipment(root, chunkIndex, equipmentPosition, left, false);
                return;
            }

            RelicChest.Create(root, position, stage.Seed, chunkIndex, dropped, () =>
            {
                takenRelics.Add(chunkIndex);
                // 유물 상자를 열면 옆에 장비 하나 (Docs/EquipmentDesign.md 7절). 중간보스 상자는 수식어 한 등급 위.
                int bonus = RoomShaper.IsMidBossArena(stage.Chunks[chunkIndex]) ? IBlameYou.Player.EquipmentOffer.MidBossGradeBonus : 0;
                var item = IBlameYou.Player.EquipmentOffer.Roll(stage.Seed, chunkIndex, bonus);
                groundEquipment[chunkIndex] = item;
                if (root != null) SpawnEquipment(root, chunkIndex, equipmentPosition, item, true);
            });
        }

        private const float EquipmentOffsetX = 1.6f; // 유물 상자 오른쪽

        private void SpawnEquipment(Transform root, int chunkIndex, Vector2 position, IBlameYou.Player.EquipmentItem item, bool dropped)
        {
            EquipmentPickup.Create(root, position, item, dropped, left =>
            {
                if (left == null) groundEquipment.Remove(chunkIndex);
                else groundEquipment[chunkIndex] = left;
            });
        }

        // 바닥에 남은 장비(groundEquipment)는 런 저장에 넣지 않는다: 2026-11-10부터 런 이어하기 저장을 쓰지 않음 (RunSave). 스테이지 2 저장을 다시 쓰게 되면 함께 넣을 것.
        // 런 진행 저장용: 지금까지의 기록을 꺼내거나(Export), 저장된 기록으로 되돌린다(Import, 청크를 만들기 전에 부를 것).
        public StageRecords ExportRecords()
        {
            var records = new StageRecords
            {
                killed = ToArray(killed),
                clearedArenas = ToArray(clearedArenas),
                collectedRewards = ToArray(collectedRewards),
                collectedCoins = ToArray(collectedCoins),
                takenRelics = ToArray(takenRelics),
                openedGates = ToArray(openedGates),
                gateStepChunks = new int[gateSteps.Count],
                gateStepValues = new int[gateSteps.Count],
                visitedRooms = ToArray(visitedRooms),
                bossCleared = bossCleared
            };

            int i = 0;
            foreach (var pair in gateSteps)
            {
                records.gateStepChunks[i] = pair.Key;
                records.gateStepValues[i] = pair.Value;
                i++;
            }

            return records;
        }

        public void ImportRecords(StageRecords records)
        {
            if (records == null) return;
            Fill(killed, records.killed);
            Fill(clearedArenas, records.clearedArenas);
            Fill(collectedRewards, records.collectedRewards);
            Fill(collectedCoins, records.collectedCoins);
            Fill(takenRelics, records.takenRelics);
            Fill(openedGates, records.openedGates);
            Fill(visitedRooms, records.visitedRooms);
            gateSteps.Clear();
            if (records.gateStepChunks != null && records.gateStepValues != null)
            {
                int count = Mathf.Min(records.gateStepChunks.Length, records.gateStepValues.Length);
                for (int i = 0; i < count; i++) gateSteps[records.gateStepChunks[i]] = records.gateStepValues[i];
            }

            bossCleared = records.bossCleared;
        }

        private static int[] ToArray(HashSet<int> set)
        {
            var array = new int[set.Count];
            set.CopyTo(array);
            return array;
        }

        private static void Fill(HashSet<int> set, int[] values)
        {
            set.Clear();
            if (values != null) foreach (int value in values) set.Add(value);
        }

        private void Unload(int index)
        {
            if (loaded.TryGetValue(index, out var root) && root != null) Destroy(root.gameObject);
            loaded.Remove(index);
            exits.Remove(index);
        }

        // 떨어진 적이 사라질 때: 화면 아래 가장자리(낙사선보다 위)에서 작은 먼지 펑 + 작은 쓰러짐 소리.
        // 화면 옆으로 벗어난 곳이면 아무것도 하지 않는다 (경험치는 주지 않는다).
        private static void FallPoof(float x, float killLine)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;
            float halfWidth = cam.orthographicSize * cam.aspect;
            if (Mathf.Abs(x - cam.transform.position.x) > halfWidth) return;
            float y = Mathf.Max(killLine + 0.5f, cam.transform.position.y - cam.orthographicSize + 0.4f);
            VfxPlayer.Dust(new Vector3(x, y, 0f), 0.8f);
            if (fallPoofFrame != Time.frameCount) GameAudio.PlaySfx("EnemyDie", 0.35f); // 같은 프레임 여러 마리면 소리는 한 번 (리뷰 LOW)
            fallPoofFrame = Time.frameCount;
        }

        private static int fallPoofFrame = -1;

        // 만들어 둔 범위 밖으로 벗어난 적은 치운다 (다시 다가가면 원래 자리에 다시 나온다).
        // 낭떠러지로 떨어진 적은 쓰러진 것으로 치고 다시 만들지 않는다.
        private void SweepEnemies()
        {
            float minX, maxX;
            if (rooms != null)
            {
                // 방 격자 모드: 지금 방 밖의 적은 치운다 (그 방에 다시 들어가면 다시 나온다)
                var rect = RoomRect(currentRoom);
                minX = rect.xMin - 1f;
                maxX = rect.xMax + 1f;
            }
            else
            {
                int center = ChunkAt(player.position.x);
                minX = (center - UnloadRadius) * ChunkData.Width;
                maxX = (center + UnloadRadius + 1) * ChunkData.Width;
            }

            float killLine = KillLine;

            List<int> removed = null;
            foreach (var pair in alive)
            {
                var health = pair.Value.Health;
                if (health == null) { (removed ?? (removed = new List<int>())).Add(pair.Key); continue; }

                var position = health.transform.position;
                if (position.y < killLine)
                {
                    killed.Add(pair.Key);
                    FallPoof(position.x, killLine);
                    Destroy(health.gameObject);
                    (removed ?? (removed = new List<int>())).Add(pair.Key);
                }
                else if (position.x < minX || position.x > maxX)
                {
                    Destroy(health.gameObject);
                    (removed ?? (removed = new List<int>())).Add(pair.Key);
                }
            }

            if (removed != null) foreach (int id in removed) alive.Remove(id);

            for (int i = extras.Count - 1; i >= 0; i--)
            {
                var health = extras[i];
                if (health == null || health.IsDead) { extras.RemoveAt(i); continue; }

                var position = health.transform.position;
                if (position.y < killLine || position.x < minX || position.x > maxX)
                {
                    if (position.y < killLine) FallPoof(position.x, killLine);
                    Destroy(health.gameObject);
                    extras.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ 방 격자 모드: 문과 방 옮기기

        // 플레이어가 지금 방의 문 자리를 지나 방 밖으로 나가면 옆 방으로 옮긴다.
        // 좌우 문: 방 양 끝을 넘음 · 위 문: 천장 구멍 위로 올라감 · 아래 문: 바닥 구멍 아래로 떨어짐 (낭떠러지 복귀보다 먼저).
        private void CheckDoors()
        {
            if (Time.time < doorReadyTime) return;
            var room = rooms.Rooms[currentRoom];
            var local = (Vector2)player.position - Origin(currentRoom);
            bool inHole = local.x >= RoomShaper.HoleX0 && local.x <= RoomShaper.HoleX1 + 1f;

            DoorDir passed = DoorDir.None;
            if (local.x < DoorEdge && (room.Doors & DoorDir.Left) != 0) passed = DoorDir.Left;
            else if (local.x > RoomCols(currentRoom) - DoorEdge && (room.Doors & DoorDir.Right) != 0) passed = DoorDir.Right;
            else if (local.y > ChunkData.Height - 0.3f && inHole && (room.Doors & DoorDir.Up) != 0) passed = DoorDir.Up;
            else if (local.y < -0.5f && inHole && (room.Doors & DoorDir.Down) != 0) passed = DoorDir.Down;
            if (passed == DoorDir.None) return;

            var next = rooms.Neighbor(room, passed);
            if (next == null) return;
            currentRoom = next.Index;
            DespawnOtherRoomEnemies();
            Refresh();
            PlaceAtArrival(next.Index, RoomLayout.Opposite(passed));
            EnterRoom(next.Index);
            ScreenFlash.RoomFade(); // 옮긴 순간을 어둡게 덮었다가 밝혀 순간이동처럼 튀어 보이지 않게
            GameAudio.PlaySfx("RoomPass", 0.8f, 0.05f); // 어두워지는 순간 가벼운 "휙" (Tools/AudioGen RoomPass, 2026-11-13 방 입장 연출)
        }

        // 방을 옮기면 떠난 방의 적은 바로 치운다 (쓰러뜨린 것으로 치지 않아 그 방에 다시 들어가면 다시 나온다).
        // 적은 방 지형이 아니라 스트리머 아래에 있어서, 그냥 두면 지형이 사라진 뒤 아래 방으로 떨어지거나 낭떠러지 처리로 사라진다.
        private void DespawnOtherRoomEnemies()
        {
            List<int> removed = null;
            foreach (var pair in alive)
            {
                if (pair.Value.Chunk == currentRoom) continue;
                if (pair.Value.Health != null) Destroy(pair.Value.Health.gameObject);
                (removed ?? (removed = new List<int>())).Add(pair.Key);
            }

            if (removed != null) foreach (int id in removed) alive.Remove(id);

            foreach (var extra in extras) if (extra != null) Destroy(extra.gameObject); // 분열 등으로 생긴 적은 떠난 방 것뿐
            extras.Clear();
        }

        // 옆 방의 반대쪽 문 자리에 플레이어를 놓는다. 좌우 문은 다시 바로 넘어가지 않게 한 칸 안쪽에 놓는다.
        private void PlaceAtArrival(int index, DoorDir door)
        {
            if (geometries == null || !geometries[index].Arrivals.TryGetValue(door, out var spot)) return;
            int column = spot.x;
            if (door == DoorDir.Left) column = 1;
            else if (door == DoorDir.Right) column = RoomCols(index) - 2;
            // 한 칸 안쪽 열의 땅이 문턱보다 높으면 그 위에 선다 (땅 속에 박히지 않게)
            var chunk = stage.Chunks[index];
            int standY = spot.y;
            while (standY < ChunkData.Height - 2 && chunk.IsSolid(column, standY)) standY++;
            var point = Origin(index) + new Vector2(column + 0.5f, standY + SpawnLift);

            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = point;
            }

            player.position = new Vector3(point.x, point.y, player.position.z);
            var movement = player.GetComponent<IBlameYou.Player.PlayerMovement>();
            if (movement != null) movement.ResetAirborne();
            var recovery = player.GetComponent<FallRecovery>();
            if (recovery != null) recovery.SetSafePoint(new Vector3(point.x, point.y + 0.1f, 0f));

            // 어디로 나왔는지 바로 보이게 (2026-11-13 방 입장 연출): 몸 둘레에 문 색(aqua) 빛 고리가 퍼지고,
            // 발밑이 땅이면 작은 먼지 (위/아래 구멍으로 나와 공중이면 먼지는 없다)
            ManaRingFx.Create(player, Vector3.zero, ArrivalRingColor);
            if (standY > 0 && chunk.IsSolid(column, standY - 1))
                VfxPlayer.Dust(new Vector3(point.x, Origin(index).y + standY, 0f), 0.5f);
        }

        private static readonly Color ArrivalRingColor = new Color(0.5f, 0.97f, 1f, 0.85f); // RoomDoorMarkers 문 빛과 같은 aqua

        // 방에 들어왔을 때: 가 본 방 기록, 카메라 영역, 낭떠러지 높이.
        private void EnterRoom(int index)
        {
            doorReadyTime = Time.time + DoorCooldown;
            reapplyCameraBounds = true;
            bool firstVisit = visitedRooms.Add(index);
            FallRecovery.FloorY = Origin(index).y;
            var follow = Camera.main != null ? Camera.main.GetComponent<IBlameYou.Core.CameraFollow>() : null;
            if (follow != null)
            {
                follow.SetBounds(RoomRect(index));
                follow.SnapToTarget();
            }

            var entered = rooms.Rooms[index];
            StageEvents.RoomDistance = entered.Distance; // 게임오버 창 "쓰러진 곳: 시작 방에서 N칸"
            StageEvents.FarthestDistance = Math.Max(StageEvents.FarthestDistance, entered.Distance);
            StageEvents.InSideRoom = !entered.MainPath;
            if (entered.Kind == RoomKind.Village)
            {
                StageEvents.VillageVisit++; // 식당 새 방문
                StageEvents.VillageRoom = index; // 장비 상점 진열 시드
                StageEvents.StageSeed = stage.Seed;
            }
            StageEvents.Depth = RoomDepth(index); // 배경 색조가 첫 프레임부터 이 방 깊이를 쓰게 (Update보다 먼저 올 수 있음)
            // 마을에 있는 동안은 잔잔한 마을 곡, 나오면 깊이에 맞는 던전 곡 (다른 곡이 나오던 중이면 건드리지 않는다)
            if (entered.Kind == RoomKind.Village) GameAudio.PlayMusic(VillageMusic);
            else if (GameAudio.CurrentMusic == VillageMusic) GameAudio.PlayMusic(ArenaZone.StageMusicFor(StageEvents.Depth));
            if (entered.Kind == RoomKind.Village && firstVisit)
            {
                // 판에서 처음 찾은 마을: 알림 + 오르골 세 음 (미니맵 집 칸은 처음 들어간 방이라 MinimapUI가 톡 반짝인다)
                int interest = IBlameYou.Core.RunGold.OnVillageFound(player); // 돼지 저금통 이자 (없으면 0)
                StageEvents.PostNotice(interest > 0 ? $"마을 발견! 저금통 이자 +{interest}" : "마을 발견! 식당·장비 상점");
                GameAudio.PlaySfx("VillageFound", 0.8f, 0f);
            }
            else if (firstVisit && (entered.Kind == RoomKind.Treasure || entered.Kind == RoomKind.Spring))
            {
                // 막다른 곁방을 처음 찾았을 때: 무슨 방인지 알림 + 반짝 소리 (마을과 같은 "발견" 리듬, 더 작게)
                StageEvents.PostNotice(entered.Kind == RoomKind.Treasure ? "보물 방 발견!" : "회복 샘 발견!");
                GameAudio.PlaySfx("Pickup", 0.6f, 0f);
            }
            // 보스 방 바로 옆 방 (보스를 아직 안 잡았을 때): 배경음악을 조금 줄이고, 처음 들어왔을 때 낮은 심장 "두-근" (Tools/AudioGen BossNear)
            bool nearBoss = !bossCleared && NextToBoss(entered);
            GameAudio.SetMusicDuck(nearBoss ? BossNearMusicDuck : 1f);
            if (nearBoss && firstVisit)
            {
                GameAudio.PlaySfx("BossNear", 0.9f, 0f);
                ScreenFlash.EdgeWarn(BossNearEdgeColor, 1.4f); // 심장 소리에 맞춰 화면 가장자리가 검붉게 두 번 천천히 (2026-11-13 보스 방 앞 분위기)
                StageEvents.PostNotice("근처에서 강한 기운이 느껴진다...");
            }
            // 판에서 마지막 남은 방을 처음 밟은 순간 (2026-11-13 방 입장 연출): 알림 + 맑은 반짝 소리 두 번. 보상은 없다.
            // 이어하기로 불러온 기록이 이미 다 가 본 상태면 firstVisit이 아니라서 다시 울리지 않는다.
            if (firstVisit && rooms.Rooms.Count > 1 && visitedRooms.Count >= rooms.Rooms.Count) // 방 하나짜리(빈 시험 방)는 시작하자마자 울리지 않게
            {
                StageEvents.PostNotice("모든 방을 찾았다!");
                GameAudio.PlaySfx("StarShard", 0.7f, 0f);
                GameAudio.PlaySfxDelayed("StarShard", 0.14f, 0.6f);
            }
            // 방 이벤트 보물 고블린 (2026-11-14 사람 결정): 가로 이동 방에 처음 들어가면 금화 자루를 멘 고블린이 나타나 도망친다 (Chunks/TreasureGoblinRule, 난수 안 씀)
            if (firstVisit && entered.Kind == RoomKind.Travel && !entered.IsVertical
                && TreasureGoblinRule.IsTreasureGoblinRoom(stage.Seed, index, RoomDepth(index), true))
                SpawnTreasureGoblin(index);
            RoomEntered?.Invoke(index);
        }

        // 플레이어에게서 먼 쪽(방 가로 30% 또는 70%) 근처에서 설 자리를 찾아 보물 고블린을 부른다. 자리가 없으면 부르지 않는다.
        private void SpawnTreasureGoblin(int index)
        {
            if (spawnEnemy == null || player == null) return;
            var chunk = stage.Chunks[index];
            var origin = Origin(index);
            int cols = RoomCols(index);
            float local = player.position.x - origin.x;
            int center = Mathf.RoundToInt(cols * (local < cols / 2f ? 0.7f : 0.3f));
            for (int offset = 0; offset <= 8; offset++)
            {
                foreach (int x in new[] { center + offset, center - offset })
                {
                    if (x < 3 || x > cols - 4) continue;
                    for (int y = 1; y < ChunkData.Height - 3; y++)
                    {
                        if (!chunk.IsSolid(x, y - 1) || chunk.IsSolid(x, y) || chunk.IsSolid(x, y + 1)) continue;
                        var health = spawnEnemy(StageEnemy.TreasureGoblin, new Vector3(origin.x + x + 0.5f, origin.y + y + SpawnLift, 0f), transform);
                        if (health == null) return;
                        RegisterEnemy(health);
                        StageEvents.PostNotice("보물 고블린이다! 10초 안에 잡아라!");
                        GameAudio.PlaySfx("CoinPile", 0.6f, 0f);
                        return;
                    }
                }
            }
        }

        private const float BossNearMusicDuck = 0.55f;
        private static readonly Color BossNearEdgeColor = new Color(0.45f, 0.05f, 0.12f); // 검붉은 (정예 보라보다 어둡고 무겁게)

        // 문으로 이어진 이웃 방 중에 보스 방이 있는가 (보스 방 자신은 아니다)
        private bool NextToBoss(RoomData room)
        {
            if (room.Kind == RoomKind.Boss) return false;
            foreach (var dir in new[] { DoorDir.Left, DoorDir.Right, DoorDir.Up, DoorDir.Down })
            {
                if ((room.Doors & dir) == 0) continue;
                var next = rooms.Neighbor(room, dir);
                if (next != null && next.Kind == RoomKind.Boss) return true;
            }
            return false;
        }

        // 깊이 = 시작 방에서의 방 거리 / 보스 거리 (= 방 난이도, RoomLayout 4×3 격자). 곁가지 방도 자기 거리로 잰다.
        private float RoomDepth(int index) => Mathf.Clamp01(rooms.Rooms[index].Difficulty);

        private void OnDestroy()
        {
            GameAudio.SetMusicDuck(1f); // 보스 방 앞에서 판이 끝나도 다음 화면 음악은 원래 크기로
            StageEvents.Depth = 0f;
            StageEvents.RoomDistance = -1;
            StageEvents.FarthestDistance = -1;
            FallRecovery.FloorY = 0f;
        }
    }
}
