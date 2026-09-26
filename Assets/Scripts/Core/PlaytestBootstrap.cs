using IBlameYou.Enemies;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 빈 씬에 이 컴포넌트 하나만 두고 Play를 누르면: 챕터 맵을 생성해 방 배치를 시각화하고,
    // 시작 방에는 실제로 밟을 수 있는 바닥/플랫폼과 플레이어를 배치해 이동을 바로 테스트할 수 있게 한다.
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
        [SerializeField] private float[] slimeSpawnOffsetsX = { 10f, -14f, 24f }; // 시작 방 중앙 기준 X 오프셋, 원소 수 = 슬라임 수

        private void Start()
        {
            // Tools/I Blame You/Generate Sprites And Animations로 미리 생성해둔 아트 설정.
            // 아직 생성 전이면 null이며, 이 경우 각 스포너가 단색 사각형으로 대체한다.
            var artConfig = Resources.Load<LevelArtConfig>("LevelArtConfig");
            var groundTile = artConfig != null ? artConfig.groundTile : null;

            var map = RoomGenerator.Generate(mainPathLength, extraRoomCount, seed);
            var mapRoot = new GameObject("ChapterMap").transform;

            foreach (var kvp in map.Rooms)
            {
                var worldPosition = new Vector3(kvp.Key.x * roomSpacingUnits, kvp.Key.y * roomSpacingUnits, 0f);
                RoomBuilder.BuildRoomBackground(mapRoot, kvp.Value, roomSizeUnits, worldPosition);

                if (kvp.Key == map.StartPosition)
                {
                    var geometryRoot = new GameObject("StartRoomGeometry");
                    geometryRoot.transform.SetParent(mapRoot, false);
                    geometryRoot.transform.position = worldPosition;
                    PlatformSpawner.BuildRoomGeometry(geometryRoot.transform, roomSizeUnits, seed, groundTile, spawnFloatingPlatforms);

                    // 바닥 기준 상대 높이로 스폰해서, 방 크기가 바뀌어도 항상 바닥 바로 위에서 시작한다.
                    float spawnY = worldPosition.y - roomSizeUnits.y / 2f + 2f;
                    PlayerSpawner.Spawn(new Vector3(worldPosition.x, spawnY, 0f), artConfig);

                    foreach (float offsetX in slimeSpawnOffsetsX)
                    {
                        SlimeSpawner.Spawn(new Vector3(worldPosition.x + offsetX, spawnY, 0f), artConfig);
                    }
                }
            }

            var startRoomCenter = new Vector3(map.StartPosition.x * roomSpacingUnits, map.StartPosition.y * roomSpacingUnits, 0f);
            SetupCamera(startRoomCenter);
        }

        // 플레이어를 쫓아다니는 카메라를 준비하고, 시야가 시작 방 밖으로 나가지 않게 제한한다.
        private void SetupCamera(Vector3 startRoomCenter)
        {
            var player = FindFirstObjectByType<PlayerMovement>();
            if (player == null) return;

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

            var roomCenter = (Vector2)startRoomCenter;
            follow.SetBounds(new Rect(roomCenter - roomSizeUnits / 2f, roomSizeUnits));
            follow.SetTarget(player.transform);
        }
    }
}
