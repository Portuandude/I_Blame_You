using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 함정 공통: 피해를 줄 공격자 스탯(StatTable.Trap)과 스테이지 그림(Resources/Stage/<이름>/NN.png) 읽기.
    public static class TrapCommon
    {
        private static CombatStats source;

        // 함정 피해 계산용 숨은 공격자. 씬을 다시 만들어도 계속 쓴다.
        public static CombatStats Source
        {
            get
            {
                if (source == null)
                {
                    var go = new GameObject("TrapStats") { hideFlags = HideFlags.HideInHierarchy };
                    Object.DontDestroyOnLoad(go);
                    source = go.AddComponent<CombatStats>();
                    source.Configure(StatTable.Trap);
                }

                return source;
            }
        }

        // 함정은 플레이어만 다치게 한다 (적은 함정을 피해 다니지 않으므로).
        public static PlayerMovement PlayerFrom(Collider2D other)
        {
            return other != null ? other.GetComponentInParent<PlayerMovement>() : null;
        }

        public static float Strike(PlayerMovement player, DamageKind kind, Element element, float multiplier)
        {
            return DamageCalculator.Strike(Source, player, kind, element, multiplier);
        }

        // 그림 프레임. 없으면 null (호출한 쪽이 단색으로 대신 그린다).
        public static Sprite[] Frames(string name) => EffectLibrary.LoadRuntimeFrames($"Stage/{name}");

        public static Sprite Frame(string name)
        {
            var frames = Frames(name);
            return frames != null ? frames[0] : null;
        }

        // 그림이 있으면 그 그림, 없으면 크기만큼 단색 사각형.
        public static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Vector2 localPosition, Vector2 fallbackSize,
            Color fallbackColor, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }
            else
            {
                renderer.sprite = SolidSpriteFactory.CreateSquare();
                renderer.color = fallbackColor;
                go.transform.localScale = new Vector3(fallbackSize.x, fallbackSize.y, 1f);
            }

            return renderer;
        }

        public static int GroundLayer
        {
            get
            {
                int layer = LayerMask.NameToLayer("Ground");
                return layer >= 0 ? layer : 0;
            }
        }
    }
}
