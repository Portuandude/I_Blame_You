using System;

namespace IBlameYou.Systems
{
    // 스테이지 진행 상황을 HUD에 알린다 (진행도, 안내 문구, 클리어).
    public static class StageEvents
    {
        public static event Action<string> Notice;
        public static event Action Cleared;

        public static float Progress { get; set; }   // 0 ~ 1 (플레이어가 스테이지 가로 길이의 어디쯤인지)
        public static string StageName { get; set; } = "스테이지 1";
        // 이번 런의 목표 시간과 방 수 (클리어 결과 창의 목표 시간 비교, ClearPace). 외길 청크 스테이지면 방 수 0.
        public static float TargetSeconds { get; set; }
        public static int MainRooms { get; set; }
        public static int SideRooms { get; set; }

        public static void PostNotice(string text) => Notice?.Invoke(text);

        public static void RaiseCleared() => Cleared?.Invoke();
    }

    // 분열 같은 효과로 새로 생긴 적을 같은 묶음(전투 구역 등)에 넣는다.
    public interface IEnemyGroup
    {
        void RegisterEnemy(HealthSystem enemy);
    }
}
