using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 땅 타일 위에 새 팔레트 그림을 얹는다 (Docs/ArtStyle.md, Tools/ArtGen/generate_terrain.py).
    // 땅 몸통은 흙색 사각형(블록마다 하나)으로 채우고, 드러난 면에만 풀 띠·옆면·아랫면 그림을, 안쪽에 돌멩이, 윗면에 장식을 시드로 놓는다.
    // 그림이 없으면 Load()가 null을 돌려주고 ChunkBuilder는 예전 방식(외부 타일/단색)을 쓴다.
    public class TerrainDresser
    {
        public static readonly Color Soil = new Color(0.561f, 0.353f, 0.18f); // 팔레트 soil #8f5a2e

        private const int FillOrder = -6;
        private const int PebbleOrder = -5;
        private const int EdgeOrder = -4;
        private const int GrassOrder = -3;
        private const int DecorOrder = -2;

        // 풀 그림(140x140)에서 타일 윗면은 위에서 40px, 그림 중심은 70px → 중심을 윗면보다 0.3 아래에 둔다.
        private const float GrassCenterBelowTop = 0.3f;

        private readonly Sprite[] grass;  // 가운데, 왼쪽 끝, 오른쪽 끝, 양쪽 끝
        private readonly Sprite[] sides;  // 왼쪽, 오른쪽
        private readonly Sprite under;
        private readonly Sprite[] pebbles;
        private readonly Sprite[] decor;

        private static TerrainDresser cached;
        private static bool loaded;

        private TerrainDresser(Sprite[] grass, Sprite[] sides, Sprite under, Sprite[] pebbles, Sprite[] decor)
        {
            this.grass = grass;
            this.sides = sides;
            this.under = under;
            this.pebbles = pebbles;
            this.decor = decor;
        }

        public static TerrainDresser Load()
        {
            if (loaded) return cached;
            loaded = true;

            var grass = TrapCommon.Frames("TerrainGrass");
            var sides = TrapCommon.Frames("TerrainSide");
            var under = TrapCommon.Frame("TerrainUnder");
            var pebbles = TrapCommon.Frames("TerrainPebble");
            var decor = TrapCommon.Frames("TerrainDecor");
            if (grass == null || grass.Length < 4 || sides == null || sides.Length < 2) return null;

            cached = new TerrainDresser(grass, sides, under, pebbles, decor);
            return cached;
        }

        // 땅 블록 하나의 몸통 (콜라이더와 같은 크기의 흙색 사각형).
        public static void Fill(Transform block, Vector2 size)
        {
            var fill = new GameObject("Fill");
            fill.transform.SetParent(block, false);
            fill.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = fill.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSpriteFactory.CreateSquare();
            renderer.color = Soil;
            renderer.sortingOrder = FillOrder;
        }

        public void Dress(ChunkData chunk, Transform root)
        {
            var parent = new GameObject("TerrainArt").transform;
            parent.SetParent(root, false);
            var blocked = BlockedTiles(chunk);

            for (int y = 0; y < ChunkData.Height; y++)
            {
                for (int x = 0; x < chunk.Cols; x++)
                {
                    if (chunk.Tiles[x, y] != StageTile.Solid) continue;

                    var center = new Vector2(x + 0.5f, y + 0.5f);
                    int hash = Hash(chunk.Index, x, y);
                    bool top = !Solid(chunk, x, y + 1);

                    if (!Solid(chunk, x - 1, y)) Add(parent, sides[0], center, EdgeOrder);
                    if (!Solid(chunk, x + 1, y)) Add(parent, sides[1], center, EdgeOrder);
                    if (under != null && !Solid(chunk, x, y - 1)) Add(parent, under, center, EdgeOrder);

                    if (top)
                    {
                        // 이웃 칸에 이어지는 윗면이 없으면 그쪽 끝을 둥글게 마무리한다.
                        bool leftEnd = !Solid(chunk, x - 1, y) || Solid(chunk, x - 1, y + 1);
                        bool rightEnd = !Solid(chunk, x + 1, y) || Solid(chunk, x + 1, y + 1);
                        var sprite = grass[(leftEnd ? 1 : 0) + (rightEnd ? 2 : 0)];
                        Add(parent, sprite, new Vector2(center.x, y + 1f - GrassCenterBelowTop), GrassOrder);

                        if (decor != null && decor.Length > 0 && hash % 4 == 0 && Empty(chunk, x, y + 1) && !blocked.Contains(new Vector2Int(x, y + 1)))
                        {
                            var deco = decor[(hash / 4) % decor.Length];
                            float jitter = ((hash / 32) % 5 - 2) * 0.08f;
                            // 아래 끝이 윗면보다 살짝 묻히게 (풀 띠 위에서 자라는 것처럼)
                            var position = new Vector2(center.x + jitter, y + 1f + deco.bounds.size.y / 2f - 0.06f);
                            var renderer = Add(parent, deco, position, DecorOrder);
                            if ((hash / 128) % 2 == 0) renderer.flipX = true;
                        }
                    }
                    else if (pebbles != null && pebbles.Length > 0 && hash % 5 == 0)
                    {
                        Add(parent, pebbles[(hash / 5) % pebbles.Length], center, PebbleOrder);
                    }
                }
            }
        }

        // 함정·문·출구·회복 구슬 칸에는 장식을 두지 않는다 (위험 신호와 목표물이 가려지지 않게).
        private static HashSet<Vector2Int> BlockedTiles(ChunkData chunk)
        {
            var blocked = new HashSet<Vector2Int>();
            foreach (var feature in chunk.Features)
            {
                switch (feature.Kind)
                {
                    case FeatureKind.Spikes:
                    case FeatureKind.FlameJet:
                    case FeatureKind.ArrowTrap:
                    case FeatureKind.Stalactite:
                    case FeatureKind.Exit:
                    case FeatureKind.Reward:
                    case FeatureKind.PlatformSwitch:
                        int width = Mathf.Max(1, feature.Width);
                        for (int dx = -1; dx <= width; dx++) blocked.Add(new Vector2Int(feature.X + dx, feature.Y));
                        break;
                    case FeatureKind.ElementGate:
                        for (int y = 0; y < ChunkData.Height; y++)
                            for (int dx = -1; dx <= 1; dx++) blocked.Add(new Vector2Int(feature.X + dx, y));
                        break;
                }
            }

            return blocked;
        }

        // 청크 밖 가로 이웃은 이음새 높이가 같으므로 같은 행의 끝 칸으로 본다. 아래는 계속 땅, 위는 하늘.
        private static bool Solid(ChunkData chunk, int x, int y)
        {
            if (y < 0) return true;
            if (y >= ChunkData.Height) return false;
            x = Mathf.Clamp(x, 0, chunk.Cols - 1);
            return chunk.Tiles[x, y] == StageTile.Solid;
        }

        private static bool Empty(ChunkData chunk, int x, int y)
        {
            return y < ChunkData.Height && chunk.Tiles[x, y] == StageTile.Empty;
        }

        private static SpriteRenderer Add(Transform parent, Sprite sprite, Vector2 localPosition, int order)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static int Hash(int chunkIndex, int x, int y)
        {
            unchecked
            {
                uint h = (uint)(chunkIndex * 73856093) ^ (uint)(x * 19349663) ^ (uint)(y * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
