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
        private float lockedAt; // 문이 닫힌 시각 (돌파 시간 알림)
        private readonly List<GameObject> gates = new List<GameObject>();
        private Rect? previousCameraBounds;
        private string fightMusic = BossMusic; // 이번 보스 구역에서 튼 곡 (Boss 또는 MidBoss)
        private bool bossFight; // 보스·중간보스가 나오는 구역: 문이 닫히는 동안 보스 곡 (Resources/Audio/Music/Boss)

        private const string BossMusic = "Boss";
        private const string MidBossMusic = "MidBoss"; // 중간보스(쌍둥이 슬라임 기사) 전용 곡: 두 목소리가 주고받는 짧은 반복 (Tools/AudioGen)
        public const float LateMusicDepth = 0.5f; // 방 깊이(시작 방 거리 / 보스 거리)가 이만큼 넘으면(대개 중간보스 뒤) 후반 던전 곡 Dungeon2

        // 깊이에 맞는 스테이지 곡: 전반 Dungeon, 후반(보스까지 거리의 절반 이후) 더 빠르고 화려한 Dungeon2 (Tools/AudioGen).
        public static string StageMusicFor(float depth) => depth >= LateMusicDepth ? "Dungeon2" : "Dungeon";

        // 지금 문이 닫혀 싸우는 구역 (HUD 남은 적 수·화면 밖 적 길잡이·마지막 적 표시). 없으면 null.
        public static ArenaZone Current { get; private set; }
        private int pendingSpawns; // 아직 안 나온 적 수 (차례로 나오는 중)
        // 남은 적 = 살아 있는 적 + 아직 안 나온 적
        public int Remaining => alive.Count + pendingSpawns;
        public bool HasBoss => bossFight; // 보스 구역이면 위쪽 가운데는 보스 체력바 자리
        public IEnumerable<HealthSystem> AliveEnemies => alive;

        // 매복 구역 (Chunks/AmbushRule): 문이 닫히면 적이 바닥 소환 대신 위에서 떨어진다. 먼저 경고(바닥 타원 + 천장 먼지 + 알림) 뒤 AmbushWarnTime 만큼 기다린다.
        public bool Ambush { get; set; }
        private Transform enteredPlayer; // 문을 닫게 한 플레이어 (매복 돌파 골드가 날아갈 곳)
        public bool WallArchers { get; set; } // 매복 변형 "벽 위 궁수" 방 (RoomShaper.IsWallArcherAmbush): 선반 위 적은 떨어지지 않고 그 자리에 나타난다
        public bool Burrow { get; set; } // 매복 변형 "땅속 매복" (AmbushRule.IsBurrow): 바닥 적이 천장 대신 땅속에서 튀어나온다
        public bool Mixed { get; set; }  // 섞인 매복 (AmbushRule.IsMixed, Burrow일 때만): 적이 번갈아 땅속·천장에서 나온다
        private bool BurrowsAt(int i) => AmbushRule.BurrowsAt(Burrow, Mixed, i);
        private const float AmbushWarnTime = 0.9f;
        private const float BurrowPopSpeed = 7f; // 땅에서 튀어나올 때 위로 솟는 속도
        private const float AmbushDropHeight = 6f;   // 떨어지기 시작하는 높이 (천장·발판이 낮으면 그 아래)
        private const float AmbushCeilingMargin = 1.2f;
        private const float AmbushMinDrop = 1.5f;    // 이보다 낮으면 그냥 제자리에 나온다
        private static readonly Color AmbushWarnColor = new Color(0.95f, 0.3f, 0.2f);

        public bool IsCleared => cleared;
        public bool IsLocked => active; // 문이 닫혀 싸우는 중 (방 문 표시가 붉게)
        public Rect Area => area; // 구역 범위 (보스가 무작위 위치를 고를 때)

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

        // 쓰러지지 않고 사라진 적(보스에게 흡수된 슬라임 등)은 목록에서 빼고 클리어 여부를 다시 본다.
        private void Update()
        {
            if (active && !cleared && Ambush) ShowFastClearCountdown();
            if (!active || alive.Count == 0) return;
            if (alive.RemoveWhere(enemy => enemy == null) > 0) CheckCleared();
        }

        // 매복 빠른 돌파 남은 시간 (2026-11-14 자동 실행): 구역 위쪽 가운데에 10·5·3·2·1초를 금색으로 (AmbushRule.FastClearTime 안에 돌파하면 골드 더)
        private int lastFastCount = -1;

        private void ShowFastClearCountdown()
        {
            int left = Mathf.CeilToInt(AmbushRule.FastClearTime - (Time.time - lockedAt));
            if (left == lastFastCount || left <= 0) return;
            lastFastCount = left;
            if (left != 10 && left != 5 && left > 3) return;
            DamageNumbers.ShowText(new Vector3(area.center.x, area.yMax - 1.5f, 0f), left.ToString(), new Color(1f, 0.85f, 0.3f), 1.1f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (active || cleared || player == null) return;
            enteredPlayer = player.transform;
            StartCoroutine(Begin());
        }

        private IEnumerator Begin()
        {
            active = true;
            spawning = true;
            lockedAt = Time.time;
            pendingSpawns = spawns.Count;
            Current = this;
            bossFight = spawns.Exists(IsBoss);
            if (bossFight) GameAudio.PlayMusic(fightMusic = spawns.Exists(IsKing) ? BossMusic : MidBossMusic);
            CreateGate(area.xMin + 0.5f);
            CreateGate(area.xMax - 0.5f);
            foreach (var hole in holes) CreateHoleSeal(hole);
            LockCamera();
            GameAudio.PlaySfx("ArenaLock", 0.8f);
            if (!bossFight && spawns.Exists(IsElite))
            {
                // 정예 등장 경고: 낮은 경고음 + 화면 가장자리 보라 깜빡임 0.6초 + 안내
                GameAudio.PlaySfx("EliteWarn", 0.85f, 0f);
                ScreenFlash.EdgeWarn(EliteWarnColor, 0.6f);
                StageEvents.PostNotice("정예 등장!");
            }
            else if (Ambush)
            {
                // 매복 경고: 경고음 + 화면 가장자리 붉은 깜빡임 + 안내. 적은 아래 AmbushWarnTime 뒤 위에서 떨어진다
                GameAudio.PlaySfx("TrapWarn", 0.9f, 0f);
                ScreenFlash.EdgeWarn(AmbushWarnColor, 0.5f);
                StageEvents.PostNotice(Burrow && Mixed ? "매복이다! 위와 발밑을 조심해!"
                    : Burrow ? "매복이다! 발밑을 조심해!"
                    : spawns.Exists(OnLedge) ? "매복이다! 위와 벽 위 궁수를 조심해!" : "매복이다! 위를 조심해!");
            }
            else
            {
                StageEvents.PostNotice("적을 모두 쓰러뜨려라!");
            }

            // 소환 자리 예고 (적 등장 연출, 2026-11-13): 문이 닫히면 보스가 아닌 적이 나올 바닥마다 붉은 타원이 깜빡이다가, 그 적이 나오면 사라진다.
            var marks = new List<SpriteRenderer>(spawns.Count);
            foreach (var spawn in spawns) marks.Add(IsBoss(spawn) ? null : CreateSpawnMark(spawn.Position));
            StartCoroutine(PulseSpawnMarks(marks));

            // 매복: 떨어질 자리 위 천장에서 먼지가 부슬부슬 떨어지며 잠깐 기다린다
            var drops = new float[spawns.Count];
            if (Ambush)
            {
                // 땅속 매복: 바닥이 우르르 흔들리고 나올 자리마다 흙이 튄다. 섞인 매복이면 나머지는 천장 먼지 뒤 떨어진다
                if (Burrow) CameraFollow.ShakeMain(AmbushWarnTime, 0.06f);
                for (int i = 0; i < spawns.Count; i++)
                {
                    if (IsBoss(spawns[i]) || OnLedge(spawns[i])) continue; // 벽 위 궁수(선반 위 적)는 그 자리에 나타난다
                    if (BurrowsAt(i))
                    {
                        StartCoroutine(GroundRumble(spawns[i].Position));
                        continue;
                    }
                    drops[i] = AmbushDrop(spawns[i].Position);
                    if (drops[i] > 0f) StartCoroutine(CeilingDust(spawns[i].Position + Vector3.up * (drops[i] + 0.6f)));
                }
                yield return new WaitForSeconds(AmbushWarnTime);
            }

            for (int i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                bool falls = drops[i] > 0f; // 매복: 표시 타원은 남겨 두고 떨어지는 적이 땅에 닿을 때까지 보이게 (WatchLanding이 지운다)
                if (marks[i] != null && !falls) Destroy(marks[i].gameObject);
                bool dropsIn = IsBoss(spawn); // 보스·중간보스는 위에서 떨어진다 (BossEntrance)
                bool burrows = Ambush && BurrowsAt(i) && !dropsIn && !OnLedge(spawn);
                if (burrows)
                {
                    // 땅에서 흙을 튀기며 "퍽" 솟아오른다
                    var feet = spawn.Position - Vector3.up * ChunkStreamer.SpawnLift;
                    VfxPlayer.Dust(feet, 1.1f);
                    EffectLibrary.PlayOneShot(feet + Vector3.up * 0.3f, EffectLibrary.TeleportFrames, 20f, new Color(0.62f, 0.46f, 0.3f, 0.8f), 1.1f);
                    GameAudio.PlaySfx("Land", 0.75f);
                }
                else if (!dropsIn && !falls)
                {
                    EffectLibrary.PlayOneShot(spawn.Position + Vector3.up * 0.6f, EffectLibrary.TeleportFrames, 20f, new Color(1f, 0.4f, 0.4f, 0.7f), 1.2f);
                    GameAudio.PlaySfx("Summon", 0.6f); // 붉은 연기 속에서 "뿅"
                }
                pendingSpawns--;
                var spawned = spawnEnemy(spawn.Kind, falls ? spawn.Position + Vector3.up * drops[i] : spawn.Position, transform);
                RegisterEnemy(spawned);
                // 연기 속에서 길쭉하게 "뿅" 솟았다가 제 모양으로 (그림만, 판정·발 위치 그대로)
                if (!dropsIn && !falls && spawned != null) IBlameYou.Enemies.EnemySquash.For(spawned.gameObject).Spawn();
                if (burrows && spawned != null)
                {
                    var rb = spawned.GetComponent<Rigidbody2D>();
                    if (rb != null && rb.gravityScale > 0f) rb.linearVelocity = new Vector2(rb.linearVelocity.x, BurrowPopSpeed); // 나는 적은 튀기지 않는다
                }
                if (falls) StartCoroutine(WatchLanding(spawned, marks[i], spawn.Position.y));
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
            if (Current == this) Current = null;
            foreach (var gate in gates)
            {
                if (gate == null) continue;
                var fx = gate.GetComponent<ArenaGate>();
                if (fx != null) fx.Open(); // 충돌은 바로 풀고 그림만 위로 걷힌다
                else Destroy(gate);
            }
            gates.Clear();
            GameAudio.PlaySfx("GateOpen", 0.45f);
            UnlockCamera();
            LeaveBossMusic();
            // 중간보스가 없는 외길 모드도 후반에는 후반 곡으로: 전반 곡이 나오는 중에 깊이 절반을 넘긴 구역을 돌파하면 바꾼다
            if (GameAudio.CurrentMusic == "Dungeon" && StageMusicFor(StageEvents.Depth) != "Dungeon") GameAudio.PlayMusic(StageMusicFor(StageEvents.Depth));
            // 매복 돌파 보너스 (2026-11-14 ① 컨텐츠): 경고를 보고 버틴 보상으로 골드 조금 (복주머니 안 붙음, 동전 무더기와 같은 연출)
            // 빠른 돌파(AmbushRule.FastClearTime초 안)면 골드를 조금 더 (2026-11-14 자동 실행)
            float clearTime = Time.time - lockedAt;
            int bonus = Ambush && enteredPlayer != null
                ? IBlameYou.Core.RunGold.OnCoinPile(enteredPlayer, new Vector3(area.center.x, area.yMin + 2f, 0f), AmbushRule.ClearGoldFor(clearTime)) : 0;
            StageEvents.PostNotice(bonus > 0
                ? $"{ClearNotice(clearTime)} 매복 돌파 골드 +{bonus}{(AmbushRule.IsFastClear(clearTime) ? " (빠른 돌파!)" : "")}"
                : ClearNotice(clearTime));
            VfxPlayer.ClearBurst(new Vector3(area.center.x, area.yMin + 3f, 0f)); // 구역 가운데에서 별·하트 축하
            onCleared?.Invoke();
        }

        private static readonly Color SpawnMarkColor = new Color(1f, 0.3f, 0.35f);
        private const float SpawnMarkWidth = 1.3f;
        private const float SpawnMarkHeight = 0.32f;

        // position: 적이 만들어질 자리 (바닥 + SpawnLift). 땅 그림 위, 캐릭터 뒤에 납작한 타원.
        private SpriteRenderer CreateSpawnMark(Vector3 position)
        {
            var go = new GameObject("SpawnMark");
            go.transform.SetParent(transform, true);
            go.transform.position = new Vector3(position.x, position.y - ChunkStreamer.SpawnLift + 0.05f, 0f); // 적은 바닥에서 SpawnLift 위에 만들어진다
            go.transform.localScale = new Vector3(SpawnMarkWidth, SpawnMarkHeight, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateCircle();
            renderer.color = new Color(SpawnMarkColor.r, SpawnMarkColor.g, SpawnMarkColor.b, 0.5f);
            renderer.sortingOrder = -1; // 땅 그림(-3 이하) 위, 발판·캐릭터(0 이상) 뒤 (착지 먼지와 같은 층)
            return renderer;
        }

        // 남은 예고 타원이 0.3~0.75 사이로 빠르게 깜빡이며 살짝 커졌다 줄었다 한다. 다 사라지면 끝.
        private IEnumerator PulseSpawnMarks(List<SpriteRenderer> marks)
        {
            while (true)
            {
                bool any = false;
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                foreach (var mark in marks)
                {
                    if (mark == null) continue;
                    any = true;
                    mark.color = new Color(SpawnMarkColor.r, SpawnMarkColor.g, SpawnMarkColor.b, Mathf.Lerp(0.3f, 0.75f, wave));
                    float s = Mathf.Lerp(0.9f, 1.05f, wave);
                    mark.transform.localScale = new Vector3(SpawnMarkWidth * s, SpawnMarkHeight * s, 1f);
                }
                if (!any) yield break;
                yield return null;
            }
        }

        // 바닥보다 높은 선반 위에 놓인 구역 적인지 (RoomShaper 매복 변형 "벽 위 궁수"). 바닥 적은 area.yMin + SpawnLift에 만들어진다.
        private bool OnLedge(Spawn spawn) => WallArchers && spawn.Position.y > area.yMin + ChunkStreamer.SpawnLift + 1.5f;

        // 매복 적이 떨어질 높이: 자리 위로 천장·발판이 있으면 그 아래까지. 너무 낮으면 0 (제자리 소환).
        private static float AmbushDrop(Vector3 position)
        {
            float drop = AmbushDropHeight;
            var hit = Physics2D.BoxCast(position + Vector3.up * 0.6f, new Vector2(1f, 0.2f), 0f, Vector2.up, AmbushDropHeight + AmbushCeilingMargin, 1 << TrapCommon.GroundLayer); // 몸 폭만큼 (발판 끝에 걸리지 않게)
            if (hit.collider != null) drop = Mathf.Min(drop, hit.distance + 0.6f - AmbushCeilingMargin);
            return drop >= AmbushMinDrop ? drop : 0f;
        }

        // 땅속 매복 경고: 나올 자리 바닥에서 흙이 세 번 튄다 (점점 세게)
        private IEnumerator GroundRumble(Vector3 spawnPosition)
        {
            var feet = spawnPosition - Vector3.up * ChunkStreamer.SpawnLift;
            for (int i = 0; i < 3; i++)
            {
                VfxPlayer.Dust(feet + Vector3.right * ((i % 2 == 0 ? -1f : 1f) * 0.25f), 0.35f + i * 0.2f);
                yield return new WaitForSeconds(AmbushWarnTime / 3f);
            }
        }

        // 천장(떨어질 자리 위)에서 흙먼지가 세 번 부슬부슬
        private IEnumerator CeilingDust(Vector3 point)
        {
            for (int i = 0; i < 3; i++)
            {
                VfxPlayer.Dust(point + Vector3.right * ((i - 1) * 0.35f), 0.45f);
                yield return new WaitForSeconds(AmbushWarnTime / 3f);
            }
        }

        // 떨어진 매복 적이 바닥(경고 타원 높이) 근처에 오거나 2초가 지나면 타원을 지우고 쿵 먼지 + 착지 소리.
        private IEnumerator WatchLanding(HealthSystem enemy, SpriteRenderer mark, float groundY)
        {
            float until = Time.time + 2f;
            while (enemy != null && !enemy.IsDead && Time.time < until && enemy.transform.position.y > groundY + 0.15f)
                yield return null;
            if (mark != null) Destroy(mark.gameObject);
            if (enemy == null || enemy.IsDead || Time.time >= until) yield break;
            var feet = new Vector3(enemy.transform.position.x, groundY - ChunkStreamer.SpawnLift, 0f);
            VfxPlayer.Dust(feet, 0.9f);
            GameAudio.PlaySfx("Land", 0.7f);
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
            ArenaGate.Attach(go, visual, area.yMin); // 위에서 쿵 떨어지고, 닫힌 동안 숨쉬고, 돌파하면 위로 걷힌다
            gates.Add(go);
        }

        // 세로 전투 방 (2026-11-14): 문이 닫히는 동안 천장·바닥 구멍(위·아래 문)도 가로 장벽으로 막는다. 월드 좌표 사각형.
        private readonly List<Rect> holes = new List<Rect>();

        public void SealHoles(IEnumerable<Rect> rects) => holes.AddRange(rects);

        private void CreateHoleSeal(Rect hole)
        {
            var go = new GameObject("ArenaHoleSeal");
            go.transform.SetParent(transform, true);
            go.transform.position = new Vector3(hole.center.x, hole.center.y, 0f);
            go.layer = TrapCommon.GroundLayer;
            go.AddComponent<BoxCollider2D>().size = hole.size;
            var frames = TrapCommon.Frames("Gate");
            var visual = EffectLibrary.CreateVisual(go.transform, "Visual", frames, 10f, true, new Color(1f, 0.35f, 0.4f, 0.55f), 1f, 2);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 세로 문 그림을 눕힌다
            visual.transform.localScale = frames == null ? new Vector3(0.8f, hole.width, 1f) : new Vector3(1f, hole.width / 3f, 1f);
            gates.Add(go); // 돌파하면 다른 문과 함께 치운다
        }

        private void LockCamera()
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow == null) return;
            previousCameraBounds = follow.Bounds;
            follow.SetBounds(new Rect(area.xMin, FallRecovery.FloorY - 1f, area.width, ChunkData.Height + 1f)); // 방 격자 모드에서는 지금 방 바닥 기준
        }

        private void UnlockCamera()
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow == null || !previousCameraBounds.HasValue) return;
            follow.SetBounds(previousCameraBounds.Value);
        }

        // "구역 돌파! (N초)": 문이 닫힌 순간부터 마지막 적이 쓰러질 때까지 (게임 시간, 일시정지·창은 빠진다). 1초 미만은 1초로.
        public static string ClearNotice(float seconds) => $"구역 돌파! ({Mathf.Max(1, Mathf.RoundToInt(seconds))}초)";

        private static readonly Color EliteWarnColor = new Color(0.62f, 0.25f, 0.95f); // 정예 보라 (미니맵 정예 색과 같은 계열)

        private static bool IsElite(Spawn spawn) => spawn.Kind == StageEnemy.MotherSlime || spawn.Kind == StageEnemy.GoblinWarrior;

        private static bool IsKing(Spawn spawn) => spawn.Kind == StageEnemy.SlimeKing;

        private static bool IsBoss(Spawn spawn) =>
            spawn.Kind == StageEnemy.SlimeKing || spawn.Kind == StageEnemy.SlimeKnightFire || spawn.Kind == StageEnemy.SlimeKnightWater;

        // 보스 곡이 아직 나오고 있을 때만 스테이지 곡으로 (그사이 메뉴 곡 등으로 바뀌었으면 건드리지 않는다).
        private void LeaveBossMusic()
        {
            if (!bossFight) return;
            bossFight = false;
            if (GameAudio.CurrentMusic == fightMusic) GameAudio.PlayMusic(StageMusicFor(StageEvents.Depth));
        }

        private void OnDestroy()
        {
            if (active) UnlockCamera();
            if (Current == this) Current = null;
            LeaveBossMusic(); // 싸우다 방을 떠나거나 구역이 치워지면
        }
    }
}
