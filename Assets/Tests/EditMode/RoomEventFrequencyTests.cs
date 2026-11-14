using System;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 방 이벤트 비율 점검 (2026-11-14 자동 실행): 실제 방 격자(RoomLayout)에서 방 이벤트가 판마다 나오는지.
    // 4×3 격자의 남는 이동 방은 모두 세로 방이라, 가로 방만 보던 보물 고블린·저주받은 상자가 60시드에 한 번도 안 나왔던 일을 막는다.
    // 문턱·확률을 바꾸면 이 하한도 같이 본다 (너무 드문 변형은 플레이테스트에서 못 본다).
    public class RoomEventFrequencyTests
    {
        private const float TargetSeconds = 20f * 60f; // 시작 화면 기본 목표 20분
        private const int SeedCount = 60;

        private struct Counts
        {
            public int Ambush, WallArchers, Burrow, Mixed, Cursed, CursedSwarm, DeepSwarm, Goblin, Tower, TowerGuard;
            public int RunsWithGoblin, RunsWithCursed, RunsWithAnyEvent;
        }

        private static Counts Count()
        {
            var c = new Counts();
            for (int seed = 0; seed < SeedCount; seed++)
            {
                bool goblin = false, cursed = false, any = false;
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    float depth = RoomEventRule.Depth(room);
                    if (room.Kind == RoomKind.Combat && room.Chunk == ChunkKind.Arena && AmbushRule.IsAmbush(seed, room.Index, depth, true))
                    {
                        c.Ambush++;
                        any = true;
                        if (RoomShaper.IsWallArcherAmbush(seed, room)) c.WallArchers++;
                        else if (AmbushRule.IsBurrow(seed, room.Index))
                        {
                            c.Burrow++;
                            if (AmbushRule.IsMixed(seed, room.Index, depth)) c.Mixed++;
                        }
                    }
                    if (RoomEventRule.IsTreasureGoblinRoom(seed, room)) { c.Goblin++; goblin = any = true; }
                    if (RoomEventRule.IsCursedChestRoom(seed, room))
                    {
                        c.Cursed++;
                        cursed = any = true;
                        if (CursedChestRule.IsSwarm(seed, room.Index)) c.CursedSwarm++;
                        if (CursedChestRule.IsSwarm(seed, room.Index) && depth >= CursedChestRule.SwarmEliteDifficulty) c.DeepSwarm++;
                    }
                    if (room.IsVertical && RoomEventRule.IsEventRoom(room))
                    {
                        var style = RoomShaper.PickShaftStyle(seed, room);
                        if (RoomShaper.IsArcherTower(room, style)) { c.Tower++; any = true; }
                        if (RoomShaper.HasArcherTowerGuard(room, style)) c.TowerGuard++;
                    }
                }
                if (goblin) c.RunsWithGoblin++;
                if (cursed) c.RunsWithCursed++;
                if (any) c.RunsWithAnyEvent++;
            }
            return c;
        }

        [Test]
        public void EveryRoomEventShowsUpAcrossSeeds()
        {
            var c = Count();
            string tag = $"매복 {c.Ambush}(벽 위 궁수 {c.WallArchers}·땅속 {c.Burrow}·섞임 {c.Mixed}) 저주 상자 {c.Cursed}(무리 {c.CursedSwarm}·정예 섞인 무리 {c.DeepSwarm}) 보물 고블린 {c.Goblin} 궁수 탑 {c.Tower}(파수꾼 {c.TowerGuard})";
            Console.WriteLine(tag);
            Assert.GreaterOrEqual(c.Goblin, SeedCount / 2, tag + ": 보물 고블린은 두 판에 한 번 넘게");
            Assert.GreaterOrEqual(c.Cursed, SeedCount / 4, tag + ": 저주받은 상자는 네 판에 한 번 넘게");
            Assert.Greater(c.CursedSwarm, 0, tag + ": 쫄몹 무리 변형");
            Assert.Greater(c.Cursed - c.CursedSwarm, 0, tag + ": 정예 변형");
            Assert.GreaterOrEqual(c.Ambush, SeedCount / 4, tag + ": 매복");
            Assert.Greater(c.WallArchers, 0, tag + ": 벽 위 궁수");
            Assert.Greater(c.Burrow, 0, tag + ": 땅속 매복");
            Assert.Greater(c.Mixed, 0, tag + ": 섞인 매복");
            Assert.Greater(c.DeepSwarm, 0, tag + ": 정예 섞인 무리");
            Assert.Greater(c.Tower, 0, tag + ": 궁수 탑");
        }

        [Test]
        public void MostRunsMeetARoomEvent()
        {
            var c = Count();
            Assert.GreaterOrEqual(c.RunsWithAnyEvent, SeedCount * 3 / 4, $"방 이벤트를 하나라도 만나는 판 {c.RunsWithAnyEvent}/{SeedCount}");
        }

        // 저주 상자는 아래 문 구멍이 있는 세로 방·궁수 탑에는 없다 (정예가 구멍으로 떨어지면 저주가 안 풀린다), 보물 고블린 방과도 겹치지 않는다.
        [Test]
        public void CursedChestShaftsHaveClosedFloorAndNoTower()
        {
            int shafts = 0;
            for (int seed = 0; seed < SeedCount; seed++)
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (!RoomEventRule.IsCursedChestRoom(seed, room)) continue;
                    string tag = $"seed {seed} room {room.Index}";
                    Assert.IsTrue(RoomEventRule.IsEventRoom(room), tag);
                    Assert.IsFalse(RoomEventRule.IsTreasureGoblinRoom(seed, room), tag);
                    if (!room.IsVertical) continue;
                    shafts++;
                    Assert.IsTrue(RoomEventRule.HasClosedFloor(room), tag + ": 바닥이 막힌 세로 방");
                    Assert.IsFalse(RoomShaper.IsArcherTower(room, RoomShaper.PickShaftStyle(seed, room)), tag + ": 궁수 탑 아님");
                    Assert.GreaterOrEqual(RoomEventRule.Depth(room), CursedChestRule.MinDifficulty, tag);
                }
            Assert.Greater(shafts, 0, "세로 방 저주 상자가 한 번도 안 나옴");
        }

        // 보물 고블린·저주 상자는 일반 전투 방에만 (2026-11-14 이동 방 삭제. 정예·퍼즐·보상·마을 방에는 없다)
        [Test]
        public void EventsOnlyInCombatRooms()
        {
            for (int seed = 0; seed < SeedCount; seed++)
                foreach (var room in RoomLayout.Generate(seed, TargetSeconds).Rooms)
                {
                    if (RoomEventRule.IsEventRoom(room)) continue;
                    Assert.IsFalse(RoomEventRule.IsTreasureGoblinRoom(seed, room), $"seed {seed} room {room.Index} {room.Kind}");
                    Assert.IsFalse(RoomEventRule.IsCursedChestRoom(seed, room), $"seed {seed} room {room.Index} {room.Kind}");
                }
        }
    }
}
