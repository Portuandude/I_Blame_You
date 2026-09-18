using UnityEngine;

namespace IBlameYou.Systems
{
    // 방 하나 안에 실제로 밟고 다닐 수 있는 바닥/플랫폼 콜라이더를 절차적으로 배치한다.
    public static class PlatformSpawner
    {
        private const float WallThickness = 1f;

        public static void BuildRoomGeometry(Transform parent, Vector2 roomSize, int seed, Sprite groundTile = null, bool includeFloatingPlatforms = true)
        {
            BuildBorders(parent, roomSize, groundTile);
            BuildFloor(parent, roomSize, groundTile);

            if (includeFloatingPlatforms)
            {
                BuildFloatingPlatforms(parent, roomSize, seed, groundTile);
            }
        }

        // 방 좌우에 벽을 세워서 플레이어가 방 경계 밖으로 걸어나가 맵 밖으로 떨어지는 것을 막는다.
        private static void BuildBorders(Transform parent, Vector2 roomSize, Sprite groundTile)
        {
            float x = roomSize.x / 2f - WallThickness / 2f;
            CreatePlatform(parent, "WallLeft", new Vector2(-x, 0f), new Vector2(WallThickness, roomSize.y), groundTile);
            CreatePlatform(parent, "WallRight", new Vector2(x, 0f), new Vector2(WallThickness, roomSize.y), groundTile);
        }

        private static void BuildFloor(Transform parent, Vector2 roomSize, Sprite groundTile)
        {
            CreatePlatform(parent, "Floor", new Vector2(0f, -roomSize.y / 2f + 0.5f), new Vector2(roomSize.x, 1f), groundTile);
        }

        private static void BuildFloatingPlatforms(Transform parent, Vector2 roomSize, int seed, Sprite groundTile)
        {
            var rng = new System.Random(seed);
            int platformCount = rng.Next(2, 5);

            for (int i = 0; i < platformCount; i++)
            {
                float width = Mathf.Lerp(2f, 4f, (float)rng.NextDouble());
                float x = Mathf.Lerp(-roomSize.x / 2f + width, roomSize.x / 2f - width, (float)rng.NextDouble());
                float y = Mathf.Lerp(-roomSize.y / 2f + 2.5f, roomSize.y / 2f - 1f, (float)rng.NextDouble());

                CreatePlatform(parent, $"Platform_{i}", new Vector2(x, y), new Vector2(width, 0.5f), groundTile);
            }
        }

        private static void CreatePlatform(Transform parent, string name, Vector2 localPosition, Vector2 size, Sprite groundTile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            int groundLayer = LayerMask.NameToLayer("Ground");
            go.layer = groundLayer >= 0 ? groundLayer : 0;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            var renderer = go.AddComponent<SpriteRenderer>();

            if (groundTile != null)
            {
                // Tiled 모드는 렌더러의 size(월드 단위)로 반복 폭/높이를 정하므로 transform은 스케일하지 않는다.
                collider.size = size;
                renderer.sprite = groundTile;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = size;
            }
            else
            {
                // 아직 타일 아트가 준비되지 않았을 때의 대체용 단색 사각형.
                collider.size = Vector2.one;
                go.transform.localScale = new Vector3(size.x, size.y, 1f);
                renderer.sprite = SolidSpriteFactory.CreateSquare();
                renderer.color = new Color(0.4f, 0.4f, 0.45f);
            }
        }
    }
}
