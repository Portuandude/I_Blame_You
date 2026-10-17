using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 스킬/투사체 이펙트 스프라이트를 LevelArtConfig에서 꺼내 쓰고, 아직 생성 전이면 원형 스프라이트로 대신한다.
    public static class EffectLibrary
    {
        private static LevelArtConfig config;
        private static bool loaded;

        public static LevelArtConfig Config
        {
            get
            {
                if (!loaded)
                {
                    config = Resources.Load<LevelArtConfig>("LevelArtConfig");
                    loaded = true;
                }

                return config;
            }
        }

        public static Sprite[] SpellFrames(Element element)
        {
            var c = Config;
            if (c == null) return null;
            switch (element)
            {
                case Element.Fire: return NullIfEmpty(c.fireballFrames);
                case Element.Water: return NullIfEmpty(c.waterLanceFrames);
                case Element.Wind: return NullIfEmpty(c.windGustFrames);
                case Element.Earth: return NullIfEmpty(c.earthSpikeFrames);
                default: return NullIfEmpty(c.arcaneOrbFrames);
            }
        }

        public static Sprite[] ShieldFrames => Config != null ? NullIfEmpty(Config.shieldFrames) : null;
        public static Sprite[] TeleportFrames => Config != null ? NullIfEmpty(Config.teleportFrames) : null;
        public static Sprite[] ArrowFrames => Config != null ? NullIfEmpty(Config.arrowFrames) : null;
        public static Sprite[] SlashFrames => Config != null ? NullIfEmpty(Config.slashFrames) : null;

        // frames가 있으면 프레임 애니메이션을, 없으면 원형 스프라이트에 색을 입혀 보여준다.
        public static SpriteRenderer CreateVisual(Transform parent, string name, Sprite[] frames, float fps, bool loop,
            Color fallbackColor, float fallbackSize, int sortingOrder = 5, bool destroyAtEnd = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (frames != null && frames.Length > 0)
            {
                go.AddComponent<SpriteFrameAnimator>().Play(frames, fps, loop, destroyAtEnd);
            }
            else
            {
                renderer.sprite = SolidSpriteFactory.CreateCircle();
                renderer.color = fallbackColor;
                go.transform.localScale = new Vector3(fallbackSize, fallbackSize, 1f);
                if (destroyAtEnd) Object.Destroy(go, 0.4f);
            }

            return renderer;
        }

        // 제자리에서 한 번 재생하고 사라지는 이펙트 (순간이동 연기, 베기 등).
        public static void PlayOneShot(Vector3 position, Sprite[] frames, float fps, Color fallbackColor, float fallbackSize,
            bool flipX = false, float scale = 1f)
        {
            var root = new GameObject("OneShotEffect");
            root.transform.position = position;
            root.transform.localScale = new Vector3(flipX ? -scale : scale, scale, 1f);
            CreateVisual(root.transform, "Visual", frames, fps, false, fallbackColor, fallbackSize, 6, destroyAtEnd: true);
            Object.Destroy(root, frames != null && frames.Length > 0 ? frames.Length / fps + 0.05f : 0.45f);
        }

        // ---- Resources/Effects 아래의 런타임 이펙트 (Generate 없이 바로 쓰는 마법 비행/적중 프레임) ----

        public const float RuntimePixelsPerUnit = 100f;
        private static readonly Dictionary<string, Sprite[]> RuntimeFrames = new Dictionary<string, Sprite[]>();

        // 마법 투사체가 날아가는 동안 반복 재생할 프레임. Resources에 없으면 LevelArtConfig 것을 쓴다.
        public static Sprite[] FlightFrames(Element element) =>
            LoadRuntimeFrames($"Effects/Flight/{ElementFolder(element)}") ?? SpellFrames(element);

        // 마법이 적이나 벽에 맞았을 때 한 번 재생할 프레임.
        public static Sprite[] HitFrames(Element element) => LoadRuntimeFrames($"Effects/Hit/{ElementFolder(element)}");

        public static void PlayHit(Element element, Vector3 position, float scale = 1f)
        {
            PlayOneShot(position, HitFrames(element), 20f, ElementInfo.Color(element), 0.8f * scale, false, scale);
        }

        private static string ElementFolder(Element element) => element.ToString();

        // Resources/<path>/ 의 PNG를 이름순으로 읽어 중심 피벗 스프라이트로 만든다 (100px = 1유닛).
        // 텍스처로 읽어 직접 스프라이트를 만들기 때문에 임포트 설정(Sprite/Default)에 상관없이 동작한다.
        public static Sprite[] LoadRuntimeFrames(string path)
        {
            if (RuntimeFrames.TryGetValue(path, out var cached)) return cached;

            var textures = Resources.LoadAll<Texture2D>(path);
            Sprite[] frames = null;
            if (textures != null && textures.Length > 0)
            {
                System.Array.Sort(textures, (a, b) => string.CompareOrdinal(a.name, b.name));
                frames = new Sprite[textures.Length];
                for (int i = 0; i < textures.Length; i++)
                {
                    var texture = textures[i];
                    frames[i] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                        RuntimePixelsPerUnit);
                    frames[i].name = texture.name;
                }
            }

            RuntimeFrames[path] = frames;
            return frames;
        }

        private static Sprite[] NullIfEmpty(Sprite[] frames) => frames != null && frames.Length > 0 ? frames : null;
    }
}
