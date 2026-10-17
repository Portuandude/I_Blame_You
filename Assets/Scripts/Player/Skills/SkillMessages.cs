using System;
using IBlameYou.Systems;

namespace IBlameYou.Player
{
    // 스킬 성공/실패, 마나 부족 같은 알림을 HUD로 보낸다. "누가 마나를 날렸는지"가 서로 탓하기의 재료다.
    public static class SkillMessages
    {
        public static event Action<ManaUser, string, bool> Posted; // (누가, 내용, 실패/경고 여부)

        public static void Post(ManaUser who, string text, bool bad = false)
        {
            Posted?.Invoke(who, text, bad);
        }

        public static string RoleName(ManaUser who)
        {
            switch (who)
            {
                case ManaUser.PlayerA: return "A";
                case ManaUser.PlayerB: return "B";
                default: return "-";
            }
        }
    }
}
