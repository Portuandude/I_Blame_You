namespace IBlameYou.Net
{
    // 온라인 2인 보낸·받은 양 (초당 KB). 베타 때 "왜 끊기지?"를 볼 수 있게 상태 줄에 지연 옆에 보인다 (Docs/OnlinePlay.md).
    //   전송이 세는 누적 바이트(ITransport.BytesSent/BytesReceived)를 1초마다 한 번 읽어 그 사이 차이를 초당 값으로 바꾼다.
    // 시각은 초 (Time.unscaledTime). 순수 C# — EditMode 테스트 (NetTrafficTests).
    public sealed class TrafficMeter
    {
        public const float Window = 1f;

        private float windowStart;
        private long sentAtStart;
        private long receivedAtStart;
        private bool started;

        // 마지막 1초 동안 초당 바이트. 아직 1초가 안 지났으면 0.
        public float SentPerSecond { get; private set; }
        public float ReceivedPerSecond { get; private set; }

        public void Reset()
        {
            started = false;
            SentPerSecond = 0f;
            ReceivedPerSecond = 0f;
        }

        // 매 프레임 불러도 된다. 누적값이 줄었으면(새 전송) 기준을 다시 잡는다.
        public void Sample(float now, long sentTotal, long receivedTotal)
        {
            if (!started || sentTotal < sentAtStart || receivedTotal < receivedAtStart || now < windowStart)
            {
                started = true;
                windowStart = now;
                sentAtStart = sentTotal;
                receivedAtStart = receivedTotal;
                return;
            }
            float elapsed = now - windowStart;
            if (elapsed < Window) return;
            SentPerSecond = (sentTotal - sentAtStart) / elapsed;
            ReceivedPerSecond = (receivedTotal - receivedAtStart) / elapsed;
            windowStart = now;
            sentAtStart = sentTotal;
            receivedAtStart = receivedTotal;
        }

        // "120KB/s" 또는 "0.8MB/s"
        public static string Format(float bytesPerSecond)
        {
            float kb = bytesPerSecond / 1024f;
            if (kb >= 1024f) return (kb / 1024f).ToString("0.0") + "MB/s";
            return kb.ToString("0") + "KB/s";
        }
    }
}
