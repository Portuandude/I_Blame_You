using System;

namespace IBlameYou.Systems
{
    // 스테이지 상황을 HUD에 알린다 (안내 문구, 클리어, 지금 방의 깊이).
    // 2026-11-12 사람 결정: 플레이어에게 보이는 진행도(%) 시스템은 없앴다. 스테이지는 방을 돌아다니며 탐험한다.
    public static class StageEvents
    {
        public static event Action<string> Notice;
        public static event Action Cleared;

        // 지금 방의 깊이 0 ~ 1 = 시작 방에서의 방 거리 / 보스 방 거리 (= RoomData.Difficulty). 화면에는 보이지 않는 내부 값으로,
        // 배경 색조·후반 곡(ArenaZone.LateMusicDepth)이 쓴다. 외길 청크 스테이지(기능 테스트 등)는 가로 위치 비율.
        public static float Depth { get; set; }
        // 지금 방이 시작 방에서 문 몇 개 거리인지와 이번 판에 가 본 가장 먼 거리 (게임오버 창 "쓰러진 곳"). 외길 모드면 -1.
        public static int RoomDistance { get; set; } = -1;
        public static int FarthestDistance { get; set; } = -1;
        public static string StageName { get; set; } = "스테이지 1";
        // 이번 런의 목표 시간과 방 수 (클리어 결과 창의 목표 시간 비교, ClearPace). 외길 청크 스테이지면 방 수 0.
        public static float TargetSeconds { get; set; }
        public static int MainRooms { get; set; }
        public static int SideRooms { get; set; }
        // 마을 방에 들어온 횟수 (식당 "한 방문에 종류별 한 번"의 방문 번호, ChunkStreamer가 마을 방에 들어올 때 올린다).
        public static int VillageVisit { get; set; }
        // 지금 방이 곁방인지 (게임오버 결과 창 "쓰러진 곳").
        public static bool InSideRoom { get; set; }

        public static void PostNotice(string text) => Notice?.Invoke(text);

        public static void RaiseCleared() => Cleared?.Invoke();

        // 마을 건물 앞에서 W: 식당·상점 창을 여는 쪽(HUD)이 등록한다. 창을 열었으면 true. 아무도 없으면 false (VillageBuilding이 "곧 열려요" 알림).
        public static Func<VillageService, bool> VillageInteractRequested;

        public static bool RaiseVillageInteract(VillageService service) =>
            VillageInteractRequested != null && VillageInteractRequested(service);
    }

    // 분열 같은 효과로 새로 생긴 적을 같은 묶음(전투 구역 등)에 넣는다.
    public interface IEnemyGroup
    {
        void RegisterEnemy(HealthSystem enemy);
    }
}
