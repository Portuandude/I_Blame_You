using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 줄어든 부분을 잠깐 흰색으로 남기는 잔상 비율 (HUD 바·머리 위 바 공용).
    // 피해로 줄면 Hold초 그대로 있다가 초당 Speed 비율로 따라 내려가고, 늘면 바로 맞춘다.
    public struct BarTrail
    {
        public const float Hold = 0.45f;
        public const float Speed = 0.9f;

        public float Shown;      // 잔상 비율
        public float Last;       // 지난 갱신 비율
        public float DropTime;   // 마지막으로 줄어든 시각
        public bool Started;

        public void Step(float ratio, float now, float deltaTime)
        {
            if (!Started)
            {
                Started = true;
                Shown = Last = ratio;
                return;
            }

            if (ratio < Last - 0.0001f) DropTime = now;
            Last = ratio;

            if (ratio >= Shown) Shown = ratio;
            else if (now - DropTime > Hold) Shown = Mathf.Max(ratio, Shown - Speed * deltaTime);
        }
    }

    // 머리 위(월드) 둥근 캡슐 바: 바탕 → 흰 잔상 → 채움 스프라이트 3장.
    // HUD와 같은 그림(Resources/UI/HUD/BarBack·BarFill, generate_hud.py)을 9분할(Sliced) 스프라이트로 늘린다.
    // 그림이 없으면 예전처럼 단색 사각형으로 그린다. 채움은 크기(size.x)만 줄여서 왼쪽 둥근 끝을 유지한다.
    public class CapsuleBar
    {
        private const float TexturePixelHeight = 18f; // generate_hud.py BAR_H
        private const float TextureBorder = 9f;       // 좌우 9분할 테두리 픽셀
        private const float TrailAlpha = 0.95f;

        private static Texture2D backTexture;
        private static Texture2D fillTexture;
        private static bool texturesLoaded;
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        private readonly float width;
        private readonly float height;
        private readonly SpriteRenderer back;
        private readonly SpriteRenderer trail;
        private readonly SpriteRenderer fill;
        private readonly bool sliced;
        private readonly Color backColor;
        private readonly Color fillColor;
        private BarTrail trailState;
        private float ratio = 1f;
        private float alpha = 1f;

        public CapsuleBar(Transform parent, string name, Vector3 localPosition, float width, float height,
            Color backColor, Color fillColor, int sortingOrder)
        {
            this.width = width;
            this.height = height;
            this.backColor = backColor;
            this.fillColor = fillColor;

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;

            LoadTextures();
            sliced = backTexture != null && fillTexture != null;

            back = MakeRenderer(root, "Background", sliced ? Sliced(backTexture, height, false) : SolidSpriteFactory.CreateSquare(),
                Vector3.zero, sortingOrder);
            trail = MakeRenderer(root, "Trail", sliced ? Sliced(fillTexture, height, true) : SolidSpriteFactory.CreateLeftPivotSquare(),
                new Vector3(-width / 2f, 0f, 0f), sortingOrder + 1);
            fill = MakeRenderer(root, "Fill", sliced ? Sliced(fillTexture, height, true) : SolidSpriteFactory.CreateLeftPivotSquare(),
                new Vector3(-width / 2f, 0f, 0f), sortingOrder + 2);

            SetLength(back, width);
            SetLength(trail, width);
            SetLength(fill, width);
            ApplyColors();
        }

        public void SetRatio(float value)
        {
            ratio = Mathf.Clamp01(value);
            SetLength(fill, width * ratio);
        }

        // 매 프레임 (LateUpdate) 잔상을 따라 내리고 색(투명도)을 적용한다.
        public void Tick(float now, float deltaTime)
        {
            trailState.Step(ratio, now, deltaTime);
            SetLength(trail, width * trailState.Shown);
            ApplyColors();
        }

        public void SetAlpha(float value)
        {
            alpha = Mathf.Clamp01(value);
            ApplyColors();
        }

        private void ApplyColors()
        {
            // 그림이 있으면 바탕 그림 자체가 진한 보라라 흰색으로 곱하고, 단색 대체일 때만 backColor를 쓴다.
            var b = sliced ? Color.white : backColor;
            back.color = new Color(b.r, b.g, b.b, backColor.a * alpha);
            trail.color = new Color(1f, 1f, 1f, TrailAlpha * alpha);
            fill.color = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * alpha);
        }

        private void SetLength(SpriteRenderer renderer, float length)
        {
            // 너무 짧으면 둥근 끝이 찌그러지므로 0에 가까우면 숨긴다.
            renderer.enabled = length > 0.001f;
            if (sliced)
            {
                renderer.size = new Vector2(Mathf.Max(length, 0.001f), height);
            }
            else
            {
                renderer.transform.localScale = new Vector3(length, height, 1f);
            }
        }

        private SpriteRenderer MakeRenderer(Transform root, string name, Sprite sprite, Vector3 localPosition, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (sliced) renderer.drawMode = SpriteDrawMode.Sliced;
            return renderer;
        }

        private static void LoadTextures()
        {
            if (texturesLoaded) return;
            texturesLoaded = true;
            backTexture = Resources.Load<Texture2D>("UI/HUD/BarBack");
            fillTexture = Resources.Load<Texture2D>("UI/HUD/BarFill");
        }

        // 바 높이마다 픽셀 밀도를 맞춘 9분할 스프라이트 (그림 18픽셀 = 바 높이). 높이·피벗별로 한 번만 만든다.
        private static Sprite Sliced(Texture2D texture, float worldHeight, bool leftPivot)
        {
            string key = $"{texture.name}/{Mathf.RoundToInt(worldHeight * 10000f)}/{(leftPivot ? "L" : "C")}";
            if (SpriteCache.TryGetValue(key, out var cached) && cached != null) return cached;

            float pixelsPerUnit = TexturePixelHeight / Mathf.Max(0.001f, worldHeight);
            var pivot = leftPivot ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(TextureBorder, 0f, TextureBorder, 0f));
            SpriteCache[key] = sprite;
            return sprite;
        }
    }
}
