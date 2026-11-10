using System;

namespace IBlameYou.Systems
{
    // 클리어 결과 창의 "목표 시간 비교" 한 줄 (사람 결정 2026-11-10: 스테이지 1 = 15분).
    // 15분 조정이 맞는지 플레이테스트에서 바로 보이게, 목표보다 빨랐는지(초록)·느렸는지(주황)와 이번 시드의 방 수를 적는다.
    // UnityEngine 없이 돌아서 EditMode 테스트로 고정한다. 색은 SkillHUD가 IsFast를 보고 고른다.
    public static class ClearPace
    {
        // 목표보다 빠르거나 같으면 true (초록). 목표가 없으면(0 이하) 비교하지 않으므로 false.
        public static bool IsFast(float clearSeconds, float targetSeconds) =>
            targetSeconds > 0f && clearSeconds <= targetSeconds;

        // 예: "목표 15분 00초보다 1분 23초 빠름 · 방 22개 (주 경로 19 · 곁방 3)".
        // 목표가 없으면 빈 문자열. 방 수가 0이면(예전 외길 청크 스테이지) 방 부분을 뺀다.
        public static string Describe(float clearSeconds, float targetSeconds, int mainRooms, int sideRooms)
        {
            if (targetSeconds <= 0f || clearSeconds <= 0f) return "";
            int clear = (int)Math.Floor(clearSeconds);
            int target = (int)Math.Floor(targetSeconds);
            int diff = Math.Abs(clear - target);
            string pace = diff == 0
                ? $"목표 {Format(target)} 딱 맞춤"
                : $"목표 {Format(target)}보다 {Format(diff)} {(clear < target ? "빠름" : "느림")}";
            if (mainRooms <= 0) return pace;
            return $"{pace} · 방 {mainRooms + Math.Max(0, sideRooms)}개 (주 경로 {mainRooms} · 곁방 {Math.Max(0, sideRooms)})";
        }

        // 게임오버 결과 창의 "쓰러진 곳" 한 줄. 예: "쓰러진 곳: 주 경로 7/19번째 방 (곁방) · 진행도 33% · 버틴 시간 5분 12초".
        // pathStep: 주 경로 몇 번째 방인지(1부터, 곁방이면 붙은 방 기준). mainRooms가 0이면(외길 청크 스테이지) 방 부분을 뺀다.
        public static string DescribeDefeat(int pathStep, int mainRooms, bool sideRoom, float progress, float seconds)
        {
            int percent = (int)Math.Round(Math.Max(0f, Math.Min(1f, progress)) * 100f, MidpointRounding.AwayFromZero);
            string where = mainRooms > 0 && pathStep > 0
                ? $"주 경로 {Math.Min(pathStep, mainRooms)}/{mainRooms}번째 방{(sideRoom ? " (곁방)" : "")} · 진행도 {percent}%"
                : $"진행도 {percent}%";
            string time = seconds > 0f ? $" · 버틴 시간 {Format((int)Math.Floor(seconds))}" : "";
            return $"쓰러진 곳: {where}{time}";
        }

        // SaveData.FormatTime과 같은 모양 ("N분 SS초").
        public static string Format(int seconds) => $"{seconds / 60}분 {seconds % 60:00}초";
    }
}
