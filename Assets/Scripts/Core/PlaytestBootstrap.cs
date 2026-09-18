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
        [SerializeField] private Vector2 roomSize = new Vector2(64f, 36f); // 기존 16x9의 4배
        [SerializeField] private float roomSpacing = 80f; // roomSize와 같은 비율로 확대
        [SerializeField] private bool spawnFloatingPlatforms = false; // 일단 비활성화, 필요해지면 true로

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
                var worldPosition = new Vector3(kvp.Key.x * roomSpacing, kvp.Key.y * roomSpacing, 0f);
                RoomBuilder.BuildRoomBackground(mapRoot, kvp.Value, roomSize, worldPosition);

                if (kvp.Key == map.StartPosition)
                {
                    var geometryRoot = new GameObject("StartRoomGeometry");
                    geometryRoot.transform.SetParent(mapRoot, false);
                    geometryRoot.transform.position = worldPosition;
                    PlatformSpawner.BuildRoomGeometry(geometryRoot.transform, roomSize, seed, groundTile, spawnFloatingPlatforms);

                    // 바닥 기준 상대 높이로 스폰해서, 방 크기가 바뀌어도 항상 바닥 바로 위에서 시작한다.
                    float spawnY = worldPosition.y - roomSize.y / 2f + 2f;
                    PlayerSpawner.Spawn(new Vector3(worldPosition.x, spawnY, 0f), artConfig);
                    SlimeSpawner.Spawn(new Vector3(worldPosition.x + 3f, spawnY, 0f), artConfig);
                }
            }
        }
    }
}
