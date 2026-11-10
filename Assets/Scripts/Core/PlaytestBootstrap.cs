using System.Collections;
using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 빈 씬에 이 컴포넌트 하나만 두고 Play를 누르면: 시작 화면(개발자 모드 선택)을 띄우고, 시작하면
    // 시드로 스테이지 1(청크를 가로로 이어 붙인 약 30분 분량의 맵)을 만들고, 시작 지점에 플레이어와 카메라를 준비한다.
    // 청크는 ChunkStreamer가 플레이어 주변만 만들고 멀어지면 치운다. 쓰러지면 R로 새 맵 다시 시작, M으로 시작 화면.
    // 휴식 체크포인트를 켜면 런 진행을 저장하고(RunSave), 시작 화면의 "이어하기"로 그 체크포인트부터 다시 시작한다.
    public class PlaytestBootstrap : MonoBehaviour
    {
        [Header("Stage")]
        [SerializeField] private float targetMinutes = 30f;
        [SerializeField] private bool randomSeedEachRun = true; // 끄면 아래 seed로 항상 같은 맵
        [SerializeField] private int seed = 0;
        [Tooltip("방 격자형 스테이지 (Docs/LevelDesign.md). 끄면 예전 외길 청크 스테이지")]
        [SerializeField] private bool roomGrid = true;

        private ChunkStreamer streamer;
        private float currentTargetSeconds; // 이번 런의 목표 시간 (저장해서 이어하기 때 같은 맵을 만든다)
        private bool? settingsDevMode;      // 이어하기로 모드를 바꿨다면 원래 설정값
        private GameObject player;
        private SkillHUD hud;
        private DevCheats cheats;
        private MinimapUI minimap;

        private void Start()
        {
            ShowMenu();
        }

        private void ShowMenu()
        {
            GameAudio.PlayMusic("Menu");
            gameObject.AddComponent<StartMenu>().Initialize(() => BuildWorld(null), ContinueWorld);
        }

        private void ContinueWorld()
        {
            var data = RunSave.Load();
            if (data == null || data.roomGrid != roomGrid)
            {
                BuildWorld(null); // 저장이 깨졌거나 사라졌으면 새 런
                return;
            }

            // 저장된 런의 모드로 바꾸되, 시작 화면으로 돌아오면 원래 설정으로 되돌린다 (설정이 런 모드로 덮이지 않게).
            if (data.devMode != GameSession.DevMode) settingsDevMode = GameSession.DevMode;
            GameSession.DevMode = data.devMode;
            BuildWorld(data);
        }

        // resume: 이어 할 저장 (null이면 새 런, 이전 저장은 지운다).
        private void BuildWorld(RunSave.Data resume)
        {
            GameAudio.PlayMusic("Dungeon");
            // Tools/I Blame You/Generate Sprites And Animations로 미리 생성해둔 아트 설정.
            // 아직 생성 전이면 null이며, 이 경우 각 스포너가 단색 사각형으로 대체한다.
            var artConfig = Resources.Load<LevelArtConfig>("LevelArtConfig");
            var groundTile = artConfig != null ? artConfig.groundTile : null;

            if (resume == null) RunSave.Clear();
            int runSeed = resume != null ? resume.seed : randomSeedEachRun ? Random.Range(1, 1000000) : seed;
            float targetSeconds = resume != null && resume.targetSeconds > 0f ? resume.targetSeconds : targetMinutes * 60f;
            currentTargetSeconds = targetSeconds;
            streamer = new GameObject("Stage").AddComponent<ChunkStreamer>();
            System.Func<StageEnemy, Vector3, Transform, HealthSystem> spawner = (kind, position, parent) => SpawnEnemy(kind, position, parent, artConfig);
            if (roomGrid)
            {
                var layout = RoomLayout.Generate(runSeed, targetSeconds);
                var geometries = new System.Collections.Generic.List<RoomGeometry>();
                int invalid = 0;
                foreach (var room in layout.Rooms)
                {
                    var geometry = RoomShaper.Build(runSeed, room);
                    if (!geometry.Valid) invalid++;
                    geometries.Add(geometry);
                }

                Debug.Log($"[Stage] 시드 {runSeed}, 방 {layout.Rooms.Count}개 (격자 {layout.MaxX - layout.MinX + 1}×{layout.MaxY - layout.MinY + 1}), 검증 실패 {invalid}");
                streamer.InitializeRooms(layout, geometries, groundTile, spawner);
                if (resume != null) streamer.SetCurrentRoom(resume.checkpointChunk);
            }
            else
            {
                var stage = StageGenerator.Generate(runSeed, targetSeconds);
                Debug.Log($"[Stage] 시드 {runSeed}, 청크 {stage.Chunks.Count}개, 예상 {stage.EstimatedSeconds / 60f:0.0}분");
                streamer.Initialize(stage, groundTile, spawner);
            }

            if (resume != null) streamer.ImportRecords(resume.records); // 청크를 만들기 전에: 쓰러뜨린 적, 돌파한 구역 등

            var start = resume != null ? new Vector3(resume.spawnX, resume.spawnY, 0f) : streamer.PlayerStart;
            var movement = PlayerSpawner.Spawn(start, artConfig);
            player = movement.gameObject;
            if (player.GetComponent<FallRecovery>() == null) player.AddComponent<FallRecovery>(); // 프리팹을 다시 굽기 전 대비
            if (player.GetComponent<PlayerProgression>() == null) player.AddComponent<PlayerProgression>();
            if (resume != null) RestorePlayer(resume, start); // HUD를 붙이기 전에 (특성 다시 찍기가 HUD 알림으로 쏟아지지 않게)
            streamer.SetPlayer(player.transform);
            streamer.CheckpointActivated += SaveRun;

            SetupCamera(streamer, movement);

            cheats = gameObject.AddComponent<DevCheats>();
            cheats.Initialize(player);
            hud = gameObject.AddComponent<SkillHUD>();
            hud.Initialize(player, cheats, Restart);
            if (streamer.IsRoomMode)
            {
                minimap = gameObject.AddComponent<MinimapUI>();
                minimap.Initialize(streamer, player);
            }
            StageEvents.StageName = "스테이지 1";
            if (resume != null)
            {
                hud.ResumeRun(resume.elapsed, resume.kills, resume.damageTaken, resume.arenasCleared, resume.gatesOpened);
                StageEvents.PostNotice($"이어하기: 체크포인트에서 다시 시작 (시드 {runSeed})");
            }
            else
            {
                StageEvents.PostNotice($"스테이지 1 시작 (시드 {runSeed})");
            }
        }

        // 저장된 레벨·특성·유물·체력을 새로 만든 플레이어에 되살린다.
        private void RestorePlayer(RunSave.Data data, Vector3 start)
        {
            var progression = player.GetComponent<PlayerProgression>();
            progression.RestoreProgress(data.level, data.experience, data.totalExperience, data.points, data.traitRanks);

            var relics = PlayerRelics.For(player);
            if (data.relics != null)
                foreach (int id in data.relics)
                    if (System.Enum.IsDefined(typeof(RelicId), id)) relics.Add((RelicId)id);

            var health = player.GetComponent<HealthSystem>();
            if (health != null && data.health > 0f) health.SetValues(Mathf.Min(data.health, health.Max), health.Max);
            if (health != null && data.deathSaves >= 0) health.DeathSaves = data.deathSaves; // 특성 다시 찍기로 되살아난 버티기를 저장값으로
            var recovery = player.GetComponent<FallRecovery>();
            if (data.freeFalls >= 0) recovery.FreeFalls = data.freeFalls; // 유물 Apply가 다시 채운 모래시계 횟수를 저장값으로
            recovery.SetSafePoint(start);
        }

        // 체크포인트를 처음 켰을 때 (체력·마나 회복 뒤) 런 진행을 저장한다.
        private void SaveRun(int checkpointChunk, Vector3 point)
        {
            if (streamer == null || player == null || hud == null || RunStats.Ended) return;
            var health = player.GetComponent<HealthSystem>();
            if (health == null || health.IsDead) return;

            var progression = player.GetComponent<PlayerProgression>();
            var relics = player.GetComponent<PlayerRelics>();
            var recovery = player.GetComponent<FallRecovery>();
            var relicIds = new System.Collections.Generic.List<int>();
            if (relics != null) foreach (var id in relics.Owned) relicIds.Add((int)id);

            RunSave.Save(new RunSave.Data
            {
                seed = streamer.Stage.Seed,
                targetSeconds = currentTargetSeconds,
                devMode = GameSession.DevMode,
                checkpointChunk = checkpointChunk,
                roomGrid = streamer.IsRoomMode,
                spawnX = point.x,
                spawnY = point.y,
                elapsed = hud.RunElapsed,
                progress = streamer.IsRoomMode ? StageEvents.Progress : Mathf.Clamp01(point.x / streamer.Stage.WidthTiles),
                level = progression != null ? progression.Level : 1,
                experience = progression != null ? progression.Experience : 0f,
                totalExperience = progression != null ? progression.TotalExperience : 0f,
                points = progression != null ? progression.Points : 0,
                traitRanks = progression != null ? progression.RanksSnapshot() : new int[0],
                relics = relicIds.ToArray(),
                health = health.Current,
                deathSaves = health.DeathSaves,
                freeFalls = recovery != null ? recovery.FreeFalls : -1,
                kills = RunStats.Kills,
                damageTaken = RunStats.DamageTaken,
                arenasCleared = RunStats.ArenasCleared,
                gatesOpened = RunStats.GatesOpened,
                records = streamer.ExportRecords()
            });
        }

        // 런 도중 앱을 꺼도: 이어 할 때 시간이 체크포인트 시점으로 되감기지 않게 하고, 모드 설정은 원래대로 남긴다.
        private void OnApplicationQuit()
        {
            if (hud != null && !RunStats.Ended) RunSave.UpdateElapsed(hud.RunElapsed);
            if (settingsDevMode.HasValue)
            {
                GameSession.DevMode = settingsDevMode.Value;
                SaveData.SaveSettings();
            }
        }

        private static HealthSystem SpawnEnemy(StageEnemy kind, Vector3 position, Transform parent, LevelArtConfig artConfig)
        {
            EnemyController enemy;
            switch (kind)
            {
                case StageEnemy.Goblin: enemy = GoblinSpawner.SpawnGoblin(position, artConfig); break;
                case StageEnemy.GoblinArcher: enemy = GoblinArcherSpawner.Spawn(position, artConfig); break;
                case StageEnemy.GoblinSoldier: enemy = GoblinSpawner.SpawnSoldier(position, artConfig); break;
                case StageEnemy.MotherSlime: enemy = MotherSlimeSpawner.Spawn(position, artConfig); break;
                case StageEnemy.GoblinWarrior: enemy = GoblinWarriorSpawner.Spawn(position, artConfig); break;
                case StageEnemy.SlimeKing: enemy = SlimeKingSpawner.Spawn(position, artConfig); break;
                case StageEnemy.SlimeKnightFire: enemy = SlimeKnightSpawner.Spawn(position, Element.Fire, artConfig); break;
                case StageEnemy.SlimeKnightWater: enemy = SlimeKnightSpawner.Spawn(position, Element.Water, artConfig); break;
                default: enemy = SlimeSpawner.Spawn(position, artConfig); break;
            }

            if (enemy == null) return null;
            enemy.transform.SetParent(parent, true);
            return enemy.GetComponent<HealthSystem>();
        }

        // 지금 런을 치우고 다시 시작한다. 씬을 다시 불러오지 않아 빌드 설정에 씬을 등록하지 않아도 된다.
        private void Restart(bool backToMenu)
        {
            StartCoroutine(RestartRoutine(backToMenu));
        }

        private IEnumerator RestartRoutine(bool backToMenu)
        {
            if (hud != null) Destroy(hud);
            if (cheats != null) Destroy(cheats);
            if (minimap != null) Destroy(minimap);
            if (player != null) Destroy(player);
            if (streamer != null) Destroy(streamer.gameObject); // 청크, 적, 함정이 모두 이 아래에 있다
            foreach (var projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);

            yield return null; // Destroy가 끝난 다음 프레임에 새로 만든다

            if (settingsDevMode.HasValue && backToMenu)
            {
                GameSession.DevMode = settingsDevMode.Value;
                SaveData.SaveSettings();
                settingsDevMode = null;
            }

            if (backToMenu) ShowMenu();
            else BuildWorld(null);
        }

        // 플레이어를 쫓아다니는 카메라를 준비하고, 시야를 스테이지 안으로 제한한다 (전투 구역에서는 ArenaZone이 잠깐 좁힌다).
        private void SetupCamera(ChunkStreamer stageStreamer, PlayerMovement playerMovement)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cameraObject.AddComponent<Camera>();
                if (FindAnyObjectByType<AudioListener>() == null) cameraObject.AddComponent<AudioListener>(); // GameAudio가 이미 둔 경우 중복 방지
                cam.orthographicSize = 6f;
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.165f, 0.13f, 0.31f); // 팔레트 night (배경 그림이 없을 때)
            var background = ParallaxBackground.Attach(cam, stageStreamer.WorldBounds);
            background.SetRoom(stageStreamer.IsRoomMode ? stageStreamer.CurrentRoom : -1); // 첫 방은 이미 들어온 뒤라 지금 방으로 맞춘다
            if (stageStreamer.IsRoomMode)
            {
                stageStreamer.RoomEntered -= background.SetRoom;
                stageStreamer.RoomEntered += background.SetRoom;
            }

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

            follow.SetBounds(stageStreamer.CameraBounds); // 방 격자 모드에서는 지금 방 (방을 옮길 때마다 스트리머가 바꾼다)
            follow.SetTarget(playerMovement.transform);
        }
    }
}
