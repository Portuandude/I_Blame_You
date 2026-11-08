using UnityEngine;

namespace IBlameYou.Systems
{
    // ChunkData(타일 격자)를 씬 오브젝트로 만든다: 땅은 직사각형으로 묶어 한 청크당 콜라이더 하나(CompositeCollider2D)로 합치고,
    // 발판은 위에서만 밟히는 판(PlatformEffector2D)으로 만든다. 함정/적/전투 구역은 ChunkStreamer가 붙인다.
    public static class ChunkBuilder
    {
        public const float PlatformThickness = 0.3f;

        private static readonly Color FallbackGround = new Color(0.36f, 0.31f, 0.27f);

        public static Transform Build(ChunkData chunk, Transform parent, Sprite groundTile)
        {
            var root = new GameObject($"Chunk_{chunk.Index:00}_{chunk.Kind}").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(chunk.Index * ChunkData.Width, 0f, 0f);

            BuildBackground(chunk, root);
            BuildSolids(chunk, root, groundTile);
            BuildPlatforms(chunk, root, groundTile);
            return root;
        }

        // 전투/휴식/동굴처럼 성격이 다른 청크는 뒤 배경 색을 달리해서 멀리서도 알아보게 한다.
        private static void BuildBackground(ChunkData chunk, Transform root)
        {
            Color? tint = null;
            switch (chunk.Kind)
            {
                case ChunkKind.Arena: tint = new Color(0.24f, 0.13f, 0.16f); break;
                case ChunkKind.EliteArena: tint = new Color(0.3f, 0.16f, 0.12f); break;
                case ChunkKind.Boss: tint = new Color(0.34f, 0.09f, 0.09f); break;
                case ChunkKind.Rest: tint = new Color(0.11f, 0.2f, 0.25f); break;
                case ChunkKind.Cave: tint = new Color(0.08f, 0.08f, 0.11f); break;
            }

            if (!tint.HasValue) return;

            var go = new GameObject("Background");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(ChunkData.Width / 2f, ChunkData.Height / 2f, 0f);
            go.transform.localScale = new Vector3(ChunkData.Width, ChunkData.Height + 8f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateSquare();
            renderer.color = tint.Value;
            renderer.sortingOrder = -10;
        }

        // 땅 타일을 큰 직사각형으로 묶는다 (가로로 최대한 늘린 뒤 같은 폭으로 위로 늘림).
        private static void BuildSolids(ChunkData chunk, Transform root, Sprite groundTile)
        {
            var solids = new GameObject("Ground");
            solids.transform.SetParent(root, false);
            solids.layer = TrapCommon.GroundLayer;

            var body = solids.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var composite = solids.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.sharedMaterial = PhysicsMaterialFactory.Frictionless();

            var used = new bool[ChunkData.Width, ChunkData.Height];
            for (int y = 0; y < ChunkData.Height; y++)
            {
                for (int x = 0; x < ChunkData.Width; x++)
                {
                    if (used[x, y] || chunk.Tiles[x, y] != StageTile.Solid) continue;

                    int width = 1;
                    while (x + width < ChunkData.Width && !used[x + width, y] && chunk.Tiles[x + width, y] == StageTile.Solid) width++;

                    int height = 1;
                    while (y + height < ChunkData.Height && RowFree(chunk, used, x, width, y + height)) height++;

                    for (int dy = 0; dy < height; dy++)
                        for (int dx = 0; dx < width; dx++)
                            used[x + dx, y + dy] = true;

                    CreateBlock(solids.transform, new Vector2(x, y), new Vector2(width, height), groundTile);
                }
            }
        }

        private static bool RowFree(ChunkData chunk, bool[,] used, int x, int width, int y)
        {
            for (int dx = 0; dx < width; dx++)
            {
                if (used[x + dx, y] || chunk.Tiles[x + dx, y] != StageTile.Solid) return false;
            }

            return true;
        }

        private static void CreateBlock(Transform parent, Vector2 bottomLeft, Vector2 size, Sprite groundTile)
        {
            var go = new GameObject("Block");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = bottomLeft + size / 2f;
            go.layer = parent.gameObject.layer;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;

            if (groundTile != null)
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = groundTile;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = size;
                return;
            }

            // 타일 아트가 없을 때: 콜라이더 크기가 스케일에 영향받지 않도록 단색 그림만 자식으로 늘린다.
            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            fill.transform.localScale = new Vector3(size.x, size.y, 1f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = SolidSpriteFactory.CreateSquare();
            fillRenderer.color = FallbackGround;
        }

        // 같은 행에 이어진 발판 칸을 하나의 판으로 만든다. 판은 칸의 윗부분에만 있어서 아래로 걸어 지나갈 수 있다.
        private static void BuildPlatforms(ChunkData chunk, Transform root, Sprite groundTile)
        {
            var plank = TrapCommon.Frame("Platform");
            for (int y = 0; y < ChunkData.Height; y++)
            {
                for (int x = 0; x < ChunkData.Width; x++)
                {
                    if (chunk.Tiles[x, y] != StageTile.Platform) continue;
                    int start = x;
                    while (x + 1 < ChunkData.Width && chunk.Tiles[x + 1, y] == StageTile.Platform) x++;
                    CreatePlatform(root, start, x - start + 1, y + 1f, plank, groundTile);
                }
            }
        }

        private static void CreatePlatform(Transform root, int x, int width, float top, Sprite plank, Sprite groundTile)
        {
            var go = new GameObject("Platform");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x + width / 2f, top - PlatformThickness / 2f, 0f);
            go.layer = TrapCommon.GroundLayer;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, PlatformThickness);
            collider.usedByEffector = true;
            collider.sharedMaterial = PhysicsMaterialFactory.Frictionless();
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;

            for (int i = 0; i < width; i++)
            {
                var sprite = plank != null ? plank : groundTile;
                var renderer = TrapCommon.AddSprite(go.transform, $"Plank_{i}", sprite, new Vector2(i - (width - 1) / 2f, 0f),
                    new Vector2(1f, PlatformThickness + 0.1f), new Color(0.65f, 0.5f, 0.32f), 0);
                if (plank == null && groundTile != null)
                {
                    renderer.drawMode = SpriteDrawMode.Tiled;
                    renderer.size = new Vector2(1f, PlatformThickness + 0.1f);
                }
            }
        }
    }
}
