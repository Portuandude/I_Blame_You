using System.Collections.Generic;

namespace IBlameYou.Net
{
    // 호스트 창 주소 목록 순서 (Docs/OnlinePlay.md "인터넷 너머 친구와 하기").
    // 친구가 다른 집에 있으면 가상 LAN 주소를 써야 해서, 그것을 맨 앞에 두고 "주소 복사"도 그것을 복사한다.
    //   가상 LAN: Radmin VPN 26.x · Hamachi 25.x · Tailscale 100.64~127.x(CGNAT) · ZeroTier 흔한 대역 10.147.x/10.144.x
    //   그다음 집 네트워크 192.168.x · 172.16~31.x · 10.x, 나머지는 뒤.
    // 순수 C# — EditMode 테스트 (NetAddressTests).
    public static class AddressRanking
    {
        public static bool IsVirtualLan(string ip)
        {
            if (!TryOctets(ip, out int a, out int b)) return false;
            if (a == 26 || a == 25) return true;
            if (a == 100 && b >= 64 && b <= 127) return true;
            if (a == 10 && (b == 147 || b == 144)) return true;
            return false;
        }

        public static bool IsHomeLan(string ip)
        {
            if (!TryOctets(ip, out int a, out int b)) return false;
            if (a == 192 && b == 168) return true;
            if (a == 172 && b >= 16 && b <= 31) return true;
            return a == 10;
        }

        private static int Rank(string ip) => IsVirtualLan(ip) ? 0 : IsHomeLan(ip) ? 1 : 2;

        // 같은 등급 안에서는 원래 순서를 지킨다
        public static List<string> Sort(IList<string> addresses)
        {
            var result = new List<string>(addresses.Count);
            for (int rank = 0; rank <= 2; rank++)
                foreach (var ip in addresses)
                    if (Rank(ip) == rank) result.Add(ip);
            return result;
        }

        private static bool TryOctets(string ip, out int a, out int b)
        {
            a = b = -1;
            if (string.IsNullOrEmpty(ip)) return false;
            var parts = ip.Split('.');
            return parts.Length == 4 && int.TryParse(parts[0], out a) && int.TryParse(parts[1], out b);
        }
    }
}
