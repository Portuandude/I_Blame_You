using System.Collections.Generic;
using IBlameYou.Systems;

namespace IBlameYou.Enemies
{
    // 화면 위쪽 보스 체력바가 읽는 값. 보스가 분열하면 여러 몸의 체력을 합쳐서 보여준다.
    public static class BossEvents
    {
        private static readonly List<HealthSystem> bodies = new List<HealthSystem>();

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
            if (body != null && !bodies.Contains(body)) bodies.Add(body);
        }

        public static void Unregister(HealthSystem body)
        {
            bodies.Remove(body);
            bodies.RemoveAll(b => b == null);
            if (bodies.Count == 0) Notice = null;
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
                float sum = 0f;
                foreach (var body in bodies) if (body != null) sum += body.Max;
                return sum;
            }
        }
    }
}
