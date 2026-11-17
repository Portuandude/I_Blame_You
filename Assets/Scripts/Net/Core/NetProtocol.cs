namespace IBlameYou.Net
{
    // 호스트 방식 온라인 2인 (Docs/OnlinePlay.md): 호스트 컴퓨터가 게임 전체를 돌리고 P1(이동)을 맡는다.
    // 친구 컴퓨터(클라이언트)는 P2(공격) 입력을 보내고, 호스트가 보낸 그림·HUD·소리를 그대로 보여 준다.
    public static class NetProtocol
    {
        // 메시지 모양이 바뀌면 올린다. 다르면 접속을 거절한다 (두 사람이 같은 빌드를 써야 한다).
        public const ushort Version = 1;
        public const int DefaultPort = 27777;
    }

    // 메시지 첫 바이트
    public enum MsgType : byte
    {
        Hello = 1,      // 클라이언트 → 호스트: 버전, 이름
        Welcome = 2,    // 호스트 → 클라이언트: 받아 줌/거절 + 이유
        P2Input = 3,    // 클라이언트 → 호스트: P2 마우스·키 상태
        Texture = 10,   // 호스트 → 클라이언트: 그림 한 장 (PNG)
        Sprite = 11,    // 호스트 → 클라이언트: 그림 안의 스프라이트 영역
        World = 12,     // 호스트 → 클라이언트: 카메라 + 바뀐 그림 조각들
        Hud = 13,       // 호스트 → 클라이언트: 한 프레임의 HUD 그리기 명령
        Audio = 14,     // 호스트 → 클라이언트: 효과음·배경음 사건
        ResetView = 15, // 호스트 → 클라이언트: 그림 표를 비우고 처음부터 (새 접속)
        Compressed = 20, // 안에 다른 메시지 하나가 deflate로 줄어 들어 있다 (NetCompression)
    }

    public static class HelloMessage
    {
        public static byte[] Write(string name)
        {
            var w = new NetWriter(32);
            w.WriteByte((byte)MsgType.Hello);
            w.WriteUInt16(NetProtocol.Version);
            w.WriteString(name);
            return w.ToArray();
        }

        public static void Read(NetReader r, out ushort version, out string name)
        {
            version = r.ReadUInt16();
            name = r.ReadString();
        }
    }

    public static class WelcomeMessage
    {
        public static byte[] Write(bool accepted, string reason)
        {
            var w = new NetWriter(32);
            w.WriteByte((byte)MsgType.Welcome);
            w.WriteUInt16(NetProtocol.Version);
            w.WriteBool(accepted);
            w.WriteString(reason);
            return w.ToArray();
        }

        public static void Read(NetReader r, out ushort version, out bool accepted, out string reason)
        {
            version = r.ReadUInt16();
            accepted = r.ReadBool();
            reason = r.ReadString();
        }
    }
}
