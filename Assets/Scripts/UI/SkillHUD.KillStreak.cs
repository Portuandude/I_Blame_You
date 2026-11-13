using IBlameYou.Enemies;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 연속 처치 알림 (전투 손맛, 2026-11-12): 앞 처치에서 KillStreakWindow초 안에 다음 적을 쓰러뜨리면 이어진다.
    // 3마리째부터 마지막 적 머리 위에 "N연속!" 글자 + 오르는 세 음 Combo. 끊기면 다시 1부터.
    // 함정·종유석으로 쓰러진 적도 센다 (끌어들인 것도 플레이어 솜씨라서). 히트스톱으로 시간이 거의 멈춰도 창이 줄지 않게 게임 시간으로 잰다.
    public partial class SkillHUD
    {
        private const float KillStreakWindow = 0.6f;
        private const int KillStreakMin = 3;
        private static readonly Color KillStreakColor = new Color(1f, 0.55f, 0.85f); // 분홍 (치명타 금색·약점 노랑과 구분)

        private int killStreak;
        private float lastKillTime = -10f;

        private void CountKillStreak(EnemyController enemy)
        {
            float now = Time.time;
            // (도메인 리로드 없이 다시 플레이해 Time.time이 작아져도 이어지지 않게 now >= lastKillTime도 본다)
            bool chained = now >= lastKillTime && now - lastKillTime <= KillStreakWindow;
            killStreak = chained ? killStreak + 1 : 1;
            lastKillTime = now;
            if (killStreak < KillStreakMin || enemy == null) return;

            DamageNumbers.ShowText(enemy.transform.position + new Vector3(0f, 2.3f, 0f), $"{killStreak}연속!", KillStreakColor,
                Mathf.Min(1.1f + 0.1f * (killStreak - KillStreakMin), 1.6f));
            GameAudio.PlaySfx("Combo", 0.7f, 0f);
        }
    }
}
