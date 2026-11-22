namespace IBlameYou.Net
{
    // 참가 주소 칸 읽기: "192.168.0.12" 또는 호스트 창 "주소 복사"가 넣는 "192.168.0.12:27777".
    // 포트가 붙어 있으면 그 포트, 없으면 포트 칸 값. 순수 C# — EditMode 테스트 (NetAddressTests).
    public static class NetAddress
    {
        public static bool TryParse(string text, int fallbackPort, out string host, out int port)
        {
            host = null;
            port = fallbackPort;
            if (text == null) return false;
            string t = text.Trim();
            if (t.Length == 0) return false;
            int colon = t.LastIndexOf(':');
            // "[::1]:27777" 꼴 IPv6 + 포트 (2026-11-22)
            if (t[0] == '[')
            {
                int close = t.IndexOf(']');
                if (close <= 1) return false;
                string rest = t.Substring(close + 1).Trim();
                if (rest.Length > 0)
                {
                    if (rest[0] != ':' || !int.TryParse(rest.Substring(1).Trim(), out int p6) || p6 <= 0 || p6 >= 65536) return false;
                    port = p6;
                }
                host = t.Substring(1, close - 1).Trim();
                return host.Length > 0;
            }
            if (t[0] == ':' && t.IndexOf(':', 1) < 0) return false; // 주소 없이 포트만 (":27777"). "::1"은 IPv6라 통과
            // 콜론이 하나뿐일 때만 "주소:포트"로 본다 (IPv6 주소는 콜론이 여럿 — 그대로 둔다)
            if (colon > 0 && t.IndexOf(':') == colon)
            {
                string portText = t.Substring(colon + 1).Trim();
                if (!int.TryParse(portText, out int p) || p <= 0 || p >= 65536) return false;
                port = p;
                t = t.Substring(0, colon).Trim();
                if (t.Length == 0) return false;
            }
            host = t;
            return true;
        }
    }
}
