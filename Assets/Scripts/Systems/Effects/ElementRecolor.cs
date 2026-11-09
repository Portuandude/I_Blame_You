using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 애니메이터가 넣은 원본 스프라이트를 같은 이름의 회색 스프라이트(Resources/<folder>/<이름>.png)로 바꿔 끼우고
    // 색을 곱해서, 프레임 하나로 여러 색의 몹을 만든다 (속성 슬라임).
    // 애니메이터와 SpriteColliderFitter가 원본을 본 뒤에 바꾸도록 LateUpdate 실행 순서를 뒤로 둔다.
    // 콜라이더 모양은 원본 스프라이트의 물리 셰이프를 그대로 쓴다 (SpriteColliderFitter가 ToOriginal로 되돌려 읽음).
    [DefaultExecutionOrder(1000)]
    public class ElementRecolor : MonoBehaviour
    {
        // 폴더별: 원본 스프라이트 → 회색 스프라이트 (원본마다 피벗/PPU를 맞춰 한 번만 만든다).
        private static readonly Dictionary<string, Dictionary<string, Texture2D>> TexturesByFolder =
            new Dictionary<string, Dictionary<string, Texture2D>>();
        // 같은 원본 스프라이트라도 폴더마다 다른 그림으로 바꾼다 (속성 슬라임 회색판 / 슬라임 기사 투구판).
        private static readonly Dictionary<string, Dictionary<Sprite, Sprite>> GrayByFolder =
            new Dictionary<string, Dictionary<Sprite, Sprite>>();
        private static readonly Dictionary<Sprite, Sprite> OriginalByGray = new Dictionary<Sprite, Sprite>();

        private SpriteRenderer spriteRenderer;
        private Dictionary<string, Texture2D> textures;
        private Dictionary<Sprite, Sprite> grayByOriginal;
        private float padding;
        private Color tint = Color.white;

        // 곱할 색 (보스의 발광처럼 잠깐 색을 바꿀 때).
        public Color Tint
        {
            get => tint;
            set => tint = value;
        }

        // padding: 바꿔 끼울 그림이 원본보다 사방으로 몇 px 넓은지 (슬라임 기사는 투구 때문에 넓힌 캔버스). 0이면 원본과 같은 크기.
        public static ElementRecolor Attach(SpriteRenderer renderer, string resourcesFolder, Color color, float padding = 0f)
        {
            var recolor = renderer.GetComponent<ElementRecolor>();
            if (recolor == null) recolor = renderer.gameObject.AddComponent<ElementRecolor>();
            recolor.spriteRenderer = renderer;
            recolor.textures = LoadFolder(resourcesFolder);
            recolor.padding = padding;
            if (!GrayByFolder.TryGetValue(resourcesFolder, out recolor.grayByOriginal))
                GrayByFolder[resourcesFolder] = recolor.grayByOriginal = new Dictionary<Sprite, Sprite>();
            recolor.tint = color;
            recolor.Apply();
            return recolor;
        }

        // 회색으로 바뀐 스프라이트면 원본을, 아니면 그대로 돌려준다.
        public static Sprite ToOriginal(Sprite sprite)
        {
            return sprite != null && OriginalByGray.TryGetValue(sprite, out var original) ? original : sprite;
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.color = tint;

            var current = spriteRenderer.sprite;
            if (current == null || OriginalByGray.ContainsKey(current)) return; // 이미 회색

            var gray = GrayFor(current);
            if (gray != null) spriteRenderer.sprite = gray;
        }

        private Sprite GrayFor(Sprite original)
        {
            if (grayByOriginal == null) return null;
            if (grayByOriginal.TryGetValue(original, out var cached)) return cached;
            if (textures == null || !textures.TryGetValue(original.name, out var texture)) return null;

            var rect = original.rect;
            // 피벗은 원본 그림 크기 기준으로 계산한다 (임포트가 텍스처를 2의 거듭제곱으로 늘려도 비율은 그대로).
            // 넓힌 그림(padding > 0)이면 피벗을 그만큼 옮기고, 텍스처 실제 높이에 맞춰 PPU를 고쳐 원본과 같은 크기로 그린다.
            float fullWidth = rect.width + padding * 2f;
            float fullHeight = rect.height + padding * 2f;
            var pivot = new Vector2((original.pivot.x + padding) / fullWidth, (original.pivot.y + padding) / fullHeight);
            float ppu = padding > 0f ? original.pixelsPerUnit * texture.height / fullHeight : original.pixelsPerUnit;
            var gray = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, ppu);
            gray.name = original.name + "_Gray";

            grayByOriginal[original] = gray;
            OriginalByGray[gray] = original;
            return gray;
        }

        private static Dictionary<string, Texture2D> LoadFolder(string folder)
        {
            if (TexturesByFolder.TryGetValue(folder, out var loaded)) return loaded;

            loaded = new Dictionary<string, Texture2D>();
            foreach (var texture in Resources.LoadAll<Texture2D>(folder))
            {
                loaded[texture.name] = texture;
            }

            TexturesByFolder[folder] = loaded;
            return loaded;
        }
    }
}
