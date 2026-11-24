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
    // 파일 나눔 (2026-11-22 정리): ChunkStreamer.Queries / .Puzzles / .Rewards / .RoomEvents (partial)
    public partial class ChunkStreamer : MonoBehaviour, IEnemyGroup
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
        private readonly HashSet<int> cursedQuick = new HashSet<int>(); // 그중 빠른 해제(CursedChestRule.QuickTime 안)로 유물 3개 중 고르기인 방
        private readonly HashSet<int> cursedLeftAwake = new HashSet<int>(); // 깨웠지만 아직 못 푼 저주 상자 방 (떠났다 오면 "다시 잠들었다")
        private readonly HashSet<int> cursedBroken = new HashSet<int>(); // 저주받은 상자의 정예를 쓰러뜨린 방 (유물을 아직 안 골랐으면 유물 상자가 남는다)
        private readonly Dictionary<int, IBlameYou.Player.EquipmentItem> groundEquipment = new Dictionary<int, IBlameYou.Player.EquipmentItem>(); // 유물 상자 옆에 남은 장비 (청크 → 장비)
        private readonly HashSet<int> openedGates = new HashSet<int>(); // 봉인문을 연 청크
        private readonly HashSet<int> solvedPuzzles = new HashSet<int>(); // 등불 순서 퍼즐을 푼 청크 (방). 런 이어하기 저장은 안 쓰므로 StageRecords에 넣지 않는다
        private readonly Dictionary<int, int> windBoxColumns = new Dictionary<int, int>(); // 바람 상자를 밀어 둔 열 (방을 나갔다 와도 그 자리)
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
                WakeWaitingGoblin();
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
                        // 바람 상자 선반의 구슬은 주우면 그 퍼즐을 푼 것으로 친다 (영혼석 + 결과 창 퍼즐 N/M)
                        int rewardChunk = index;
                        System.Action solvedByOrb = chunk.Kind == ChunkKind.WindBoxPuzzle
                            ? () =>
                            {
                                if (solvedPuzzles.Add(rewardChunk)) IBlameYou.Core.RunStats.AddHeartDust(HeartDustRule.Puzzle);
                            }
                            : (System.Action)null;
                        SpawnReward(root, index, new Vector2(ox + f.X + 0.5f, oy + f.Y), false, solvedByOrb);
                        break;
                    case FeatureKind.WindBox:
                        // 바람 상자 (Puzzles/WindBox): 밀어 둔 자리를 기억해 다시 들어와도 그 자리에. Direction = 선반 왼쪽 열, Width = 선반 폭
                        int boxChunk = index;
                        int boxColumn = windBoxColumns.TryGetValue(index, out int keptColumn) ? keptColumn : f.X;
                        IBlameYou.Puzzles.WindBox.Create(root, chunk, new Vector2(ox, oy), boxColumn, f.Y, f.Direction, f.Width,
                            column => windBoxColumns[boxChunk] = column);
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
                            step => gateSteps[gateChunk] = step);
                        break;
                    case FeatureKind.Arena:
                        int arenaChunk = index;
                        bool boss = chunk.Kind == ChunkKind.Boss;
                        bool elite = chunk.Kind == ChunkKind.EliteArena;
                        bool midBoss = RoomShaper.IsMidBossArena(chunk); // 보상: 체력 50% 즉시 회복 + 유물 상자 확정 (회복 구슬 없음, 2026-11-10 사람 결정)
                        var arenaRoot = root;
                        var arenaRoom = rooms != null ? rooms.Rooms[index] : null;
                        bool shaftArena = arenaRoom != null && arenaRoom.IsVertical; // 세로 전투 방: 보상은 바닥 구멍(14~16열)을 피해 바닥 칸 옆
                        var rewardPosition = shaftArena && geometries[index].Floor.HasValue
                            ? new Vector2(ox + geometries[index].Floor.Value.x + 2.5f, oy + geometries[index].Floor.Value.y)
                            : new Vector2(ox + f.X + f.Width / 2f, oy + f.Y);
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
                        if (shaftArena) arenaZone.SealHoles(ShaftHoles(arenaRoom, ox, oy));
                        arenaZone.Ambush = ambush;
                        arenaZone.WallArchers = ambush && RoomShaper.IsWallArcherAmbush(stage.Seed, rooms.Rooms[index]);
                        arenaZone.Burrow = ambush && !arenaZone.WallArchers && AmbushRule.IsBurrow(stage.Seed, index);
                        arenaZone.Mixed = arenaZone.Burrow && AmbushRule.IsMixed(stage.Seed, index, RoomDepth(index)); // 깊은 방: 땅속·천장 번갈아
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

            LoadLanternPuzzle(index, root, chunk, ox, oy);
            if (rooms != null) LoadRoomExtras(index, root, origin);
        }

        // 세로 전투 방의 천장 구멍(위 문)·바닥 구멍(아래 문) 자리 (Chunks/RoomShaper.HoleX0~HoleX1)
        private static List<Rect> ShaftHoles(RoomData room, float ox, float oy)
        {
            var list = new List<Rect>();
            float x = ox + RoomShaper.HoleX0;
            float w = RoomShaper.HoleX1 - RoomShaper.HoleX0 + 1;
            if ((room.Doors & DoorDir.Up) != 0) list.Add(new Rect(x, oy + ChunkData.Height - 1, w, 1f));
            if ((room.Doors & DoorDir.Down) != 0) list.Add(new Rect(x, oy + RoomShaper.ShaftGround - 1, w, 1f));
            return list;
        }

        // 방 격자 모드의 보상 방: 보물 방 = 유물 상자, 샘 방 = 회복 구슬 (가져가면 기록돼 다시 오면 없다), 마을 = 식당·장비 상점 건물.
        private void LoadRoomExtras(int index, Transform root, Vector2 origin)
        {
            var room = rooms.Rooms[index];
            RoomDoorMarkers.Create(root, origin, room, geometries[index], rooms, HasVisited);
            // 좌우 방은 평평한 휴식 지형 가운데, 세로 방은 바닥 구멍을 피해 바닥 칸 옆
            var floorSpot = geometries[index].Floor;
            var floor = floorSpot.HasValue
                ? new Vector2(origin.x + floorSpot.Value.x + 2.5f, origin.y + floorSpot.Value.y)
                : new Vector2(origin.x + RoomCols(index) / 2f, origin.y + stage.Chunks[index].EntryHeight);
            // 두 속성 봉인문이 선 세로 보물 방은 상자를 봉인문(8열) 왼쪽으로 (RoomShaper.HasPairGate)
            if (room.Kind == RoomKind.Treasure && room.IsVertical && RoomShaper.HasPairGate(room))
                floor = new Vector2(origin.x + RoomShaper.ShaftPairChestX + 0.5f, origin.y + RoomShaper.ShaftGround);
            if (IsCursedChestRoom(index)) PlaceCursedChest(root, index);
            if (goblinEquipmentSpots.TryGetValue(index, out var goblinSpot) && groundEquipment.TryGetValue(index, out var goblinLeft))
                SpawnEquipment(root, index, goblinSpot, goblinLeft, false); // 보물 고블린이 떨군 장비를 두고 갔다가 다시 온 경우
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

        // 용사의 훈장으로 회복했으면 true. 2026-11-24부터 중간보스·보스를 잡았을 때만 (정예 구역 돌파는 아님).
        // 보스·중간보스 구역은 그쪽 알림에 훈장 회복을 합쳐 한 줄로 띄운다.
        private bool OnArenaCleared(int chunkIndex, bool boss, bool midBoss)
        {
            clearedArenas.Add(chunkIndex);
            IBlameYou.Core.RunStats.AddArenaCleared();
            var relics = player != null ? IBlameYou.Player.PlayerRelics.For(player.gameObject) : null;
            bool medalHealed = (boss || midBoss) && relics != null && relics.Has(IBlameYou.Player.RelicId.HeroMedal) // 용사의 훈장: 회복 + 머리 위 금 메달
                && HealPlayer(IBlameYou.Player.RelicBook.HeroMedalHealRatio, null);
            if (medalHealed) VfxPlayer.MedalGlint(player, MedalGlintOffset);
            if (!boss) return medalHealed;

            bossCleared = true;
            foreach (var exit in exits.Values) if (exit != null) exit.SetOpen(true);
            StageEvents.PostNotice(medalHealed ? $"출구가 열렸다! · 용사의 훈장 +{MedalPercent}%" : "출구가 열렸다!");
            return medalHealed;
        }

        private static string MedalPercent => $"{IBlameYou.Player.RelicBook.HeroMedalHealRatio * 100f:0}";

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
                StageEvents.PostNotice("마을 발견! 식당·장비 상점");
                GameAudio.PlaySfx("VillageFound", 0.8f, 0f);
            }
            else if (firstVisit && (entered.Kind == RoomKind.Treasure || entered.Kind == RoomKind.Spring))
            {
                // 보물·샘 방을 처음 찾았을 때: 무슨 방인지 알림 + 반짝 소리 (마을과 같은 "발견" 리듬, 더 작게)
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
            // 방 이벤트 보물 고블린 (2026-11-14 사람 결정): 이동 방(세로 방 포함 — Chunks/RoomEventRule)에 처음 들어가면 금화 자루를 멘 고블린이 나타나 도망친다 (난수 안 씀)
            if (firstVisit && RoomEventRule.IsTreasureGoblinRoom(stage.Seed, entered))
            {
                if (entered.IsVertical) goblinWaiting.Add(index); // 바닥 가까이 내려오면 (WakeWaitingGoblin)
                else if (!SpawnTreasureGoblin(index)) goblinWaiting.Add(index); // 설 자리를 못 찾았으면 기다렸다가 다시 (WakeWaitingGoblin)
            }
            // 기다리는 보물 고블린이 있는 세로 방에 위쪽으로 들어오면 아래에서 금화 소리 (2026-11-14 자동 실행): 내려가 보라는 힌트
            if (goblinWaiting.Contains(index) && player != null && player.position.y - Origin(index).y > GoblinWakeHeight)
            {
                StageEvents.PostNotice("아래에서 금화 짤랑이는 소리가 들린다...");
                GameAudio.PlaySfx("CoinPile", 0.35f, 0f);
            }
            RoomEntered?.Invoke(index);
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

        // 깊이 = 시작 방에서의 방 거리 / 보스 거리 (= 방 난이도, RoomLayout 5×4 격자). 어느 방이든 자기 거리로 잰다.
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
