using UnityEngine;

namespace IBlameYou.Systems
{
    // 벽/바닥에 마찰이 있으면 캐릭터가 벽에 붙은 채로 방향키를 눌러도 미끄러져 내려오지 않는다.
    // 플레이어와 레벨 지오메트리 콜라이더에 공통으로 물려서 마찰을 0으로 만든다.
    public static class PhysicsMaterialFactory
    {
        private static PhysicsMaterial2D frictionless;

        public static PhysicsMaterial2D Frictionless()
        {
            if (frictionless == null)
            {
                frictionless = new PhysicsMaterial2D("Frictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }

            return frictionless;
        }
    }
}
