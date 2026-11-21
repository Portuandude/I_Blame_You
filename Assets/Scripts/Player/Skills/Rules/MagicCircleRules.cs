using System;
using System.Collections.Generic;

namespace IBlameYou.Player
{
    // 마법진 한 점. 마법진 가운데가 (0, 0), 바깥 원 반지름이 0.86인 좌표 (1 = 판 반지름). y는 아래로 커진다 (화면 GUI 좌표와 같은 방향).
    public struct CirclePoint
    {
        public float X, Y;
        public CirclePoint(float x, float y) { X = x; Y = y; }
    }

    // P2 마법진 그리기 규칙 (2026-11-16 사람 결정, Docs/MagicCircleDesign.md). 순수 C#, EditMode 테스트 대상.
    // - 공격 마법을 고르면 영창 타이핑 대신 마우스로 마법진을 따라 그린다. 게임은 멈추지도 느려지지도 않는다.
    // - 룬 4개(불·물·바람·땅)는 火·水·风·土의 뼈대 느낌만 빌린 새 문자. 단계마다 마법진 전체가 한 획(한붓그리기)이다.
    // - 단계: 같은 속성 안 마나 코스트 순위 (가장 싼 마법 = 1단계). 1단계 룬만, 2단계 바깥 원 → 룬, 3단계 바깥 원 → 속성 도형 → 안쪽 원 → 룬.
    // - 판정: 마우스를 떼는 순간 (한붓이라 떼면 끝). 일치율 = 덮은 정도 × 덜 샌 정도. 50% 이하 실패(마나만 날림), 그 위는 피해 × 일치율 ÷ 75%.
    public static class MagicCircleRules
    {
        public const int TierCount = 3;
        public const float FailAtOrBelow = 50f;   // 이 % 이하면 발동 실패
        public const float NormalAt = 75f;        // 이 %가 피해 ×1
        public const float PerfectAt = 90f;       // 이 % 이상이면 "완벽!" (쌍둥이 반지 마나 보상)
        public const float OuterRing = 0.86f;
        public const float InnerRing = 0.5f;
        private const float SampleStep = 0.02f;
        private const float PrecisionSlack = 1.2f; // 덜 샌 정도는 허용 폭보다 조금 너그럽게

        private static readonly float[] GlyphScale = { 0.52f, 0.42f, 0.30f };   // 룬 크기 (0~100 상자 → 반지름 비율)
        private static readonly float[] Tolerance = { 0.10f, 0.08f, 0.065f };   // 허용 폭 (판 반지름 비율)
        private static readonly float[] TimeLimit = { 3f, 4f, 6f };             // 제한 시간(초). 다 되면 그때까지 그린 것으로 판정
        private static readonly string[] TierNames = { "견습", "숙련", "대마법" };

        public static float GlyphScaleOf(int tier) => GlyphScale[ClampTier(tier)];
        public static float TimeLimitOf(int tier) => TimeLimit[ClampTier(tier)];
        public static string TierName(int tier) => TierNames[ClampTier(tier)];

        // ease: 수정 탑 "마법진 여유" 배율 보너스 (0.04 = +4%)
        public static float ToleranceOf(int tier, float ease = 0f) => Tolerance[ClampTier(tier)] * (1f + Math.Max(0f, ease));

        public static int ClampTier(int tier) => tier < 0 ? 0 : tier >= TierCount ? TierCount - 1 : tier;

        // 같은 속성 안 코스트 순위로 단계를 정한다. costs = 그 속성의 모든 공격 마법 코스트. 순풍이면 한 단계 쉬워진다.
        public static int TierFor(float cost, IList<float> costs, bool tailwind = false)
        {
            int rank = 0;
            if (costs != null)
                foreach (float c in costs)
                    if (c < cost) rank++;
            int tier = ClampTier(rank);
            return tailwind ? Math.Max(0, tier - 1) : tier;
        }

