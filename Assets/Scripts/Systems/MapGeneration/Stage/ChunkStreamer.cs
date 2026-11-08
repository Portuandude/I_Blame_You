using System;
using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 마인크래프트처럼 플레이어 주변 청크만 씬에 만들어 두고, 멀어진 청크는 치운다.
    // 청크 데이터(StageData)는 시드로 미리 다 만들어져 있어서, 다시 다가가면 같은 지형이 그대로 다시 생긴다.
    // 청크를 치워도 남는 기록: 쓰러뜨린 적, 돌파한 전투 구역, 켠 체크포인트, 보스 돌파 여부.
    public class ChunkStreamer : MonoBehaviour, IEnemyGroup
    {
        public const int LoadRadius = 2;   // 플레이어가 있는 청크 좌우로 몇 청크까지 만들어 둘지
        public const int UnloadRadius = 3; // 이보다 멀어지면 치운다 (경계에서 만들고 치우기를 반복하지 않게 한 칸 여유)
        private const float SweepInterval = 0.5f;
        public const float SpawnLift = 1.2f; // 서 있는 칸 바닥에서 이만큼 띄워 만들고 떨어뜨린다 (몸이 땅에 박히지 않게)

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
        private readonly HashSet<int> activatedCheckpoints = new HashSet<int>();
        private readonly Dictionary<int, StageExit> exits = new Dictionary<int, StageExit>();

        private StageData stage;
        private Sprite groundTile;
        private Func<StageEnemy, Vector3, Transform, HealthSystem> spawnEnemy;
        private Transform chunkRoot;
        private Transform player;
        private bool bossCleared;
        private float nextSweep;

        public StageData Stage => stage;
        public Rect WorldBounds => new Rect(0f, -1f, stage.WidthTiles, ChunkData.Height + 1f);

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
            Refresh();
        }

        public void RegisterEnemy(HealthSystem enemy)
        {
            if (enemy != null && !enemy.IsDead) extras.Add(enemy);
        }

        private void Update()
        {
            if (player == null || stage == null) return;

            Refresh();
            StageEvents.Progress = Mathf.Clamp01(player.position.x / stage.WidthTiles);

            if (Time.time >= nextSweep)
            {
                nextSweep = Time.time + SweepInterval;
                SweepEnemies();
            }
        }

        private int ChunkAt(float x) => Mathf.Clamp(Mathf.FloorToInt(x / ChunkData.Width), 0, stage.Chunks.Count - 1);

        private void Refresh()
        {
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
            var root = ChunkBuilder.Build(chunk, chunkRoot, groundTile);
            loaded[index] = root;
            float ox = index * ChunkData.Width;

            var arenaSpawns = new List<ArenaZone.Spawn>();
            foreach (var f in chunk.Features)
            {
                if (f.Kind == FeatureKind.Enemy && f.Deferred)
                    arenaSpawns.Add(new ArenaZone.Spawn { Kind = f.Enemy, Position = EnemyPosition(ox, f) });
            }

            foreach (var f in chunk.Features)
            {
                switch (f.Kind)
                {
                    case FeatureKind.Spikes:
                        SpikeTrap.Create(root, new Vector2(ox + f.X, f.Y), f.Width);
                        break;
                    case FeatureKind.FlameJet:
                        FlameJetTrap.Create(root, new Vector2(ox + f.X + 0.5f, f.Y), f.X * 0.37f + index * 0.61f);
                        break;
                    case FeatureKind.ArrowTrap:
                        ArrowTrap.Create(root, new Vector2(ox + f.X + 0.5f, f.Y + 0.5f), f.Direction);
                        break;
                    case FeatureKind.Crumble:
                        CrumblingPlatform.Create(root, new Vector2(ox + f.X, f.Y + 1f), f.Width);
                        break;
                    case FeatureKind.Checkpoint:
                        int checkpointChunk = index;
                        Checkpoint.Create(root, new Vector2(ox + f.X + 0.5f, f.Y), activatedCheckpoints.Contains(index),
                            () => activatedCheckpoints.Add(checkpointChunk));
                        break;
                    case FeatureKind.Exit:
                        exits[index] = StageExit.Create(root, new Vector2(ox + f.X + 0.5f, f.Y), bossCleared);
                        break;
                    case FeatureKind.Arena:
                        int arenaChunk = index;
                        bool boss = chunk.Kind == ChunkKind.Boss;
                        ArenaZone.Create(root, ox + f.X, ox + f.X + f.Width, f.Y, arenaSpawns, clearedArenas.Contains(index), spawnEnemy,
                            () => OnArenaCleared(arenaChunk, boss));
                        break;
                    case FeatureKind.Enemy:
                        if (!f.Deferred) SpawnTracked(index, ox, f);
                        break;
                }
            }
        }

        private static Vector3 EnemyPosition(float ox, ChunkFeature f) => new Vector3(ox + f.X + 0.5f, f.Y + SpawnLift, 0f);

        private void SpawnTracked(int chunkIndex, float ox, ChunkFeature f)
        {
            if (killed.Contains(f.Id) || alive.ContainsKey(f.Id)) return;

            var health = spawnEnemy(f.Enemy, EnemyPosition(ox, f), transform);
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
            if (!boss) return;

            bossCleared = true;
            foreach (var exit in exits.Values) if (exit != null) exit.SetOpen(true);
            StageEvents.PostNotice("출구가 열렸다!");
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
            int center = ChunkAt(player.position.x);
            float minX = (center - UnloadRadius) * ChunkData.Width;
            float maxX = (center + UnloadRadius + 1) * ChunkData.Width;

            List<int> removed = null;
            foreach (var pair in alive)
            {
                var health = pair.Value.Health;
                if (health == null) { (removed ?? (removed = new List<int>())).Add(pair.Key); continue; }

                var position = health.transform.position;
                if (position.y < FallRecovery.KillY)
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
                if (position.y < FallRecovery.KillY || position.x < minX || position.x > maxX)
                {
                    Destroy(health.gameObject);
                    extras.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            StageEvents.Progress = 0f;
        }
    }
}
