using UnityEngine;

namespace IBlameYou.Systems
{
    // 방 하나 안에 실제로 밟고 다닐 수 있는 바닥/플랫폼 콜라이더를 절차적으로 배치한다.
    public static class PlatformSpawner
    {
        public static void BuildRoomGeometry(Transform parent, Vector2 roomSize, int seed)
        {
            BuildFloor(parent, roomSize);
            BuildFloatingPlatforms(parent, roomSize, seed);
        }

        private static void BuildFloor(Transform parent, Vector2 roomSize)
        {
            CreatePlatform(parent, "Floor", new Vector2(0f, -roomSize.y / 2f + 0.5f), new Vector2(roomSize.x, 1f));
        }

        private static void BuildFloatingPlatforms(Transform parent, Vector2 roomSize, int seed)
        {
            var rng = new System.Random(seed);
            int platformCount = rng.Next(2, 5);

            for (int i = 0; i < platformCount; i++)
            {
                float width = Mathf.Lerp(2f, 4f, (float)rng.NextDouble());
                float x = Mathf.Lerp(-roomSize.x / 2f + width, roomSize.x / 2f - width, (float)rng.NextDouble());
                float y = Mathf.Lerp(-roomSize.y / 2f + 2.5f, roomSize.y / 2f - 1f, (float)rng.NextDouble());

                CreatePlatform(parent, $"Platform_{i}", new Vector2(x, y), new Vector2(width, 0.5f));
            }
        }

        private static void CreatePlatform(Transform parent, string name, Vector2 localPosition, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            go.layer = groundLayer >= 0 ? groundLayer : 0;

            go.AddComponent<BoxCollider2D>();

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateSquare();
            renderer.color = new Color(0.4f, 0.4f, 0.45f);
        }
    }
}
