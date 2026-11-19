namespace IBlameYou.Net
{
    // 연결 상태 지키기 (Docs/OnlinePlay.md "지연 표시·끊김 감지").
    //   호스트가 1초마다 Ping(자기 시각 + 지금까지 잰 왕복 지연)을 보내고, 친구는 받은 시각을 그대로 Pong으로 돌려준다.
    //   호스트는 돌아온 시각으로 왕복 지연을 재고, 친구는 Ping 안의 값을 그대로 보여 준다 (두 컴퓨터 시계를 맞출 필요가 없다).
    //   어느 쪽이든 상대에게서 아무 메시지도 Timeout초 동안 오지 않으면 연결이 죽은 것으로 보고 끊는다.
    //   (친구 컴퓨터가 꺼지거나 인터넷이 끊기면 TCP는 한참 동안 알아채지 못해서, 친구가 다시 들어오려 해도
    //    호스트가 "이미 한 명 있음"으로 막고 있었다.)
    // 시각은 모두 초 단위 (Unity의 Time.unscaledTime — 일시정지와 상관없이 흐른다). 순수 C# — EditMode 테스트.
    public sealed class PingTracker
    {
        public const float Interval = 1f;
        public const float Timeout = 10f;
        private const float Smoothing = 0.3f; // 새 측정값 비중 (튀는 값 하나에 표시가 출렁이지 않게)

        private float lastSent;
        private float lastHeard;
        private bool started;

        // 왕복 지연 (밀리초). 아직 못 쟀으면 -1.
        public int RttMs { get; private set; } = -1;

        public void Reset(float now)
        {
            started = true;
            lastHeard = now;
            lastSent = now - Interval; // 바로 첫 Ping을 보낸다
            RttMs = -1;
        }

        // 상대에게서 무엇이든 받았다
        public void Heard(float now)
        {
            if (!started) Reset(now);
            lastHeard = now;
        }

        public float SilentFor(float now) => started ? now - lastHeard : 0f;

        public bool TimedOut(float now) => started && now - lastHeard > Timeout;

        // 호스트: 지금 Ping을 보낼 때인가
        public bool ShouldSendPing(float now)
        {
            if (!started) Reset(now);
            if (now - lastSent < Interval) return false;
            lastSent = now;
            return true;
        }

        // 호스트: Pong이 돌아왔다 (sentAt = 그 Ping을 보낸 내 시각)
        public void PongReceived(float sentAt, float now)
        {
            float rtt = (now - sentAt) * 1000f;
            if (rtt < 0f || rtt > Timeout * 1000f) return; // 이전 연결에서 온 값 등 말이 안 되는 값
            int sample = (int)(rtt + 0.5f);
            RttMs = RttMs < 0 ? sample : (int)(RttMs + (sample - RttMs) * Smoothing + 0.5f);
        }

        // 친구: 호스트가 Ping에 실어 보낸 값을 그대로 쓴다
        public void SetRemoteRtt(int rttMs) => RttMs = rttMs;
    }

    public static class PingMessage
    {
        // 호스트 → 친구: 보낸 시각, 지금까지 잰 왕복 지연(ms, 모르면 -1)
        public static byte[] Write(float sentAt, int rttMs)
        {
            var w = new NetWriter(12);
            w.WriteByte((byte)MsgType.Ping);
            w.WriteFloat(sentAt);
            w.WriteVarInt(rttMs);
            return w.ToArray();
        }

        public static void Read(NetReader r, out float sentAt, out int rttMs)
        {
            sentAt = r.ReadFloat();
            rttMs = r.ReadVarInt();
        }
    }

    public static class PongMessage
    {
        // 친구 → 호스트: 받은 Ping의 시각을 그대로
        public static byte[] Write(float sentAt)
        {
            var w = new NetWriter(8);
            w.WriteByte((byte)MsgType.Pong);
            w.WriteFloat(sentAt);
            return w.ToArray();
        }

        public static float Read(NetReader r) => r.ReadFloat();
    }
}
