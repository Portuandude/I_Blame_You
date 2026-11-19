namespace IBlameYou.Net
{
    // 친구(클라이언트)가 판 도중 연결을 잃으면 같은 호스트에 저절로 다시 붙는다 (Docs/OnlinePlay.md "다시 연결").
    //   한 번 호스트가 받아 준 주소만 기억한다 (처음 참가에 실패한 주소로는 다시 하지 않는다 — 주소가 틀렸을 수 있다).
    //   시도 사이 간격은 Delays 순서. 다 합치면 30초가 넘어서, 호스트가 죽은 연결을 10초(PingTracker.Timeout) 뒤에야
    //   비우는 경우("이미 한 명 있음"으로 바로 닫힘)에도 그 뒤 시도로 들어간다.
    //   사람이 끊거나(F10·취소), 다른 방을 열거나 참가하거나, 버전이 달라 거절되면 잊는다.
    // 시각은 초 (Time.unscaledTime). 순수 C# — EditMode 테스트 (NetReconnectTests).
    public sealed class ReconnectPlan
    {
        public static readonly float[] Delays = { 1f, 2f, 3f, 4f, 5f, 5f, 5f, 5f };
        public static int MaxAttempts => Delays.Length;

        private float nextAt;

        public string Address { get; private set; }
        public int Port { get; private set; }
        public bool HasTarget => Address != null;

        // 지금까지 시작한(또는 기다리는) 시도 번호, 1부터. 연결돼 있으면 0.
        public int Attempt { get; private set; }

        // 다음 시도를 기다리는 중
        public bool Waiting { get; private set; }

        // 끊긴 뒤 다시 붙으려고 하는 중 (기다리거나 시도 중)
        public bool InProgress => HasTarget && Attempt > 0;

        // 호스트가 받아 줬다: 이 주소를 기억하고 시도 횟수를 비운다
        public void Remember(string address, int port)
        {
            Address = address;
            Port = port;
            Attempt = 0;
            Waiting = false;
        }

        public void Forget()
        {
            Address = null;
            Port = 0;
            Attempt = 0;
            Waiting = false;
        }

        // 연결을 잃었거나 다시 붙기 시도가 실패했다. 다음 시도를 잡았으면 true.
        // 기억한 주소가 없거나 시도를 다 썼으면 false (다 썼으면 잊는다).
        public bool Lost(float now)
        {
            if (!HasTarget) return false;
            if (Attempt >= Delays.Length)
            {
                Forget();
                return false;
            }
            nextAt = now + Delays[Attempt];
            Attempt++;
            Waiting = true;
            return true;
        }

        // 기다리던 시도를 지금 시작할 때인가. true면 Waiting이 풀린다 (한 번만 true).
        public bool Due(float now)
        {
            if (!Waiting || now < nextAt) return false;
            Waiting = false;
            return true;
        }

        public float SecondsLeft(float now) => Waiting ? System.Math.Max(0f, nextAt - now) : 0f;
    }
}
