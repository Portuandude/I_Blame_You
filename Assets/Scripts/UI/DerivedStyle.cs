using System;
using UnityEngine;

namespace IBlameYou.UI
{
    // 기준 스타일에서 바꾼 스타일 하나를 한 번만 만들어 둔다 (OnGUI는 한 프레임에 여러 번 불려서
    // `new GUIStyle(body) { ... }`를 그리는 곳마다 쓰면 네이티브 GUIStyle이 계속 쌓인다).
    // 기준 스타일 객체가 바뀌면(창이 스타일을 새로 만들면) 다시 만든다. setup은 캡처 없는 람다로 넘긴다.
    public sealed class DerivedStyle
    {
        private GUIStyle from;
        private GUIStyle made;

        public GUIStyle Get(GUIStyle baseStyle, Action<GUIStyle> setup)
        {
            if (made == null || !ReferenceEquals(from, baseStyle))
            {
                from = baseStyle;
                made = new GUIStyle(baseStyle);
                setup?.Invoke(made);
            }
            return made;
        }
    }
}
