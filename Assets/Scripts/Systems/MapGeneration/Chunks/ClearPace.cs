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

        // 최근 클리어 기록 (15분 조정 판단용). 저장 문자열은 "걸린초,목표초,방수;..." (정수만이라 문화권 영향 없음), 새 기록이 맨 앞.
        public const int RecentMax = 5;

        // 새 기록을 맨 앞에 더하고 RecentMax개만 남긴 문자열. 걸린 시간이 0 이하면 그대로 돌려준다. 깨진 항목은 버린다.
        public static string AddRecent(string list, float clearSeconds, float targetSeconds, int rooms)
        {
            if (clearSeconds <= 0f) return list ?? "";
            var entries = new System.Collections.Generic.List<string>
            {
                $"{(int)Math.Floor(clearSeconds)},{(int)Math.Floor(Math.Max(0f, targetSeconds))},{Math.Max(0, rooms)}"
            };
            foreach (var e in ParseRecent(list))
            {
                if (entries.Count >= RecentMax) break;
                entries.Add($"{e.clear},{e.target},{e.rooms}");
            }
            return string.Join(";", entries);
        }

        // 시작 화면 한 줄. 예: "최근 3판 평균 14분 32초 (목표 15분 00초 · 방 평균 22개)". 기록이 없으면 빈 문자열.
        // 목표·방 수가 0인 기록(외길 모드 등)은 그 평균에서 뺀다.
        public static string RecentSummary(string list)
        {
            var entries = ParseRecent(list);
            if (entries.Count == 0) return "";
            long clear = 0, target = 0, rooms = 0;
            int targetCount = 0, roomCount = 0;
            foreach (var e in entries)
            {
                clear += e.clear;
                if (e.target > 0) { target += e.target; targetCount++; }
                if (e.rooms > 0) { rooms += e.rooms; roomCount++; }
            }
            var extra = new System.Collections.Generic.List<string>();
            if (targetCount > 0) extra.Add($"목표 {Format((int)(target / targetCount))}");
            if (roomCount > 0) extra.Add($"방 평균 {(int)Math.Round(rooms / (double)roomCount, MidpointRounding.AwayFromZero)}개");
            string tail = extra.Count > 0 ? $" ({string.Join(" · ", extra)})" : "";
            return $"최근 {entries.Count}판 평균 {Format((int)(clear / entries.Count))}{tail}";
        }

        private static System.Collections.Generic.List<(int clear, int target, int rooms)> ParseRecent(string list)
        {
            var result = new System.Collections.Generic.List<(int clear, int target, int rooms)>();
            if (string.IsNullOrEmpty(list)) return result;
            foreach (var entry in list.Split(';'))
            {
                var f = entry.Split(',');
                if (f.Length != 3) continue;
                if (!int.TryParse(f[0], out int c) || !int.TryParse(f[1], out int t) || !int.TryParse(f[2], out int r)) continue;
                if (c <= 0) continue;
                result.Add((c, t, r));
                if (result.Count >= RecentMax) break;
            }
            return result;
        }

        // SaveData.FormatTime과 같은 모양 ("N분 SS초").
        public static string Format(int seconds) => $"{seconds / 60}분 {seconds % 60:00}초";
    }
}
