using UnityEngine;

namespace IBlameYou.Systems
{
    // 아트 에셋이 준비되기 전까지, 방/플랫폼을 눈으로 확인할 수 있도록 1x1 단색 스프라이트를 만들어 재사용한다.
    public static class SolidSpriteFactory
    {
        private static Sprite cachedSquare;
        private static Sprite cachedLeftPivotSquare;

        public static Sprite CreateSquare()
        {
            if (cachedSquare != null) return cachedSquare;
            cachedSquare = CreateSquare(new Vector2(0.5f, 0.5f));
            return cachedSquare;
        }

        // 왼쪽 가장자리를 고정한 채 가로 스케일만 줄여서 채움 게이지(HP/기력/마나 바 등)를 표현할 때 쓴다.
        public static Sprite CreateLeftPivotSquare()
        {
            if (cachedLeftPivotSquare != null) return cachedLeftPivotSquare;
            cachedLeftPivotSquare = CreateSquare(new Vector2(0f, 0.5f));
            return cachedLeftPivotSquare;
        }

        private static Sprite CreateSquare(Vector2 pivot)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), pivot, 1f);
        }
    }
}
