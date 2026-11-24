using System.Collections.Generic;
using IBlameYou.Systems;

namespace IBlameYou.Enemies
{
    // 화면 위쪽 보스 체력바가 읽는 값. 보스가 분열하면 여러 몸의 체력을 합쳐서 보여준다.
    public static class BossEvents
    {
        private static readonly List<HealthSystem> bodies = new List<HealthSystem>();
        // 싸움 도중 먼저 치워진 몸(쓰러진 쌍둥이 기사·슬라임 킹 반쪽·와르그)의 최대 체력. 시체가 치워질 때 합계에서 빠지면
        // 남은 몸의 바가 다시 가득 찬 것처럼 튀므로 (버그 점검 2026-11-24) 마지막 몸이 빠질 때까지 최대 체력에 남겨 둔다.
        private static float removedMax;

        public static string Name { get; private set; }
        public static string Notice { get; set; } // 체력바 아래 한 줄 (합체까지 남은 시간 등)

        public static bool Active
        {
            get
            {
                bodies.RemoveAll(b => b == null);
                return bodies.Count > 0;
            }
        }

        public static int AliveCount
        {
            get
            {
                int count = 0;
                foreach (var body in bodies) if (body != null && !body.IsDead) count++;
                return count;
            }
        }

        public static IReadOnlyList<HealthSystem> Bodies => bodies;

        public static void Register(string name, HealthSystem body)
        {
            Name = name;
            bodies.RemoveAll(b => b == null);
            if (bodies.Count == 0) removedMax = 0f; // 새 보스 싸움
            if (body != null && !bodies.Contains(body)) bodies.Add(body);
        }

        public static void Unregister(HealthSystem body)
        {
            bool had = body != null && bodies.Remove(body);
            bodies.RemoveAll(b => b == null);
            if (bodies.Count == 0)
            {
                Notice = null;
                removedMax = 0f;
            }
            else if (had) removedMax += body.Max; // 남은 몸이 있으면 바 비율이 유지되게
        }

        public static float Current
        {
            get
            {
                float sum = 0f;
                foreach (var body in bodies) if (body != null) sum += body.Current;
                return sum;
            }
        }

        public static float Max
        {
            get
            {
                float sum = removedMax;
                foreach (var body in bodies) if (body != null) sum += body.Max;
                return sum;
            }
        }
    }
}
