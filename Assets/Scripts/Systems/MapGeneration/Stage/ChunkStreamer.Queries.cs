using System;
using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // ChunkStreamer 나눈 파일: 미니맵·결과 창이 묻는 방 상태 (방 이벤트 수, 퍼즐·저주 상자·보물 고블린·동전 남음 여부).
    public partial class ChunkStreamer
    {
        // 결과 창 방 이벤트 줄 (2026-11-14 자동 실행): 이번 판에 만난(들어가 본) 방 이벤트와 해낸 수.
        // 매복 = 가 본 매복 전투 방 / 돌파한 것, 저주 상자 = 가 본 저주 상자 방 / 저주를 푼 것, 보물 고블린 = 나타난 것 / 잡은 것.
        public struct RoomEventTally
        {
            public int AmbushMet, AmbushCleared, CursedMet, CursedSolved, GoblinMet, GoblinCaught;
            public int PuzzleMet, PuzzleSolved; // 퍼즐 방 (등불 순서·거울 룬, 이중 스위치 제외): 가 본 것 / 푼 것
            public bool Any => AmbushMet + CursedMet + GoblinMet + PuzzleMet > 0;
        }

        private readonly HashSet<int> goblinsMet = new HashSet<int>();
        private readonly HashSet<int> goblinsCaught = new HashSet<int>();

        public RoomEventTally EventTally()
        {
            var tally = new RoomEventTally { GoblinMet = goblinsMet.Count, GoblinCaught = goblinsCaught.Count };
            if (rooms == null || stage == null) return tally;
            foreach (int index in visitedRooms)
            {
                if (index < 0 || index >= rooms.Rooms.Count || index >= stage.Chunks.Count) continue;
                var chunk = stage.Chunks[index];
                if (chunk.Kind == ChunkKind.Arena && !RoomShaper.IsMidBossArena(chunk)
                    && AmbushRule.IsAmbush(stage.Seed, index, RoomDepth(index), true))
                {
                    tally.AmbushMet++;
                    if (clearedArenas.Contains(index)) tally.AmbushCleared++;
                }
                if (IsCursedChestRoom(index))
                {
                    tally.CursedMet++;
                    if (cursedBroken.Contains(index)) tally.CursedSolved++;
                }
                if (IsDeductionPuzzle(chunk.Kind))
                {
                    tally.PuzzleMet++;
                    if (solvedPuzzles.Contains(index)) tally.PuzzleSolved++;
                }
            }
            return tally;
        }

        // 이 스테이지에 마을 방이 있으면 가 봤는지, 없으면(기능 테스트 빈 방 등) null.
        public bool? VillageFound
        {
            get
            {
                if (rooms == null) return null;
                foreach (var room in rooms.Rooms)
                    if (room.Kind == RoomKind.Village) return visitedRooms.Contains(room.Index);
                return null;
            }
        }
        // 미니맵 자물쇠: 아직 안 연 두 속성 봉인문(숲은 덩굴 벽일 수도)이 있는 보물 방
        public bool HasClosedPairGate(int index) =>
            rooms != null && index >= 0 && index < rooms.Rooms.Count && HasTreasureGate(index) && !openedGates.Contains(index);

        // 보물 방 상자 앞을 막는 것: 두 속성 봉인문(RoomShaper.HasPairGate) 또는 숲 덩굴 벽(ForestDressing)
        private bool HasTreasureGate(int index) =>
            rooms != null && index >= 0 && index < rooms.Rooms.Count && rooms.Rooms[index].Kind == RoomKind.Treasure
            && (stage.Chunks[index].HasFeature(FeatureKind.ElementGate) || stage.Chunks[index].HasFeature(FeatureKind.VineWall));
        // 미니맵 돌파 체크: 그 방의 전투 구역을 돌파했는지 (방 격자 모드에서 청크 번호 = 방 번호)
        public bool HasClearedArena(int index) => rooms != null && clearedArenas.Contains(index);
        public event Action<int> RoomEntered;

        // 미니맵 룬 점: 그 방에 선 마법 스위치 룬들의 속성 (청크 Features 순서). 없으면 빈 목록. 처음 물을 때 방마다 한 번 모은다.
        private List<Element>[] switchElements;
        private static readonly List<Element> NoSwitches = new List<Element>();
        public IReadOnlyList<Element> SwitchElements(int index)
        {
            if (rooms == null || stage == null || index < 0 || index >= stage.Chunks.Count) return NoSwitches;
            if (switchElements == null) switchElements = new List<Element>[stage.Chunks.Count];
            if (switchElements[index] == null)
            {
                var list = new List<Element>();
                foreach (var f in stage.Chunks[index].Features)
                    if (f.Kind == FeatureKind.PlatformSwitch) list.Add((Element)f.ElementIndex);
                switchElements[index] = list;
            }
            return switchElements[index];
        }

        private static bool IsDeductionPuzzle(ChunkKind kind) =>
            kind == ChunkKind.LanternPuzzle || kind == ChunkKind.MirrorPuzzle || kind == ChunkKind.WindBoxPuzzle;

        // 미니맵 방 이벤트 점: 추리 퍼즐 방(등불 순서·거울 룬·바람 상자)을 아직 안 풀었거나, 풀었는데 유물 상자를 아직 안 열었는지.
        // 바람 상자는 선반의 회복 구슬을 주우면 푼 것이다 (유물 상자가 없다).
        public bool HasPuzzleLeft(int index) =>
            stage != null && index >= 0 && index < stage.Chunks.Count && IsDeductionPuzzle(stage.Chunks[index].Kind)
            && (!solvedPuzzles.Contains(index)
                || (stage.Chunks[index].Kind != ChunkKind.WindBoxPuzzle && !takenRelics.Contains(index)));

        // 미니맵 방 이벤트 점 (2026-11-14): 저주받은 상자 방에서 아직 유물을 안 골랐는지 (상자가 잠들어 있거나, 정예를 잡고 유물 상자만 남았거나).
        public bool HasCursedChestLeft(int index) =>
            rooms != null && stage != null && index >= 0 && index < rooms.Rooms.Count && IsCursedChestRoom(index) && !takenRelics.Contains(index);

        // 미니맵 방 이벤트 점: 그 방에 보물 고블린이 아직 살아 있는지 (잡거나 10초가 지나 도망치면 false)
        private HealthSystem treasureGoblin;
        private int treasureGoblinRoom = -1;
        // 세로 방 보물 고블린은 플레이어가 바닥 가까이(GoblinWakeHeight칸 안) 내려와야 나타난다 (2026-11-14 자동 실행).
        // 위 문으로 들어오면 바닥까지 22칸을 내려오는 사이 10초가 흘러 잡을 틈이 없었다. 나타나기 전에 떠나면 다음에 올 때까지 기다린다.
        private readonly HashSet<int> goblinWaiting = new HashSet<int>();
        private const float GoblinWakeHeight = 9f;

        // 미니맵: 세로 방에서 아직 나타나지 않고 기다리는 보물 고블린 (들어가 본 방, 깜빡이지 않는 옅은 금색 점)
        public bool HasWaitingTreasureGoblin(int index) => goblinWaiting.Contains(index);
        private readonly Dictionary<int, Vector2> goblinEquipmentSpots = new Dictionary<int, Vector2>(); // 보물 고블린이 떨군 장비 자리 (방 → 발밑)
        public bool HasTreasureGoblin(int index) =>
            index == treasureGoblinRoom && treasureGoblin != null && !treasureGoblin.IsDead;

        // 미니맵 장비 점 (2026-11-14 자동 실행): 보물 고블린이 떨군 장비가 그 방 바닥에 아직 남았는지 (주우면 false, 바꿔 낀 옛 장비가 놓이면 그대로 true)
        public bool HasGoblinEquipmentLeft(int index) =>
            goblinEquipmentSpots.ContainsKey(index) && groundEquipment.ContainsKey(index);

        // 미니맵 동전 점: 그 방에 아직 안 주운 동전 무더기가 남았는지 (방 격자 모드, 청크 번호 = 방 번호).
        // 무더기 수는 청크 Features에서 처음 물을 때 방마다 한 번 센다. 주운 기록은 CoinPileKey(방, 순번) — 놓는 순서와 같은 순번
        private int[] coinPileCounts;
        public bool HasCoinsLeft(int index)
        {
            if (rooms == null || stage == null || index < 0 || index >= stage.Chunks.Count) return false;
            EnsureCoinPileCounts();
            for (int k = 0; k < coinPileCounts[index]; k++)
                if (!collectedCoins.Contains(CoinPileKey(index, k))) return true;
            return false;
        }

        private void EnsureCoinPileCounts()
        {
            if (coinPileCounts != null) return;
            coinPileCounts = new int[stage.Chunks.Count];
            for (int i = 0; i < coinPileCounts.Length; i++)
                foreach (var f in stage.Chunks[i].Features)
                    if (f.Kind == FeatureKind.CoinPile) coinPileCounts[i]++;
        }

        // 결과 창 "동전 무더기 a/b": 이번 스테이지 전체 무더기 수와 주운 수. 스테이지가 없으면 0.
        public int CoinPilesTotal
        {
            get
            {
                if (stage == null) return 0;
                EnsureCoinPileCounts();
                int total = 0;
                foreach (int n in coinPileCounts) total += n;
                return total;
            }
        }

        public int CoinPilesCollected => Mathf.Min(collectedCoins.Count, CoinPilesTotal);
    }
}
