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
    public class PlaytestBootstrap : MonoBehaviour
    {
        [Header("Stage")]
        [SerializeField] private float targetMinutes = 30f;
        [SerializeField] private bool randomSeedEachRun = true; // 끄면 아래 seed로 항상 같은 맵
        [SerializeField] private int seed = 0;

        private ChunkStreamer streamer;
        private GameObject player;
        private SkillHUD hud;
        private DevCheats cheats;

        private void Start()
        {
            ShowMenu();
        }

        private void ShowMenu()
        {
            GameAudio.PlayMusic("Menu");
            gameObject.AddComponent<StartMenu>().Initialize(BuildWorld);
        }

        private void BuildWorld()
        {
            GameAudio.PlayMusic("Dungeon");
            // Tools/I Blame You/Generate Sprites And Animations로 미리 생성해둔 아트 설정.
            // 아직 생성 전이면 null이며, 이 경우 각 스포너가 단색 사각형으로 대체한다.
            var artConfig = Resources.Load<LevelArtConfig>("LevelArtConfig");
            var groundTile = artConfig != null ? artConfig.groundTile : null;

            int runSeed = randomSeedEachRun ? Random.Range(1, 1000000) : seed;
            var stage = StageGenerator.Generate(runSeed, targetMinutes * 60f);
            Debug.Log($"[Stage] 시드 {runSeed}, 청크 {stage.Chunks.Count}개, 예상 {stage.EstimatedSeconds / 60f:0.0}분");

            streamer = new GameObject("Stage").AddComponent<ChunkStreamer>();
            streamer.Initialize(stage, groundTile, (kind, position, parent) => SpawnEnemy(kind, position, parent, artConfig));

            var movement = PlayerSpawner.Spawn(streamer.PlayerStart, artConfig);
            player = movement.gameObject;
            if (player.GetComponent<FallRecovery>() == null) player.AddComponent<FallRecovery>(); // 프리팹을 다시 굽기 전 대비
            streamer.SetPlayer(player.transform);

            SetupCamera(streamer, movement);

            cheats = gameObject.AddComponent<DevCheats>();
            cheats.Initialize(player);
            hud = gameObject.AddComponent<SkillHUD>();
            hud.Initialize(player, cheats, Restart);
            StageEvents.StageName = "스테이지 1";
            StageEvents.PostNotice($"스테이지 1 시작 (시드 {runSeed})");
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
            if (player != null) Destroy(player);
            if (streamer != null) Destroy(streamer.gameObject); // 청크, 적, 함정이 모두 이 아래에 있다
            foreach (var projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);

            yield return null; // Destroy가 끝난 다음 프레임에 새로 만든다

            if (backToMenu) ShowMenu();
            else BuildWorld();
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
            cam.backgroundColor = new Color(0.13f, 0.15f, 0.2f);

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

            follow.SetBounds(stageStreamer.WorldBounds);
            follow.SetTarget(playerMovement.transform);
        }
    }
}
