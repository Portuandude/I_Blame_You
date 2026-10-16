using System.Collections;
using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 빈 씬에 이 컴포넌트 하나만 두고 Play를 누르면: 시작 화면(개발자 모드 선택)을 띄우고, 시작하면
    // 챕터 맵의 모든 방을 만들고(바닥/벽/방 타입별 적), 이웃한 방끼리 문으로 이어 준 뒤, 시작 방에 플레이어와 카메라를 준비한다.
    // 쓰러지면 R로 같은 모드 다시 시작, M으로 시작 화면.
    public class PlaytestBootstrap : MonoBehaviour
    {
        [Header("Chapter Map")]
        [SerializeField] private int mainPathLength = 5;
        [SerializeField] private int extraRoomCount = 3;
        [SerializeField] private int seed = 0;

        [Header("Layout")]
        [SerializeField] private Vector2 roomSizeUnits = new Vector2(128f, 72f); // 기존 16x9의 8배 (64x36의 2배)
        [SerializeField] private float roomSpacingUnits = 160f; // roomSize와 같은 비율로 확대
        [SerializeField] private bool spawnFloatingPlatforms = false; // 일단 비활성화, 필요해지면 true로

        private enum EnemyKind
        {
            Slime,
            Goblin,
            GoblinArcher,
            GoblinSoldier,
            MotherSlime,
            GoblinWarrior
        }

        // 방 타입별 테스트 배치 (방 중앙 기준 X 오프셋과 종류). 보스는 아직 없어 정예 조합으로 대신한다.
        private static readonly (float offsetX, EnemyKind kind)[] StartRoomEnemies =
            { (-14f, EnemyKind.Slime), (14f, EnemyKind.Slime) };
        private static readonly (float, EnemyKind)[][] NormalRoomSets =
        {
            new[] { (-16f, EnemyKind.Slime), (12f, EnemyKind.Goblin), (20f, EnemyKind.Slime) },
            new[] { (-14f, EnemyKind.Goblin), (14f, EnemyKind.GoblinArcher) },
            new[] { (-18f, EnemyKind.GoblinSoldier), (10f, EnemyKind.Goblin), (22f, EnemyKind.GoblinArcher) },
            new[] { (-12f, EnemyKind.Slime), (12f, EnemyKind.GoblinSoldier) },
        };
        private static readonly (float, EnemyKind)[] EliteRoomA = { (14f, EnemyKind.MotherSlime), (-14f, EnemyKind.Slime) };
        private static readonly (float, EnemyKind)[] EliteRoomB = { (14f, EnemyKind.GoblinWarrior), (-14f, EnemyKind.Goblin) };
        private static readonly (float, EnemyKind)[] BossRoom =
            { (-16f, EnemyKind.MotherSlime), (16f, EnemyKind.GoblinWarrior), (24f, EnemyKind.GoblinArcher) };
        private static readonly (float, EnemyKind)[] PuzzleRoom = { (16f, EnemyKind.GoblinArcher) };

        private Transform mapRoot;
        private RoomManager manager;
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

            var map = RoomGenerator.Generate(mainPathLength, extraRoomCount, seed);
            mapRoot = new GameObject("ChapterMap").transform;
            manager = new GameObject("RoomManager").AddComponent<RoomManager>();

            RoomInstance startRoom = null;
            int normalIndex = 0;
            int eliteIndex = 0;
            foreach (var kvp in map.Rooms)
            {
                var worldPosition = new Vector3(kvp.Key.x * roomSpacingUnits, kvp.Key.y * roomSpacingUnits, 0f);
                var room = RoomBuilder.CreateRoom(mapRoot, kvp.Value, roomSizeUnits, worldPosition);

                int roomSeed = seed + kvp.Key.x * 31 + kvp.Key.y * 17;
                PlatformSpawner.BuildRoomGeometry(room.transform, roomSizeUnits, roomSeed, groundTile, spawnFloatingPlatforms);
                SpawnEnemies(room, PickEnemies(kvp.Value.Type, ref normalIndex, ref eliteIndex), artConfig);

                manager.Register(room);
                if (kvp.Key == map.StartPosition) startRoom = room;
            }

            manager.BuildDoors();

            // 바닥 기준 상대 높이로 스폰해서, 방 크기가 바뀌어도 항상 바닥 바로 위에서 시작한다.
            var playerSpawn = new Vector3(startRoom.Center.x, startRoom.Center.y - roomSizeUnits.y / 2f + 2f, 0f);
            var movement = PlayerSpawner.Spawn(playerSpawn, artConfig);
            player = movement.gameObject;

            SetupCamera(manager, startRoom, movement);

            cheats = gameObject.AddComponent<DevCheats>();
            cheats.Initialize(player, manager);
            hud = gameObject.AddComponent<SkillHUD>();
            hud.Initialize(player, cheats, Restart);
        }

        private static (float, EnemyKind)[] PickEnemies(RoomType type, ref int normalIndex, ref int eliteIndex)
        {
            switch (type)
            {
                case RoomType.Start:
                    return StartRoomEnemies;
                case RoomType.Elite:
                    return eliteIndex++ % 2 == 0 ? EliteRoomA : EliteRoomB;
                case RoomType.MiniBoss:
                    return eliteIndex++ % 2 == 0 ? EliteRoomB : EliteRoomA;
                case RoomType.ChapterBoss:
                    return BossRoom;
                case RoomType.Puzzle:
                    return PuzzleRoom;
                default:
                    return NormalRoomSets[normalIndex++ % NormalRoomSets.Length];
            }
        }

        private void SpawnEnemies(RoomInstance room, (float offsetX, EnemyKind kind)[] enemies, LevelArtConfig artConfig)
        {
            float spawnY = room.Center.y - roomSizeUnits.y / 2f + 2f;
            foreach (var (offsetX, kind) in enemies)
            {
                var position = new Vector3(room.Center.x + offsetX, spawnY, 0f);
                var enemy = SpawnEnemy(kind, position, artConfig);
                enemy.transform.SetParent(room.transform, true);
                room.RegisterEnemy(enemy.GetComponent<HealthSystem>());
            }
        }

        private static EnemyController SpawnEnemy(EnemyKind kind, Vector3 position, LevelArtConfig artConfig)
        {
            switch (kind)
            {
                case EnemyKind.Goblin: return GoblinSpawner.SpawnGoblin(position, artConfig);
                case EnemyKind.GoblinArcher: return GoblinArcherSpawner.Spawn(position, artConfig);
                case EnemyKind.GoblinSoldier: return GoblinSpawner.SpawnSoldier(position, artConfig);
                case EnemyKind.MotherSlime: return MotherSlimeSpawner.Spawn(position, artConfig);
                case EnemyKind.GoblinWarrior: return GoblinWarriorSpawner.Spawn(position, artConfig);
                default: return SlimeSpawner.Spawn(position, artConfig);
            }
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
            if (manager != null) Destroy(manager.gameObject);
            if (mapRoot != null) Destroy(mapRoot.gameObject);
            foreach (var projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);

            yield return null; // Destroy가 끝난 다음 프레임에 새로 만든다

            if (backToMenu) ShowMenu();
            else BuildWorld();
        }

        // 플레이어를 쫓아다니는 카메라를 준비하고, 시야를 현재 방 안으로 제한한다 (방을 옮기면 RoomManager가 범위를 바꾼다).
        private void SetupCamera(RoomManager roomManager, RoomInstance startRoom, PlayerMovement playerMovement)
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

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

            follow.SetBounds(startRoom.WorldBounds);
            follow.SetTarget(playerMovement.transform);
            roomManager.Begin(startRoom, follow);
        }
    }
}
