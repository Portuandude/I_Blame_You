using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // Physics2D.OverlapBoxAll / OverlapCircleAll과 같은 결과를 호출하는 쪽이 가진 목록에 담는다 (매번 새 배열을 만들지 않게).
    // 같은 결과: 기본 레이캐스트 레이어(Ignore Raycast 제외), 트리거 포함 여부는 프로젝트 설정 queriesHitTriggers.
    // 목록은 부르는 쪽마다 따로 둔다 — 맞힘 처리 중 다른 조회가 같은 목록을 덮어쓰지 않게.
    public static class OverlapQuery
    {
        private static ContactFilter2D Filter()
        {
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
            return filter;
        }

        public static List<Collider2D> Box(Vector2 center, Vector2 size, List<Collider2D> results)
        {
            Physics2D.OverlapBox(center, size, 0f, Filter(), results);
            return results;
        }

        public static List<Collider2D> Circle(Vector2 center, float radius, List<Collider2D> results)
        {
            Physics2D.OverlapCircle(center, radius, Filter(), results);
            return results;
        }
    }
}
