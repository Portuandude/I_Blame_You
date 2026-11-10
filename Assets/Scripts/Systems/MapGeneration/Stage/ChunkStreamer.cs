using System;
using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마인크래프트처럼 플레이어 주변 청크만 씬에 만들어 두고, 멀어진 청크는 치운다.
    // 청크 데이터(StageData)는 시드로 미리 다 만들어져 있어서, 다시 다가가면 같은 지형이 그대로 다시 생긴다.
    // 방 격자 모드(InitializeRooms, Docs/LevelDesign.md): 청크 하나 = 방 하나. 지금 방만 만들어 두고, 문(방 가장자리·천장/바닥 구멍)을
    // 지나면 옆 방으로 옮긴다 (던그리드식). 방들은 월드에서 서로 떨어진 자리에 놓이고, 카메라는 지금 방 안에서만 움직인다.
    // 청크를 치워도 남는 기록: 쓰러뜨린 적, 돌파한 전투 구역, 주운 구역 보상과 유물 상자, 켠 체크포인트, 연 봉인문, 보스 돌파 여부.
    public class ChunkStreamer : MonoBehaviour, IEnemyGroup
    {
        public const int LoadRadius = 2;   // 플레이어가 있는 청크 좌우로 몇 청크까지 만들어 둘지
        public const int UnloadRadius = 3; // 이보다 멀어지면 치운다 (경계에서 만들고 치우기를 반복하지 않게 한 칸 여유)
        private const float SweepInterval = 0.5f;
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
        private readonly HashSet<int> takenRelics = new HashSet<int>(); // 정예 구역 유물 상자에서 유물을 고른 청크
        private readonly HashSet<int> activatedCheckpoints = new HashSet<int>();
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
        // 미니맵 자물쇠: 아직 안 연 협동 봉인문이 있는 곁방 (RoomShaper.HasPairGate)
        public bool HasClosedPairGate(int index) =>
            rooms != null && index >= 0 && index < rooms.Rooms.Count && RoomShaper.HasPairGate(rooms.Rooms[index]) && !openedGates.Contains(index);
        public event Action<int> RoomEntered;

        public StageData Stage => stage;

        // 체크포인트를 처음 켰을 때 (청크 번호, 복귀 위치). 런 진행 저장이 듣는다.
        public event Action<int, Vector3> CheckpointActivated;
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
                    max = Vector2.Max(max, o);
                }

                return new Rect(min.x, min.y - 1f, max.x - min.x + ChunkData.Width, max.y - min.y + ChunkData.Height + 1f);
            }
        }

        // 카메라가 머물 영역: 외길 모드는 스테이지 전체, 방 격자 모드는 지금 방.
        public Rect CameraBounds => rooms == null ? WorldBounds : RoomRect(currentRoom);

        public Rect RoomRect(int index)
        {
            var o = Origin(index);
            return new Rect(o.x, o.y - 1f, ChunkData.Width, ChunkData.Height + 1f);
        }

        // 청크(방)의 월드 원점 (왼쪽 아래). 외길 모드는 가로로 이어 붙이고, 방 격자 모드는 격자 칸마다 간격을 두고 놓는다.
        public Vector2 Origin(int index)
        {
            if (rooms == null) return new Vector2(index * ChunkData.Width, 0f);
            var room = rooms.Rooms[index];
            return new Vector2(room.X * (ChunkData.Width + RoomGap), room.Y * (ChunkData.Height + RoomGap));
        }

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

        // 이어하기: 체크포인트가 있던 방에서 시작한다 (SetPlayer 전에 부를 것).
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
                StageEvents.Progress = RoomProgress(currentRoom);
            }
            else
            {
                StageEvents.Progress = Mathf.Clamp01(player.position.x / stage.WidthTiles);
            }

            if (Time.time >= nextSweep)
            {
                nextSweep = Time.time + SweepInterval;
                SweepEnemies();
            }
        }

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
                    case FeatureKind.MovingPlatform:
                        // 같은 청크의 발판 두 개(갈아타기)는 반 박자 엇갈려 움직인다.
                        MovingPlatform.Create(root, new Vector2(ox + f.X, oy + f.Y + 1f), f.Width, f.Direction, index * 0.37f + platformOrdinal++ * 0.5f);
                        break;
                    case FeatureKind.Crumble:
                        CrumblingPlatform.Create(root, new Vector2(ox + f.X, oy + f.Y + 1f), f.Width);
                        break;
                    case FeatureKind.Reward:
                        // 순간이동 지름길 선반의 회복 구슬. 전투 구역 보상과 같은 기록(방 번호)을 쓴다: 세로 방에는 전투 구역이 없다
                        SpawnReward(root, index, new Vector2(ox + f.X + 0.5f, oy + f.Y), false);
                        break;
                    case FeatureKind.Checkpoint:
                        int checkpointChunk = index;
                        var checkpointPoint = new Vector3(ox + f.X + 0.5f, oy + f.Y + SpawnLift, 0f);
                        Checkpoint.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), activatedCheckpoints.Contains(index), () =>
                        {
                            activatedCheckpoints.Add(checkpointChunk);
                            CheckpointActivated?.Invoke(checkpointChunk, checkpointPoint);
                        });
                        break;
                    case FeatureKind.Exit:
                        exits[index] = StageExit.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), bossCleared);
                        break;
                    case FeatureKind.ElementGate:
                        int gateChunk = index;
                        var gateElements = f.HasSecondElement
                            ? new[] { (Element)f.ElementIndex, (Element)f.SecondElementIndex }
                            : new[] { (Element)f.ElementIndex };
                        IBlameYou.Puzzles.ElementGate.Create(root, new Vector2(ox + f.X + 0.5f, oy + f.Y), gateElements,
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
                        var arenaRoot = root;
                        var rewardPosition = new Vector2(ox + f.X + f.Width / 2f, oy + f.Y);
                        var relicPosition = rewardPosition + Vector2.left * RelicChestOffset; // 구역 가운데 발판(7~11, 20~24칸) 사이 바닥
                        ArenaZone.Create(root, ox + f.X, ox + f.X + f.Width, oy + f.Y, arenaSpawns, clearedArenas.Contains(index), spawnEnemy,
                            () =>
                            {
                                OnArenaCleared(arenaChunk, boss);
                                if (boss || arenaRoot == null) return;
                                SpawnReward(arenaRoot, arenaChunk, rewardPosition, true);
                                if (elite) SpawnRelicChest(arenaRoot, arenaChunk, relicPosition, true);
                            });
                        // 돌파했지만 보상을 줍지 않고 떠났던 구역이면 다시 왔을 때 보상을 그대로 둔다.
                        if (!boss && clearedArenas.Contains(index))
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

            if (rooms != null) LoadRoomExtras(index, root, origin);
        }

        // 방 격자 모드의 곁방 보상: 보물 방 = 유물 상자, 샘 방 = 회복 구슬 (가져가면 기록돼 다시 오면 없다).
        private void LoadRoomExtras(int index, Transform root, Vector2 origin)
        {
            var room = rooms.Rooms[index];
            RoomDoorMarkers.Create(root, origin, room, geometries[index]);
            // 좌우 문 곁방은 평평한 휴식 지형 가운데, 세로 곁방은 바닥 구멍을 피해 바닥 칸 옆
            var floorSpot = geometries[index].Floor;
            var floor = floorSpot.HasValue
                ? new Vector2(origin.x + floorSpot.Value.x + 2.5f, origin.y + floorSpot.Value.y)
                : new Vector2(origin.x + ChunkData.Width / 2f, origin.y + stage.Chunks[index].EntryHeight);
            // 협동 봉인문이 선 세로 곁방은 상자를 봉인문(8열) 왼쪽으로 (RoomShaper.HasPairGate)
            if (room.Kind == RoomKind.Treasure && room.IsVertical && RoomShaper.HasPairGate(room))
                floor = new Vector2(origin.x + RoomShaper.ShaftPairChestX + 0.5f, origin.y + RoomShaper.ShaftGround);
            if (room.Kind == RoomKind.Treasure) SpawnRelicChest(root, index, floor, false);
            else if (room.Kind == RoomKind.Spring) SpawnReward(root, index, floor, false);
        }

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

        private void OnArenaCleared(int chunkIndex, bool boss)
        {
            clearedArenas.Add(chunkIndex);
            IBlameYou.Core.RunStats.AddArenaCleared();
            if (!boss) return;

            bossCleared = true;
            foreach (var exit in exits.Values) if (exit != null) exit.SetOpen(true);
            StageEvents.PostNotice("출구가 열렸다!");
        }

        // 일반 전투 구역 보상 (보스 구역은 출구가 보상이라 없음). 이미 주웠으면 만들지 않는다.
        private void SpawnReward(Transform root, int chunkIndex, Vector2 position, bool dropped)
        {
            if (collectedRewards.Contains(chunkIndex)) return;
            ArenaReward.Create(root, position, dropped, () => collectedRewards.Add(chunkIndex));
        }

        // 정예 구역 유물 상자. 이미 유물을 골랐으면 만들지 않는다.
        private void SpawnRelicChest(Transform root, int chunkIndex, Vector2 position, bool dropped)
        {
            if (takenRelics.Contains(chunkIndex)) return;
            RelicChest.Create(root, position, stage.Seed, chunkIndex, dropped, () => takenRelics.Add(chunkIndex));
        }

        // 런 진행 저장용: 지금까지의 기록을 꺼내거나(Export), 저장된 기록으로 되돌린다(Import, 청크를 만들기 전에 부를 것).
        public StageRecords ExportRecords()
        {
            var records = new StageRecords
            {
                killed = ToArray(killed),
                clearedArenas = ToArray(clearedArenas),
                collectedRewards = ToArray(collectedRewards),
                takenRelics = ToArray(takenRelics),
                activatedCheckpoints = ToArray(activatedCheckpoints),
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
            Fill(takenRelics, records.takenRelics);
            Fill(activatedCheckpoints, records.activatedCheckpoints);
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
            else if (local.x > ChunkData.Width - DoorEdge && (room.Doors & DoorDir.Right) != 0) passed = DoorDir.Right;
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
            else if (door == DoorDir.Right) column = ChunkData.Width - 2;
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
        }

        // 방에 들어왔을 때: 가 본 방 기록, 카메라 영역, 낭떠러지 높이.
        private void EnterRoom(int index)
        {
            doorReadyTime = Time.time + DoorCooldown;
            reapplyCameraBounds = true;
            visitedRooms.Add(index);
            FallRecovery.FloorY = Origin(index).y;
            var follow = Camera.main != null ? Camera.main.GetComponent<IBlameYou.Core.CameraFollow>() : null;
            if (follow != null)
            {
                follow.SetBounds(RoomRect(index));
                follow.SnapToTarget();
            }

            StageEvents.Progress = RoomProgress(index); // 배경 색조가 첫 프레임부터 이 방 진행도를 쓰게 (Update보다 먼저 올 수 있음)
            RoomEntered?.Invoke(index);
        }

        // 진행도 = 주 경로에서 몇 번째 방인지 (곁방은 붙어 있는 방 기준).
        private float RoomProgress(int index)
        {
            var room = rooms.Rooms[index];
            if (!room.MainPath && room.ParentIndex >= 0) room = rooms.Rooms[room.ParentIndex];
            int last = 0;
            foreach (var r in rooms.Rooms) if (r.MainPath) last = Mathf.Max(last, r.PathIndex);
            return last > 0 ? Mathf.Clamp01(room.PathIndex / (float)last) : 0f;
        }

        private void OnDestroy()
        {
            StageEvents.Progress = 0f;
            FallRecovery.FloorY = 0f;
        }
    }
}
