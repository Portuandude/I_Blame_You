using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 챕터별 방 배치를 생성한다: 메인 경로(n개) + 곁가지 방(n+@)을 랜덤으로, 외선형(분기형)으로 뻗어나가게 한다.
    public static class RoomGenerator
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        public static ChapterMap Generate(int mainPathLength, int extraRoomCount, int seed)
        {
            var rng = new System.Random(seed);
            var rooms = new Dictionary<Vector2Int, RoomNode>();
            var mainPath = new List<Vector2Int>();

            var start = Vector2Int.zero;
            rooms[start] = new RoomNode(start, RoomType.Start);
            mainPath.Add(start);

            GrowMainPath(rooms, mainPath, mainPathLength, rng);
            AssignMainPathRoomTypes(rooms, mainPath, rng);
            GrowBranchRooms(rooms, extraRoomCount, rng);

            return new ChapterMap(rooms, start);
        }

        private static void GrowMainPath(Dictionary<Vector2Int, RoomNode> rooms, List<Vector2Int> mainPath, int mainPathLength, System.Random rng)
        {
            int guard = 0;
            while (mainPath.Count < mainPathLength && guard < mainPathLength * 50)
            {
                guard++;

                var from = FindCellWithFreeNeighbor(mainPath, rooms, rng, preferLast: true);
                if (!from.HasValue) break;

                var freeDirs = GetFreeDirections(from.Value, rooms);
                var next = from.Value + freeDirs[rng.Next(freeDirs.Count)];

                rooms[next] = new RoomNode(next, RoomType.Normal);
                mainPath.Add(next);
            }
        }

        private static void AssignMainPathRoomTypes(Dictionary<Vector2Int, RoomNode> rooms, List<Vector2Int> mainPath, System.Random rng)
        {
            if (mainPath.Count == 0) return;

            // mainPath[0]은 Start로 이미 지정되어 있으므로 마지막 방만 챕터보스로 바꾼다.
            if (mainPath.Count > 1)
            {
                rooms[mainPath[mainPath.Count - 1]].Type = RoomType.ChapterBoss;
            }

            var middlePositions = mainPath.Skip(1).Take(Math.Max(mainPath.Count - 2, 0)).ToList();
            if (middlePositions.Count == 0) return;

            int miniBossIndex = Mathf.Clamp(Mathf.RoundToInt(middlePositions.Count * 0.7f), 0, middlePositions.Count - 1);
            rooms[middlePositions[miniBossIndex]].Type = RoomType.MiniBoss;

            var eliteCandidates = middlePositions.Where(pos => rooms[pos].Type == RoomType.Normal).ToList();
            if (eliteCandidates.Count > 0)
            {
                rooms[eliteCandidates[rng.Next(eliteCandidates.Count)]].Type = RoomType.Elite;
            }
        }

        private static void GrowBranchRooms(Dictionary<Vector2Int, RoomNode> rooms, int extraRoomCount, System.Random rng)
        {
            int added = 0;
            int guard = 0;
            while (added < extraRoomCount && guard < extraRoomCount * 50)
            {
                guard++;

                var existingCells = rooms.Keys.ToList();
                var from = FindCellWithFreeNeighbor(existingCells, rooms, rng, preferLast: false);
                if (!from.HasValue) break;

                var freeDirs = GetFreeDirections(from.Value, rooms);
                var next = from.Value + freeDirs[rng.Next(freeDirs.Count)];

                var type = rng.NextDouble() < 0.3 ? RoomType.Puzzle : RoomType.Normal;
                rooms[next] = new RoomNode(next, type);
                added++;
            }
        }

        private static Vector2Int? FindCellWithFreeNeighbor(List<Vector2Int> candidates, Dictionary<Vector2Int, RoomNode> rooms, System.Random rng, bool preferLast)
        {
            if (preferLast && candidates.Count > 0)
            {
                var last = candidates[candidates.Count - 1];
                if (GetFreeDirections(last, rooms).Count > 0) return last;
            }

            var shuffled = candidates.OrderBy(_ => rng.Next()).ToList();
            foreach (var cell in shuffled)
            {
                if (GetFreeDirections(cell, rooms).Count > 0) return cell;
            }

            return null;
        }

        private static List<Vector2Int> GetFreeDirections(Vector2Int cell, Dictionary<Vector2Int, RoomNode> rooms)
        {
            return Directions.Where(dir => !rooms.ContainsKey(cell + dir)).ToList();
        }
    }
}