        // ---- 룬 (0~100 상자, 한붓) ----
        // element: 0 불, 1 물, 2 바람, 3 땅
        public static List<CirclePoint> RuneBox(int element)
        {
            switch (element)
            {
                case 0: // 火 느낌: 왼발에서 불길 꼭대기까지 솟았다가 오른발로 내려오고, 불티로 튀어 오름
                    return Join(
                        Quad(P(16, 88), P(42, 72), P(48, 46)),
                        Quad(P(48, 46), P(44, 22), P(56, 8)),
                        Quad(P(56, 8), P(58, 44), P(84, 88)),
                        Line(P(84, 88), P(74, 56), P(86, 40)));
                case 1: // 水 느낌: 오른쪽 물줄기를 타고 올라가 물결 기둥으로 떨어지고, 소용돌이쳐 왼쪽으로 빠짐
                    return Join(
                        Quad(P(88, 86), P(64, 72), P(64, 50)),
                        Quad(P(64, 50), P(64, 18), P(50, 8)),
                        Wave(50, 8, 82, 5, 1.5f),
                        Quad(P(50, 82), P(44, 96), P(32, 84)),
                        Quad(P(32, 84), P(20, 60), P(38, 44)),
                        Line(P(38, 44), P(14, 38)));
                case 2: // 风 느낌: 둥근 지붕을 넘어 안으로 말려 들어가는 회오리
                    return Join(
                        Quad(P(16, 88), P(12, 16), P(50, 14)),
                        Quad(P(50, 14), P(88, 16), P(82, 76)),
                        Quad(P(82, 76), P(78, 90), P(62, 70)),
                        Spiral(50, 58, 17, 3, 45, -330));
                default: // 土 느낌: 세모 지붕 꼭대기에서 기둥이 내려와 땅을 받치는 그릇으로
                    return Join(
                        Line(P(50, 18), P(24, 44), P(76, 44), P(50, 18), P(50, 72), P(20, 72)),
                        Quad(P(20, 72), P(50, 102), P(80, 72)),
                        Line(P(80, 72), P(88, 60)));
            }
        }

        // 속성 도형 꼭짓점 각도 (도, 0 = 오른쪽, 90 = 아래). 불 △, 물 ▽, 바람 육각형, 땅 사각형.
        public static float[] ShapeAngles(int element)
        {
            switch (element)
            {
                case 0: return new[] { -90f, 30f, 150f };
                case 1: return new[] { -150f, -30f, 90f };
                case 2: return new[] { -90f, -30f, 30f, 90f, 150f, 210f };
                default: return new[] { -135f, -45f, 45f, 135f };
            }
        }

        // 단계마다 마법진 전체를 한 획으로. 판 좌표 (가운데 0, 반지름 1).
        public static List<CirclePoint> Path(int element, int tier)
        {
            tier = ClampTier(tier);
            float h = GlyphScale[tier];
            var rune = new List<CirclePoint>();
            foreach (var p in RuneBox(element)) rune.Add(new CirclePoint((p.X - 50f) / 50f * h, (p.Y - 50f) / 50f * h));
            if (tier == 0) return rune;

            float a = Deg(rune[0]);
            if (tier == 1) return Join(Ring(OuterRing, a), rune); // 바깥 원 한 바퀴 → 바큇살로 룬 시작점

            // 룬 시작점에 가장 가까운 도형 꼭짓점에서 시작: 바깥 원 → 도형 → 안쪽 원 → 룬
            var angles = ShapeAngles(element);
            int k = 0;
            for (int i = 1; i < angles.Length; i++)
                if (AngleGap(angles[i], a) < AngleGap(angles[k], a)) k = i;
            var shape = new List<CirclePoint>();
            for (int i = 0; i <= angles.Length; i++) shape.Add(Polar(OuterRing, angles[(k + i) % angles.Length]));
            float v = angles[k];
            return Join(Ring(OuterRing, v), shape, Ring(InnerRing, v), rune);
        }

        // ---- 판정 ----

        public struct Result
        {
            public float Coverage;   // 목표 선 중 허용 폭 안에서 지나간 비율 (0~1)
            public float Precision;  // 그린 선 중 목표 선 근처에 있는 비율 (0~1)
            public int Percent;      // 일치율 (0~100)
            public bool Failed => Percent <= FailAtOrBelow;
            public float Power => DamageMultiplier(Percent);
        }

        public static Result Score(IList<CirclePoint> target, IList<CirclePoint> drawn, float tolerance)
        {
            var result = new Result();
            if (target == null || target.Count < 2 || drawn == null || drawn.Count < 2) return result;
            var t = Resample(target, SampleStep);
            var u = Resample(drawn, SampleStep);
            int hit = 0;
            foreach (var p in t) if (Distance(p, drawn) <= tolerance) hit++;
            int near = 0;
            foreach (var p in u) if (Distance(p, target) <= tolerance * PrecisionSlack) near++;
            result.Coverage = (float)hit / t.Count;
            result.Precision = (float)near / u.Count;
            result.Percent = (int)Math.Round(result.Coverage * result.Precision * 100f);
            return result;
        }

        // 일치율 → 피해 배율. 50% 이하 0 (실패), 그 위는 일치율 ÷ 75% (75% = ×1, 100% = ×1.33).
        public static float DamageMultiplier(int percent) => percent <= FailAtOrBelow ? 0f : percent / NormalAt;

