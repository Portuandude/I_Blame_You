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
            if (t[0] == ':') return false; // 주소 없이 포트만
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
