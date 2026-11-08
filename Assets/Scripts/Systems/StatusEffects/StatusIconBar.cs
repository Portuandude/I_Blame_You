using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 디버프가 걸린 대상의 머리 위에 사각형 아이콘을 띄운다 (화상/젖음/경직 순서로 가로 배치).
    // 남은 시간은 아이콘 위쪽부터 반투명해지다가 사라지는 세로 그라데이션으로 보여준다.
    // 그라데이션을 셰이더 없이 그리려고 아이콘을 가로 띠(Slices개)로 잘라 띠마다 투명도를 준다.
    // 대상이 좌우 반전(localScale.x < 0)돼도 아이콘이 뒤집히지 않게 자식이 아닌 별도 오브젝트로 따라다닌다.
    public class StatusIconBar : MonoBehaviour
    {
        private const int Slices = 16;
        private const float IconSize = 0.5f;      // 월드 유닛
        private const float Spacing = 0.58f;
        private const float HeadGap = 0.35f;      // 몸통 위쪽 끝에서 아이콘 아래까지
        private const float EdgeSoftness = 0.08f; // 불투명 ↔ 반투명 경계의 부드러움 (아이콘 높이 비율)
        private const float FadedAlpha = 0.5f;    // 지나간 시간 영역이 시작되는 투명도 (위로 갈수록 0)

        private static readonly StatusDebuff[] Order = { StatusDebuff.Burn, StatusDebuff.Wet, StatusDebuff.Stun };
        private static readonly Dictionary<StatusDebuff, Sprite[]> SliceCache = new Dictionary<StatusDebuff, Sprite[]>();
        private static Font font;

        private class Icon
        {
            public GameObject Root;
            public SpriteRenderer[] Slices;
            public TextMesh Count;
        }

        private StatusEffects effects;
        private Collider2D body;
        private readonly Dictionary<StatusDebuff, Icon> icons = new Dictionary<StatusDebuff, Icon>();
        private readonly List<(StatusDebuff debuff, float fraction)> active = new List<(StatusDebuff, float)>();

        public static void Attach(StatusEffects owner)
        {
            var bar = new GameObject($"StatusIcons_{owner.name}").AddComponent<StatusIconBar>();
            bar.effects = owner;
            bar.body = owner.GetComponent<Collider2D>();
            foreach (var debuff in Order) bar.icons[debuff] = bar.CreateIcon(debuff);
        }

        private void LateUpdate()
        {
            if (effects == null)
            {
                Destroy(gameObject);
                return;
            }

            active.Clear();
            foreach (var debuff in Order)
            {
                float fraction = Fraction(debuff);
                if (fraction > 0f) active.Add((debuff, fraction));
                else icons[debuff].Root.SetActive(false);
            }

            transform.position = AnchorPosition();

            float startX = -(active.Count - 1) * Spacing * 0.5f;
            for (int i = 0; i < active.Count; i++)
            {
                var icon = icons[active[i].debuff];
                icon.Root.SetActive(true);
                icon.Root.transform.localPosition = new Vector3(startX + i * Spacing, IconSize * 0.5f, 0f);
                ApplyGradient(icon, active[i].fraction);

                if (icon.Count != null)
                {
                    int stacks = effects.BurnStacks;
                    icon.Count.text = stacks > 1 ? stacks.ToString() : "";
                }
            }
        }

        private float Fraction(StatusDebuff debuff)
        {
            switch (debuff)
            {
                case StatusDebuff.Burn: return effects.BurnFraction;
                case StatusDebuff.Wet: return effects.WetFraction;
                case StatusDebuff.Stun: return effects.StunFraction;
                default: return 0f;
            }
        }

        private Vector3 AnchorPosition()
        {
            if (body != null && body.enabled)
            {
                var bounds = body.bounds;
                return new Vector3(bounds.center.x, bounds.max.y + HeadGap, 0f);
            }

            return effects.transform.position + new Vector3(0f, 1.6f, 0f);
        }

        // remaining: 남은 시간 비율. 위쪽 (1 - remaining) 만큼이 지나간 시간 영역이다.
        // 경계 아래는 불투명, 경계 위는 반투명(FadedAlpha)에서 시작해 맨 위로 갈수록 0이 된다.
        private static void ApplyGradient(Icon icon, float remaining)
        {
            float boundary = remaining; // 아래(0)에서 위(1)로 잰 경계 높이
            if (boundary >= 0.999f)
            {
                foreach (var slice in icon.Slices) slice.color = Color.white;
                return;
            }

            for (int i = 0; i < Slices; i++)
            {
                float y = (i + 0.5f) / Slices;
                float above = boundary < 1f ? Mathf.Clamp01((y - boundary) / (1f - boundary)) : 0f;
                float faded = Mathf.Lerp(FadedAlpha, 0f, above);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(boundary - EdgeSoftness, boundary + EdgeSoftness, y));
                float alpha = Mathf.Lerp(1f, faded, t);
                icon.Slices[i].color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private Icon CreateIcon(StatusDebuff debuff)
        {
            var root = new GameObject(debuff.ToString());
            root.transform.SetParent(transform, false);

            var sprites = SlicesFor(debuff);
            var renderers = new SpriteRenderer[Slices];
            for (int i = 0; i < Slices; i++)
            {
                var slice = new GameObject($"Slice{i:00}");
                slice.transform.SetParent(root.transform, false);
                slice.transform.localPosition = new Vector3(0f, ((i + 0.5f) / Slices - 0.5f) * IconSize, 0f);
                var renderer = slice.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.sortingOrder = 40;
                renderers[i] = renderer;
            }

            TextMesh count = null;
            if (debuff == StatusDebuff.Burn) count = CreateCountLabel(root.transform);

            root.SetActive(false);
            return new Icon { Root = root, Slices = renderers, Count = count };
        }

        private static TextMesh CreateCountLabel(Transform parent)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("Stacks");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(IconSize * 0.42f, -IconSize * 0.3f, 0f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 48;
            mesh.characterSize = 0.045f;
            mesh.fontStyle = FontStyle.Bold;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = Color.white;

            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 41;
            return mesh;
        }

        // Resources/StatusIcons/<이름>.png를 가로 띠로 자른 스프라이트. 파일이 없으면 단색 사각형.
        private static Sprite[] SlicesFor(StatusDebuff debuff)
        {
            if (SliceCache.TryGetValue(debuff, out var cached) && cached[0] != null) return cached;

            var texture = Resources.Load<Texture2D>($"StatusIcons/{debuff}");
            if (texture == null) texture = FallbackTexture(debuff);

            var sprites = new Sprite[Slices];
            float sliceHeight = texture.height / (float)Slices;
            float pixelsPerUnit = texture.width / IconSize;
            for (int i = 0; i < Slices; i++)
            {
                var rect = new Rect(0f, i * sliceHeight, texture.width, sliceHeight);
                sprites[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
            }

            SliceCache[debuff] = sprites;
            return sprites;
        }

        private static Texture2D FallbackTexture(StatusDebuff debuff)
        {
            Color color;
            switch (debuff)
            {
                case StatusDebuff.Burn: color = new Color(1f, 0.45f, 0.15f); break;
                case StatusDebuff.Wet: color = new Color(0.3f, 0.6f, 1f); break;
                default: color = new Color(0.85f, 0.7f, 0.3f); break;
            }

            const int size = 32;
            var texture = new Texture2D(size, size) { filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                pixels[y * size + x] = border ? new Color(0.1f, 0.08f, 0.1f) : color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
