using System;
using System.Collections.Generic;

namespace IBlameYou.Systems
{
    // 화면 설정 규칙 (2026-11-12 플레이테스트 21번: 16:9 고정, 해상도 선택, 창/테두리 없는 창/전체 화면).
    // UnityEngine 없이 계산만 해서 EditMode 테스트로 검증한다. 적용은 Core/DisplaySettings가 한다.
    public static class DisplayRules
    {
        public const float Aspect = 16f / 9f;

        // 고를 수 있는 16:9 해상도 (작은 것부터).
        public static readonly (int width, int height)[] Resolutions =
        {
            (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3200, 1800), (3840, 2160)
        };

        // 창 모드 이름 (순서 = Core/DisplaySettings의 FullScreenMode 대응: 창 / 테두리 없는 창 / 전체 화면).
        public static readonly string[] ModeNames = { "창 모드", "테두리 없는 창 모드", "전체 화면" };
        public const int Windowed = 0;
        public const int Borderless = 1;
        public const int Fullscreen = 2;

        // 모니터(maxW × maxH)에 들어가는 해상도들의 번호. 하나도 안 들어가면 가장 작은 것 하나.
        public static List<int> Available(int maxWidth, int maxHeight)
        {
            var list = new List<int>();
            for (int i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i].width <= maxWidth && Resolutions[i].height <= maxHeight) list.Add(i);
            if (list.Count == 0) list.Add(0);
            return list;
        }

        // 저장된 해상도 번호가 없거나(-1) 모니터보다 크면, 모니터에 들어가는 가장 큰 것.
        public static int Sanitize(int index, int maxWidth, int maxHeight)
        {
            var available = Available(maxWidth, maxHeight);
            return available.Contains(index) ? index : available[available.Count - 1];
        }

        public static int SanitizeMode(int mode) => mode < Windowed || mode > Fullscreen ? Borderless : mode;

        // 화면(screenW × screenH) 안에 16:9를 가운데 맞춘 카메라 영역 (0~1 비율: x, y, 폭, 높이). 남는 쪽은 검은 띠.
        public static (float x, float y, float width, float height) Letterbox(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0) return (0f, 0f, 1f, 1f);
            float screenAspect = (float)screenWidth / screenHeight;
            if (Math.Abs(screenAspect - Aspect) < 0.001f) return (0f, 0f, 1f, 1f);
            if (screenAspect > Aspect)
            {
                float width = Aspect / screenAspect; // 좌우 띠
                return ((1f - width) / 2f, 0f, width, 1f);
            }

            float height = screenAspect / Aspect; // 위아래 띠
            return (0f, (1f - height) / 2f, 1f, height);
        }

        public static string ResolutionName(int index) => $"{Resolutions[index].width} × {Resolutions[index].height}";
    }
}
