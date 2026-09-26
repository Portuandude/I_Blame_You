using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 빈 씬에 이 컴포넌트 하나만 두고 Play를 누르면: 챕터 맵의 모든 방을 만들고(바닥/벽/슬라임),
    // 이웃한 방끼리 문으로 이어 준 뒤, 시작 방에 플레이어와 카메라를 준비한다.
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

        [Header("Enemies")]
        // 방 중앙 기준 X 오프셋. 원소 수 = 방마다 스폰할 슬라임 수 (테스트용으로 방마다 2마리).
        // (씬에 저장된 옛 값이 코드 기본값을 덮어쓰지 않도록 이전 slimeSpawnOffsetsX에서 이름을 바꿈)
        [SerializeField] private float[] slimeOffsetsXPerRoom = { -16f, 16f };

        private void Start()
        {
            // Tools/I Blame You/Generate Sprites And Animations로 미리 생성해둔 아트 설정.
            // 아직 생성 전이면 null이며, 이 경우 각 스포너가 단색 사각형으로 대체한다.
            var artConfig = Resources.Load<LevelArtConfig>("LevelArtConfig");
            var groundTile = artConfig != null ? artConfig.groundTile : null;

            var map = RoomGenerator.Generate(mainPathLength, extraRoomCount, seed);
            var mapRoot = new GameObject("ChapterMap").transform;
            var manager = new GameObject("RoomManager").AddComponent<RoomManager>();

            RoomInstance startRoom = null;
            foreach (var kvp in map.Rooms)
            {
                var worldPosition = new Vector3(kvp.Key.x * roomSpacingUnits, kvp.Key.y * roomSpacingUnits, 0f);
                var room = RoomBuilder.CreateRoom(mapRoot, kvp.Value, roomSizeUnits, worldPosition);

                int roomSeed = seed + kvp.Key.x * 31 + kvp.Key.y * 17;
                PlatformSpawner.BuildRoomGeometry(room.transform, roomSizeUnits, roomSeed, groundTile, spawnFloatingPlatforms);
                SpawnSlimes(room, artConfig);

                manager.Register(room);
                if (kvp.Key == map.StartPosition) startRoom = room;
            }

            manager.BuildDoors();

            // 바닥 기준 상대 높이로 스폰해서, 방 크기가 바뀌어도 항상 바닥 바로 위에서 시작한다.
            var playerSpawn = new Vector3(startRoom.Center.x, startRoom.Center.y - roomSizeUnits.y / 2f + 2f, 0f);
            var player = PlayerSpawner.Spawn(playerSpawn, artConfig);

            SetupCamera(manager, startRoom, player);
        }

        private void SpawnSlimes(RoomInstance room, LevelArtConfig artConfig)
        {
            float spawnY = room.Center.y - roomSizeUnits.y / 2f + 2f;
            foreach (float offsetX in slimeOffsetsXPerRoom)
            {
                var slime = SlimeSpawner.Spawn(new Vector3(room.Center.x + offsetX, spawnY, 0f), artConfig);
                slime.transform.SetParent(room.transform, true);
            }
        }

        // 플레이어를 쫓아다니는 카메라를 준비하고, 시야를 현재 방 안으로 제한한다 (방을 옮기면 RoomManager가 범위를 바꾼다).
        private void SetupCamera(RoomManager manager, RoomInstance startRoom, PlayerMovement player)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cam.orthographicSize = 6f;
            }

            cam.orthographic = true;

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

            follow.SetBounds(startRoom.WorldBounds);
            follow.SetTarget(player.transform);
            manager.Begin(startRoom, follow);
        }
    }
}
