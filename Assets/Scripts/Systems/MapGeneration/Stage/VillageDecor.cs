using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // NPC 마을 방 꾸미기 (콜라이더 없는 그림만, 적·함정 없음). ChunkStreamer.LoadRoomExtras가 마을 방에서 부른다.
    // 마을 방은 Rest 청크라 ChunkBuilder가 샘 방과 같은 청록 색조를 덮는데, 여기서 따뜻한 살구빛으로 바꾼다.
    // 그림: Tools/ArtGen/generate_village.py (Lamp, Flowers0~2, Fence, Fountain), 16픽셀 = 1유닛.
    public static class VillageDecor
    {
        public static readonly Color WarmTint = new Color(1f, 0.62f, 0.32f, 0.2f);

        // 방 가로 비율. 건물(식당 0.3, 상점 0.7, 폭 3.5유닛) 문 앞과 겹치지 않게 둔다.
        private const float FountainAt = 0.5f;
        private static readonly float[] LampsAt = { 0.15f, 0.41f, 0.59f, 0.85f };
        private static readonly float[] FlowersAt = { 0.07f, 0.21f, 0.39f, 0.61f, 0.79f, 0.93f };
        private const float FenceFrom = 0.1f;
        private const float FenceTo = 0.9f;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // root: 마을 방 청크 루트, left: 방 왼쪽 월드 x, cols: 방 가로 칸 수, ground: 바닥 월드 y
        public static void Build(Transform root, float left, int cols, float ground)
        {
            var background = root.Find("Background");
            if (background != null && background.TryGetComponent<SpriteRenderer>(out var tint)) tint.color = WarmTint;

            var decor = new GameObject("VillageDecor").transform;
            decor.SetParent(root, false);
            decor.position = new Vector3(left, ground, 0f);

            // 울타리: 건물 뒤에 가로로 이어 붙인 한 줄
            var fence = Load("Fence");
            if (fence != null)
            {
                float width = fence.rect.width / VillageBuilding.PixelsPerUnit;
                for (float x = cols * FenceFrom; x + width <= cols * FenceTo; x += width)
                    Place(decor, fence, x + width / 2f, -5);
            }

            Place(decor, Load("Fountain"), cols * FountainAt, -2);
            foreach (float at in LampsAt) Place(decor, Load("Lamp"), cols * at, -4);
            for (int i = 0; i < FlowersAt.Length; i++) Place(decor, Load("Flowers" + (i % 3)), cols * FlowersAt[i], -1);
        }

        private static void Place(Transform parent, Sprite sprite, float x, int order)
        {
            if (sprite == null) return;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order; // 플레이어(1)보다 뒤, 건물(-3) 앞뒤로 나눠 깊이감
        }

        private static Sprite Load(string name)
        {
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            var tex = Resources.Load<Texture2D>("Village/" + name);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0f), VillageBuilding.PixelsPerUnit);
            sprite.name = name;
            cache[name] = sprite;
            return sprite;
        }
    }
}
