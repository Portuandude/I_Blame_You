using System.Collections;
using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 빈 씬에 이 컴포넌트 하나만 두고 Play를 누르면: 시작 화면(개발자 모드 선택)을 띄우고, 시작하면
    // 시드로 스테이지 1(청크를 가로로 이어 붙인 약 15분 분량의 맵)을 만들고, 시작 지점에 플레이어와 카메라를 준비한다.
    // 청크는 ChunkStreamer가 플레이어 주변만 만들고 멀어지면 치운다. 쓰러지면 R로 새 맵 다시 시작, M으로 시작 화면.
    // 런 진행 저장(RunSave)·이어하기: 체크포인트 제거(2026-11-10 사람 결정)로 지금은 저장하는 시점이 없다. 스테이지가 하나뿐이라
    // "스테이지 시작 지점에서 이어하기"는 새 런과 다르지 않기 때문. 저장·복원 코드(SaveRun·ContinueWorld)는 스테이지 2가 생기면
    // 스테이지 전환 때 저장하는 용도로 남긴다.
    public class PlaytestBootstrap : MonoBehaviour
    {
        [Header("Stage")]
        [SerializeField] private float targetMinutes = 20f; // 스테이지 목표 15~30분의 기본값 (2026-11-12 사람 결정 15~30분, 넓은 방으로 예상 약 15~19분)
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
        private FeatureTestTools featureTools;

        private void Start()
        {
            Net.NetSession.ClientStarted += OnOnlineClientStarted;
            Net.NetSession.ClientEnded += OnOnlineClientEnded;
            ShowMenu();
        }

        private void OnDestroy()
        {
            Net.NetSession.ClientStarted -= OnOnlineClientStarted;
            Net.NetSession.ClientEnded -= OnOnlineClientEnded;
        }

        // 온라인 2인에 친구(P2)로 들어감: 이 컴퓨터는 게임을 돌리지 않고 호스트 화면을 그린다 (Net/NetClientView). 메뉴를 치운다.
        private void OnOnlineClientStarted()
        {
            foreach (var menu in GetComponents<StartMenu>()) Destroy(menu);
            foreach (var city in GetComponents<CityMenu>()) Destroy(city);
            foreach (var intro in GetComponents<StoryIntro>()) Destroy(intro);
            GameAudio.StopMusic(); // 곡은 호스트가 보내 준다
        }

        // 연결이 끝나면 시작 화면으로 (이유는 온라인 창에 보인다)
        private void OnOnlineClientEnded(string reason)
        {
            if (GetComponent<StartMenu>() == null) ShowMenu();
        }

        private void ShowMenu()
        {
            GameAudio.PlayMusic("Menu");
            gameObject.AddComponent<StartMenu>().Initialize(() => BuildWorld(null), ContinueWorld, ShowCity);
        }

        // 런 사이 도시 메뉴 (CityMenu): 출발 → 새 판, Esc → 시작 화면. 일반 판(개발자 모드 포함)만 거친다. 기능 테스트는 바로 시험 맵.
        private void ShowCity() => ShowCity(CityArrival.FromStart);

        private void ShowCity(CityArrival arrival)
        {
            gameObject.AddComponent<CityMenu>().Initialize(() => BuildWorld(null), () => Restart(true), arrival);
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
            // 이어하기면 저장된 방 깊이에 맞는 곡 (깊이 0.5+ → 후반 Dungeon2, ArenaZone.StageMusicFor). 새 런은 Dungeon.
            GameAudio.PlayMusic(resume != null ? ArenaZone.StageMusicFor(resume.progress) : "Dungeon");
            // Tools/I Blame You/Generate Sprites And Animations로 미리 생성해둔 아트 설정.
            // 아직 생성 전이면 null이며, 이 경우 각 스포너가 단색 사각형으로 대체한다.
            var artConfig = Resources.Load<LevelArtConfig>("LevelArtConfig");
            var groundTile = artConfig != null ? artConfig.groundTile : null;

            bool featureTest = GameSession.FeatureTest && resume == null;
            if (resume == null && !featureTest) RunSave.Clear(); // 기능 테스트는 이어하던 런을 지우지 않는다
            int runSeed = resume != null ? resume.seed : randomSeedEachRun ? Random.Range(1, 1000000) : seed;
            float targetSeconds = resume != null && resume.targetSeconds > 0f ? resume.targetSeconds : targetMinutes * 60f;
            currentTargetSeconds = targetSeconds;
            // 스테이지 번호: 이어하기면 저장값, 새 판이면 1 (개발자 모드는 시작 화면에서 고른 스테이지). 기능 테스트는 1
            int stageNumber = StageRoster.Clamp(resume != null ? resume.stage : featureTest || !GameSession.DevMode ? 1 : GameSession.DevStartStage);
            GameSession.Stage = stageNumber;
            StageEvents.TargetSeconds = targetSeconds;
            StageEvents.RoomCount = 0;
            StageEvents.RoomDistance = -1; // 방 격자면 첫 방에 들어갈 때 채운다 (ChunkStreamer)
            StageEvents.FarthestDistance = -1;
            streamer = new GameObject("Stage").AddComponent<ChunkStreamer>();
            System.Func<StageEnemy, Vector3, Transform, HealthSystem> spawner = (kind, position, parent) => SpawnEnemy(kind, position, parent, artConfig);
            StageData featureStage = null;
            if (featureTest)
            {
                // 기능 테스트 모드: 적 없는 시험용 외길 (FeatureTestStage). 몬스터·유물은 FeatureTestTools 키로 부른다
                runSeed = FeatureTestStage.Seed;
                featureStage = FeatureTestStage.Build();
                streamer.Initialize(featureStage, groundTile, spawner);
            }
            else if (roomGrid)
            {
                var layout = RoomLayout.Generate(runSeed, targetSeconds, stageNumber);
                StageEvents.RoomCount = layout.Rooms.Count;
                var geometries = new System.Collections.Generic.List<RoomGeometry>();
                int invalid = 0;
                foreach (var room in layout.Rooms)
                {
                    var geometry = RoomShaper.Build(runSeed, room);
                    if (!geometry.Valid) invalid++;
                    geometries.Add(geometry);
                }

                Debug.Log($"[Stage] 스테이지 {stageNumber}, 시드 {runSeed}, 방 {layout.Rooms.Count}개 (격자 {layout.MaxX - layout.MinX + 1}×{layout.MaxY - layout.MinY + 1}), 검증 실패 {invalid}");
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
            ApplyLearnedSpells(featureTest);
            if (!featureTest) ApplyTowerBonuses(); // 수정 탑 영구 능력치 (기능 테스트 제외). 이어하기 체력 복원보다 먼저
            if (resume != null) RestorePlayer(resume, start); // HUD를 붙이기 전에 (특성 다시 찍기가 HUD 알림으로 쏟아지지 않게)
            streamer.SetPlayer(player.transform);

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
            StageEvents.StageName = featureTest ? "기능 테스트" : StageTitle(stageNumber);
            if (featureTest)
            {
                featureTools = gameObject.AddComponent<FeatureTestTools>();
                featureTools.Initialize(player, streamer, featureStage, spawner, Restart);
                StageEvents.PostNotice("기능 테스트 모드 — F6 도구 목록");
            }
            else if (resume != null)
            {
                hud.ResumeRun(resume.elapsed, resume.kills, resume.damageTaken, resume.arenasCleared, resume.gatesOpened);
                StageEvents.PostNotice($"이어하기: 저장한 곳에서 다시 시작 (시드 {runSeed})");
            }
            else
            {
                StageEvents.PostNotice($"{StageTitle(stageNumber)} 시작 (시드 {runSeed})");
            }
        }

        private static string StageTitle(int stage) => StageRoster.IsForest(stage) ? $"스테이지 {stage} (숲)" : $"스테이지 {stage}";

        // 아카데미에서 배운 마법을 B의 마법 목록에 (SpellBook.Learn → 다이얼 2단계·숫자줄). 기능 테스트는 마법 다이얼을 시험하도록 모두 배운 채로.
        private static void ApplyLearnedSpells(bool featureTest)
        {
            SpellBook.ResetLearned();
            foreach (var spell in SpellBook.Academy)
                if (featureTest || SaveData.SpellLearned(spell.Id, GameSession.DevMode)) SpellBook.Learn(spell);
        }

        // 수정 탑에서 산 영구 능력치를 새 플레이어에 더한다 (Docs/CityDesign.md 4절, CrystalTowerRule). 모드별 저장.
        private void ApplyTowerBonuses()
        {
            bool dev = GameSession.DevMode;
            var health = player.GetComponent<HealthSystem>();
            var mana = player.GetComponent<ManaSystem>();
            var caster = player.GetComponent<SpellCaster>();
            float hp = CrystalTowerRule.Bonus(TowerStat.MaxHealth, SaveData.TowerLevel(TowerStat.MaxHealth, dev));
            float mp = CrystalTowerRule.Bonus(TowerStat.MaxMana, SaveData.TowerLevel(TowerStat.MaxMana, dev));
            if (health != null && hp > 0f) health.AddMax(hp, true);
            if (mana != null && mp > 0f) mana.AddMax(mp, true);
            if (mana != null) mana.RegenMultiplier += CrystalTowerRule.Bonus(TowerStat.ManaRegen, SaveData.TowerLevel(TowerStat.ManaRegen, dev));
            if (caster != null) caster.CircleEase = CrystalTowerRule.Bonus(TowerStat.CircleEase, SaveData.TowerLevel(TowerStat.CircleEase, dev));
            // 순간이동 강화·점프력 강화 (2026-11-24, OpenDecisions G1). 유물 배율(종이비행기)과는 곱한다.
            var teleport = player.GetComponent<TeleportSkill>();
            if (teleport != null) teleport.CostMultiplier *= CrystalTowerRule.TeleportCostMultiplier(SaveData.TowerLevel(TowerStat.Teleport, dev));
            var movement = player.GetComponent<PlayerMovement>();
            if (movement != null) movement.JumpMultiplier = CrystalTowerRule.JumpMultiplier(SaveData.TowerLevel(TowerStat.Jump, dev));
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

        // 런 진행 저장. 체크포인트 제거(2026-11-10) 뒤로는 부르는 곳이 없다 — 스테이지 전환 저장(스테이지 2)에서 다시 쓴다.
        // checkpointChunk: 다시 시작할 방 번호, point: 다시 시작할 위치.
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
                stage = GameSession.Stage,
                roomGrid = streamer.IsRoomMode,
                spawnX = point.x,
                spawnY = point.y,
                elapsed = hud.RunElapsed,
                progress = streamer.IsRoomMode ? StageEvents.Depth : Mathf.Clamp01(point.x / streamer.Stage.WidthTiles), // 깊이 (이어할 때 곡 고르기)
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

        // 런 도중 앱을 꺼도: 이어 할 때 시간이 저장 시점으로 되감기지 않게 하고, 모드 설정은 원래대로 남긴다.
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
                case StageEnemy.TreasureGoblin: enemy = TreasureGoblinSpawner.Spawn(position, artConfig); break;
                case StageEnemy.PrismJelly: enemy = PrismJellySpawner.Spawn(position, artConfig); break;
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
            if (featureTools != null) Destroy(featureTools);
            if (player != null) Destroy(player);
            if (streamer != null) Destroy(streamer.gameObject); // 청크, 적, 함정이 모두 이 아래에 있다
            foreach (var projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            // 땅에 남는 마법(불꽃 벽·땅 가시·물안개)도 씬 뿌리에 있어 새 판까지 남지 않게 치운다 (지운 플레이어 능력치로 계속 때리던 것)
            foreach (var wall in FindObjectsByType<FireWall>(FindObjectsSortMode.None)) Destroy(wall.gameObject);
            foreach (var spike in FindObjectsByType<GroundSpike>(FindObjectsSortMode.None)) Destroy(spike.gameObject);
            foreach (var mist in FindObjectsByType<MistZone>(FindObjectsSortMode.None)) Destroy(mist.gameObject);

            yield return null; // Destroy가 끝난 다음 프레임에 새로 만든다

            if (settingsDevMode.HasValue && backToMenu)
            {
                GameSession.DevMode = settingsDevMode.Value;
                SaveData.SaveSettings();
                settingsDevMode = null;
            }

            if (backToMenu) ShowMenu();
            else if (!GameSession.FeatureTest)
            {
                RunSave.Clear(); // 포기·끝난 판은 이어하기로 되살리지 않는다 (예전 다시 시작의 BuildWorld(null)와 같음, 리뷰 MEDIUM)
                ShowCity(CityArrival.AfterRun); // 일반 판이 끝나거나 포기하면 도시로 (CityDesign.md 2절)
            }
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
