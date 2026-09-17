using UnityEngine;

namespace IBlameYou.Systems
{
    // 아트 에셋이 준비되기 전까지, 방/플랫폼을 눈으로 확인할 수 있도록 1x1 단색 스프라이트를 만들어 재사용한다.
    public static class SolidSpriteFactory
    {
        private static Sprite cachedSquare;

        public static Sprite CreateSquare()
        {
            if (cachedSquare != null) return cachedSquare;

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            cachedSquare = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return cachedSquare;
        }
    }
}
