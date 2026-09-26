using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 애니메이션으로 스프라이트가 바뀔 때마다 PolygonCollider2D를 그 스프라이트의 물리 셰이프에 맞춰 다시 만든다.
    // 물리 셰이프는 임포트 설정의 "Generate Physics Shape"로 만들어진 불투명 영역의 윤곽이다.
    // 셰이프가 없는 스프라이트(런타임 단색 사각형 등)는 스프라이트 경계 사각형으로 대체한다.
    public class SpriteColliderFitter : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer source;
        [SerializeField] private PolygonCollider2D target;

        private readonly List<Vector2> shape = new List<Vector2>();
        private readonly List<Vector2> converted = new List<Vector2>();
        private Sprite lastSprite;

        public void Configure(SpriteRenderer spriteRenderer, PolygonCollider2D polygonCollider)
        {
            source = spriteRenderer;
            target = polygonCollider;
            Refit();
        }

        private void Awake()
        {
            Refit();
        }

        private void LateUpdate()
        {
            if (source != null && source.sprite != lastSprite) Refit();
        }

        private void Refit()
        {
            if (source == null || target == null) return;

            var sprite = source.sprite;
            lastSprite = sprite;
            if (sprite == null) return;

            int shapeCount = sprite.GetPhysicsShapeCount();
            if (shapeCount == 0)
            {
                var b = sprite.bounds;
                shape.Clear();
                shape.Add(new Vector2(b.min.x, b.min.y));
                shape.Add(new Vector2(b.max.x, b.min.y));
                shape.Add(new Vector2(b.max.x, b.max.y));
                shape.Add(new Vector2(b.min.x, b.max.y));
                target.pathCount = 1;
                target.SetPath(0, ToTargetSpace(shape));
                return;
            }

            target.pathCount = shapeCount;
            for (int i = 0; i < shapeCount; i++)
            {
                sprite.GetPhysicsShape(i, shape);
                target.SetPath(i, ToTargetSpace(shape));
            }
        }

        // 스프라이트 로컬 좌표 → 콜라이더가 붙은 오브젝트의 로컬 좌표.
        // 월드를 거쳐 변환하므로 비주얼의 위치/스케일 오프셋과 좌우 반전(음수 스케일)이 모두 반영된다.
        private List<Vector2> ToTargetSpace(List<Vector2> spritePoints)
        {
            converted.Clear();
            foreach (var point in spritePoints)
            {
                var world = source.transform.TransformPoint(point);
                converted.Add(target.transform.InverseTransformPoint(world));
            }

            return converted;
        }
    }
}
