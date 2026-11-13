using System;
using IBlameYou.Systems;

namespace IBlameYou.Player
{
    // 스킬 성공/실패, 마나 부족 같은 알림을 HUD로 보낸다. "누가 마나를 날렸는지"가 서로 탓하기의 재료다.
    public static class SkillMessages
    {
        public static event Action<ManaUser, string, bool> Posted; // (누가, 내용, 실패/경고 여부)

        private const float FizzleCooldown = 0.2f; // 실패 키를 연타해도 "푸슉"이 겹겹이 쌓이지 않게
        private static float lastFizzleTime = float.NegativeInfinity;

        public static void Post(ManaUser who, string text, bool bad = false)
        {
            Posted?.Invoke(who, text, bad);
            // 빨간 실패 알림(마나 부족·순간이동 불가·영창 오타 등)은 낮게 꺼지는 "푸슉" 소리를 같이 낸다 (Tools/AudioGen Fizzle)
            if (bad && UnityEngine.Time.unscaledTime - lastFizzleTime >= FizzleCooldown)
            {
                lastFizzleTime = UnityEngine.Time.unscaledTime;
                GameAudio.PlaySfx("Fizzle", 0.7f);
            }
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