        // 결과 한 마디
        public static string Verdict(int percent) =>
            percent <= FailAtOrBelow ? "실패" : percent >= PerfectAt ? "완벽!" : percent >= 70 ? "발동!" : "약하게 발동";

        // 점과 꺾은선 사이 가장 짧은 거리
        public static float Distance(CirclePoint p, IList<CirclePoint> path)
        {
            float best = float.MaxValue;
            if (path.Count == 1) return Dist(p, path[0]);
            for (int i = 1; i < path.Count; i++)
            {
                float d = SegmentDistance(p, path[i - 1], path[i]);
                if (d < best) best = d;
            }
            return best;
        }

        public static List<CirclePoint> Resample(IList<CirclePoint> path, float step)
        {
            var output = new List<CirclePoint>();
            if (path.Count == 0) return output;
            output.Add(path[0]);
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                float d = Dist(a, b);
                int n = Math.Max(1, (int)Math.Ceiling(d / step));
                for (int k = 1; k <= n; k++) output.Add(new CirclePoint(a.X + (b.X - a.X) * k / n, a.Y + (b.Y - a.Y) * k / n));
            }
            return output;
        }

        // 꺾은선 길이 (별똥별 안내·테스트용)
        public static float Length(IList<CirclePoint> path)
        {
            float sum = 0f;
            for (int i = 1; i < path.Count; i++) sum += Dist(path[i - 1], path[i]);
            return sum;
        }

        // 꺾은선 위 거리 d 지점 (0 ~ Length)
        public static CirclePoint PointAt(IList<CirclePoint> path, float d)
        {
            if (path.Count == 0) return new CirclePoint(0f, 0f);
            if (d <= 0f) return path[0];
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Dist(path[i - 1], path[i]);
                if (d <= seg && seg > 0f)
                {
                    float f = d / seg;
                    return new CirclePoint(path[i - 1].X + (path[i].X - path[i - 1].X) * f, path[i - 1].Y + (path[i].Y - path[i - 1].Y) * f);
                }
                d -= seg;
            }
            return path[path.Count - 1];
        }

        // ---- 도우미 ----

        private static CirclePoint P(float x, float y) => new CirclePoint(x, y);

        private static List<CirclePoint> Line(params CirclePoint[] points) => new List<CirclePoint>(points);

        private static List<CirclePoint> Quad(CirclePoint a, CirclePoint c, CirclePoint b, int n = 14)
        {
            var list = new List<CirclePoint>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, u = 1f - t;
                list.Add(new CirclePoint(u * u * a.X + 2f * u * t * c.X + t * t * b.X, u * u * a.Y + 2f * u * t * c.Y + t * t * b.Y));
            }
            return list;
        }

        private static List<CirclePoint> Wave(float x, float y0, float y1, float amp, float waves, int n = 30)
        {
            var list = new List<CirclePoint>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                list.Add(new CirclePoint(x + (float)Math.Sin(t * waves * Math.PI * 2) * amp, y0 + (y1 - y0) * t));
            }
            return list;
        }

        private static List<CirclePoint> Spiral(float cx, float cy, float r0, float r1, float a0, float a1, int n = 32)
        {
            var list = new List<CirclePoint>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, r = r0 + (r1 - r0) * t;
                double a = (a0 + (a1 - a0) * t) * Math.PI / 180.0;
                list.Add(new CirclePoint(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r));
            }
            return list;
        }

        private static List<CirclePoint> Ring(float r, float startDeg, int n = 72)
        {
            var list = new List<CirclePoint>(n + 1);
            for (int i = 0; i <= n; i++) list.Add(Polar(r, startDeg + 360f * i / n));
            return list;
        }

        private static CirclePoint Polar(float r, float deg)
        {
            double a = deg * Math.PI / 180.0;
            return new CirclePoint((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
        }

        private static List<CirclePoint> Join(params List<CirclePoint>[] parts)
        {
            var list = new List<CirclePoint>();
            foreach (var part in parts)
                for (int i = list.Count == 0 ? 0 : 1; i < part.Count; i++) list.Add(part[i]);
            return list;
        }

        private static float Deg(CirclePoint p) => (float)(Math.Atan2(p.Y, p.X) * 180.0 / Math.PI);

        private static float AngleGap(float a, float b)
        {
            float d = ((a - b) % 360f + 540f) % 360f - 180f;
            return Math.Abs(d);
        }

        private static float Dist(CirclePoint a, CirclePoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static float SegmentDistance(CirclePoint p, CirclePoint a, CirclePoint b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, len = dx * dx + dy * dy;
            float t = len > 0f ? ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float x = a.X + t * dx - p.X, y = a.Y + t * dy - p.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }
    }
}
