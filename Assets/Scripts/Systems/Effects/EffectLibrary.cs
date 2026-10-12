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

        private static Sprite[] NullIfEmpty(Sprite[] frames) => frames != null && frames.Length > 0 ? frames : null;
    }
}
