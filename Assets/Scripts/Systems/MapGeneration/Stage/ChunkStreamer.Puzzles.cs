using System;
using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // ChunkStreamer 나눈 파일: 퍼즐 방 불러오기 (등불 순서·거울 룬). 바람 상자는 Load의 기능 switch에서 만든다.
    public partial class ChunkStreamer
    {
        // 퍼즐 방 "등불 순서"·"거울 룬" (Docs/PuzzleRoomDesign.md): 등불 셋 + 벽화(또는 거울). 셋을 순서대로 켜면 상자 자리에 유물 상자가 떨어진다.
        // 푼 방은 다시 와도 등불이 켜진 채이고, 유물을 안 골랐으면 상자가 남는다 (SpawnRelicChest가 takenRelics를 본다).
        private void LoadLanternPuzzle(int index, Transform root, ChunkData chunk, float ox, float oy)
        {
            List<IBlameYou.Puzzles.LanternPuzzle.LanternSpec> specs = null;
            int lowest = int.MaxValue;
            Vector2 mural = Vector2.zero;
            bool mirror = false;
            Vector2? chest = null;
            foreach (var f in chunk.Features)
            {
                if (f.Kind == FeatureKind.Lantern) lowest = Mathf.Min(lowest, f.Y);
                else if (f.Kind == FeatureKind.LanternMural) mural = new Vector2(ox + f.X + 0.5f, oy + f.Y);
                else if (f.Kind == FeatureKind.MirrorMural) { mural = new Vector2(ox + f.X + 0.5f, oy + f.Y); mirror = true; } // 거울 룬
                else if (f.Kind == FeatureKind.PuzzleChest) chest = new Vector2(ox + f.X + 0.5f, oy + f.Y);
            }
            if (lowest == int.MaxValue) return;

            foreach (var f in chunk.Features)
            {
                if (f.Kind != FeatureKind.Lantern) continue;
                (specs ?? (specs = new List<IBlameYou.Puzzles.LanternPuzzle.LanternSpec>())).Add(new IBlameYou.Puzzles.LanternPuzzle.LanternSpec
                {
                    Bottom = new Vector2(ox + f.X + 0.5f, oy + f.Y),
                    Element = (Element)f.ElementIndex,
                    Order = f.Direction,
                    Rise = f.Y - lowest
                });
            }

            bool solved = solvedPuzzles.Contains(index);
            var chestAt = chest ?? mural;
            IBlameYou.Puzzles.LanternPuzzle.Create(root, specs, mural, solved, () =>
            {
                solvedPuzzles.Add(index);
                IBlameYou.Core.RunStats.AddHeartDust(HeartDustRule.Puzzle); // 퍼즐을 풀면 영혼석
                if (root != null) SpawnRelicChest(root, index, chestAt, true);
            }, mirror);
            if (solved) SpawnRelicChest(root, index, chestAt, false);
        }
    }
}
