using System;
using System.Collections;
using System.Collections.Generic;
using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 전투 구역: 플레이어가 안쪽으로 들어오면 양쪽에 문(마력 장벽)이 내려오고 적이 차례로 나타난다.
    // 적을 모두 쓰러뜨리면 문이 열린다. 문이 닫힌 동안 카메라는 구역 안만 비춘다.
    public class ArenaZone : MonoBehaviour, IEnemyGroup
    {
        public struct Spawn
        {
            public StageEnemy Kind;
            public Vector3 Position;
        }

        private const float GateHeight = 9f;
        private const float SpawnInterval = 0.3f;
        private const float TriggerInset = 3f;

        private readonly HashSet<HealthSystem> alive = new HashSet<HealthSystem>();
        private List<Spawn> spawns;
        private Func<StageEnemy, Vector3, Transform, HealthSystem> spawnEnemy;
        private Action onCleared;
        private Rect area;
        private bool active;
        private bool cleared;
        private bool spawning;
        private readonly List<GameObject> gates = new List<GameObject>();
        private Rect? previousCameraBounds;

        public bool IsCleared => cleared;

        // left/right: 구역 양 끝 x(월드), ground: 바닥 윗면 y. 이미 클리어한 구역이면 아무 일도 하지 않는다.
        public static ArenaZone Create(Transform parent, float left, float right, float ground, List<Spawn> spawns, bool alreadyCleared,
            Func<StageEnemy, Vector3, Transform, HealthSystem> spawnEnemy, Action onCleared)
        {
            var go = new GameObject("ArenaZone");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((left + right) / 2f, ground, 0f);

            var zone = go.AddComponent<ArenaZone>();
            zone.area = new Rect(left, ground, right - left, GateHeight);
            zone.spawns = spawns;
            zone.spawnEnemy = spawnEnemy;
            zone.onCleared = onCleared;
            zone.cleared = alreadyCleared;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(right - left - TriggerInset * 2f, GateHeight);
            trigger.offset = new Vector2(0f, GateHeight / 2f);
            return zone;
        }

        public void RegisterEnemy(HealthSystem enemy)
        {
            if (enemy == null || enemy.IsDead) return;
            alive.Add(enemy);
            enemy.HealthChanged += (current, max) =>
            {
                if (current > 0f) return;
                alive.Remove(enemy);
                CheckCleared();
            };
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (active || cleared || TrapCommon.PlayerFrom(other) == null) return;
            StartCoroutine(Begin());
        }

        private IEnumerator Begin()
        {
            active = true;
            spawning = true;
            CreateGate(area.xMin + 0.5f);
            CreateGate(area.xMax - 0.5f);
            LockCamera();
            StageEvents.PostNotice("적을 모두 쓰러뜨려라!");

            foreach (var spawn in spawns)
            {
                EffectLibrary.PlayOneShot(spawn.Position + Vector3.up * 0.6f, EffectLibrary.TeleportFrames, 20f, new Color(1f, 0.4f, 0.4f, 0.7f), 1.2f);
                RegisterEnemy(spawnEnemy(spawn.Kind, spawn.Position, transform));
                yield return new WaitForSeconds(SpawnInterval);
            }

            spawning = false;
            CheckCleared();
        }

        private void CheckCleared()
        {
            if (!active || spawning || alive.Count > 0) return;

            active = false;
            cleared = true;
            foreach (var gate in gates) if (gate != null) Destroy(gate);
            gates.Clear();
            UnlockCamera();
            StageEvents.PostNotice("구역 돌파!");
            onCleared?.Invoke();
        }

        private void CreateGate(float x)
        {
            var go = new GameObject("ArenaGate");
            go.transform.SetParent(transform, true);
            go.transform.position = new Vector3(x, area.yMin + GateHeight / 2f, 0f);
            go.layer = TrapCommon.GroundLayer;
            go.AddComponent<BoxCollider2D>().size = new Vector2(1f, GateHeight);

            var frames = TrapCommon.Frames("Gate");
            var visual = EffectLibrary.CreateVisual(go.transform, "Visual", frames, 10f, true, new Color(1f, 0.35f, 0.4f, 0.55f), 1f, 2);
            if (frames == null) visual.transform.localScale = new Vector3(0.8f, GateHeight, 1f);
            else visual.transform.localScale = new Vector3(1f, GateHeight / 3f, 1f); // 그림은 1 × 3유닛
            gates.Add(go);
        }

        private void LockCamera()
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow == null) return;
            previousCameraBounds = follow.Bounds;
            follow.SetBounds(new Rect(area.xMin, -1f, area.width, ChunkData.Height + 1f));
        }

        private void UnlockCamera()
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow == null || !previousCameraBounds.HasValue) return;
            follow.SetBounds(previousCameraBounds.Value);
        }

        private void OnDestroy()
        {
            if (active) UnlockCamera();
        }
    }
}
