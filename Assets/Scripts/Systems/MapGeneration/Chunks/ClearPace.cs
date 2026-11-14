using System;

namespace IBlameYou.Systems
{
    // 클리어 결과 창의 "목표 시간 비교" 한 줄 (사람 결정 2026-11-10: 스테이지 1 = 15분, 2026-11-12 15~30분·기본 목표 20분).
    // 15분 조정이 맞는지 플레이테스트에서 바로 보이게, 목표보다 빨랐는지(초록)·느렸는지(주황)와 이번 시드의 방 수를 적는다.
    // UnityEngine 없이 돌아서 EditMode 테스트로 고정한다. 색은 SkillHUD가 IsFast를 보고 고른다.
    public static class ClearPace
    {
        // 목표보다 빠르거나 같으면 true (초록). 목표가 없으면(0 이하) 비교하지 않으므로 false.
        public static bool IsFast(float clearSeconds, float targetSeconds) =>
            targetSeconds > 0f && clearSeconds <= targetSeconds;

        // 예: "목표 15분 00초보다 1분 23초 빠름 · 방 22개". 2026-11-14 사람 결정: "곁방" 구분 없이 격자의 방 수만 (주 경로·막다른 방 합).
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
            return $"{pace} · 방 {mainRooms + Math.Max(0, sideRooms)}개";
        }

        // 게임오버 결과 창의 "쓰러진 곳" 한 줄 (2026-11-12 진행도 % 삭제 → 시작 방에서의 방 거리).
        // 예: "쓰러진 곳: 시작 방에서 3칸 · 가장 멀리 4칸 · 버틴 시간 5분 12초". sideRoom은 2026-11-14부터 표시하지 않는다 (곁방 개념 없앰).
        // distance: 쓰러진 방이 시작 방에서 문 몇 개 거리인지 (방 격자가 아니면 -1 → 방 부분을 뺀다).
        // farthest: 이번 판에 가 본 가장 먼 방 거리. distance보다 클 때만 적는다.
        public static string DescribeDefeat(int distance, int farthest, bool sideRoom, float seconds)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (distance >= 0)
            {
                parts.Add(distance == 0 ? "시작 방" : $"시작 방에서 {distance}칸");
                if (farthest > distance) parts.Add($"가장 멀리 {farthest}칸");
            }
            if (seconds > 0f) parts.Add($"버틴 시간 {Format((int)Math.Floor(seconds))}");
            if (parts.Count == 0) return "";
            return distance >= 0 ? $"쓰러진 곳: {string.Join(" · ", parts)}" : string.Join(" · ", parts);
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

        // 최근 쓰러진 곳 기록 (어려운 구간 찾기용). 저장 문자열은 "가장먼방거리;..." (정수만), 새 기록이 맨 앞, RecentMax개.
        // 거리는 0~MaxDefeatDistance로 자른다. 예전 진행도(%) 기록과 섞이지 않게 SaveData가 새 저장 키를 쓴다.
        public const int MaxDefeatDistance = 99;

        public static string AddRecentDefeat(string list, int farthest)
        {
            var entries = new System.Collections.Generic.List<string> { Math.Max(0, Math.Min(MaxDefeatDistance, farthest)).ToString() };
            foreach (var d in ParseDefeats(list))
            {
                if (entries.Count >= RecentMax) break;
                entries.Add(d.ToString());
            }
            return string.Join(";", entries);
        }

        // 시작 화면 한 줄. 예: "최근 쓰러진 곳 3판 평균 시작 방에서 4칸까지". 기록이 없으면 빈 문자열.
        public static string RecentDefeatSummary(string list)
        {
            int average = RecentDefeatAverage(list);
            if (average < 0) return "";
            return $"최근 쓰러진 곳 {ParseDefeats(list).Count}판 평균 시작 방에서 {average}칸까지";
        }

        // 최근 쓰러진 판들이 간 가장 먼 방 거리의 평균 (반올림). 기록이 없으면 -1.
        public static int RecentDefeatAverage(string list)
        {
            var entries = ParseDefeats(list);
            if (entries.Count == 0) return -1;
            long sum = 0;
            foreach (var d in entries) sum += d;
            return (int)Math.Round(sum / (double)entries.Count, MidpointRounding.AwayFromZero);
        }

        // 게임오버 창 "이번 판 vs 평소" 한 줄. usual은 이번 판을 기록하기 전의 평균(RecentDefeatAverage), -1이면 빈 문자열.
        // 예: "평소보다 2칸 더 갔다 (최근 평균 3칸)" / "평소보다 1칸 덜 갔다 (최근 평균 4칸)" / "평소만큼 갔다 (최근 평균 3칸)".
        public static string CompareDefeat(int farthest, int usual)
        {
            if (usual < 0) return "";
            int diff = DefeatGap(farthest, usual);
            string average = $"(최근 평균 {Math.Min(MaxDefeatDistance, usual)}칸)";
            if (diff > 0) return $"평소보다 {diff}칸 더 갔다 {average}";
            if (diff < 0) return $"평소보다 {-diff}칸 덜 갔다 {average}";
            return $"평소만큼 갔다 {average}";
        }

        // 이번 판 가장 먼 방 거리 − 평소 평균. 양수면 더 갔다 (초록), 음수면 덜 갔다 (주황).
        public static int DefeatGap(int farthest, int usual) =>
            Math.Max(0, Math.Min(MaxDefeatDistance, farthest)) - Math.Max(0, Math.Min(MaxDefeatDistance, usual));

        private static System.Collections.Generic.List<int> ParseDefeats(string list)
        {
            var result = new System.Collections.Generic.List<int>();
            if (string.IsNullOrEmpty(list)) return result;
            foreach (var entry in list.Split(';'))
            {
                if (!int.TryParse(entry, out int d) || d < 0 || d > MaxDefeatDistance) continue;
                result.Add(d);
                if (result.Count >= RecentMax) break;
            }
            return result;
        }

        // SaveData.FormatTime과 같은 모양 ("N분 SS초").
        public static string Format(int seconds) => $"{seconds / 60}분 {seconds % 60:00}초";
    }
}
