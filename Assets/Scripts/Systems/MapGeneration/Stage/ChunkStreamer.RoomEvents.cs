using System;
using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // ChunkStreamer 나눈 파일: 방 이벤트: 보물 고블린, 저주받은 상자 (정예·무리 깨우기와 보상), 설 자리 찾기.
    public partial class ChunkStreamer
    {
        // 세로 방에서 기다리던 보물 고블린: 플레이어가 그 방 바닥에서 GoblinWakeHeight칸 안으로 내려오면 나타난다.
        private void WakeWaitingGoblin()
        {
            if (goblinWaiting.Count == 0 || !goblinWaiting.Contains(currentRoom)) return;
            if (player.position.y - Origin(currentRoom).y > GoblinWakeHeight) return;
            if (SpawnTreasureGoblin(currentRoom)) goblinWaiting.Remove(currentRoom); // 못 불렀으면(설 자리 없음) 다음 프레임에 다시: 먼저 지우면 그 판엔 영영 안 나왔다 (버그 점검 2026-11-22)
        }

        // 선악도 방 (AlignmentRoomRule): 판에서 처음 들어간 뒤 문 쿨다운이 지나면 A·B 선택지 룰렛 창을 한 번 연다.
        // 창을 못 열었으면(다른 창이 떠 있음 등) 그 방에 있는 동안 다음 프레임에 다시. 연 뒤에는 쓰러짐·클리어로 창이 닫혀도 다시 열지 않는다.
        private readonly HashSet<int> alignmentOffered = new HashSet<int>();

        private void OfferAlignmentEvent()
        {
            if (rooms == null || player == null || Time.time < doorReadyTime) return;
            var room = rooms.Rooms[currentRoom];
            if (room.Kind != RoomKind.Alignment || alignmentOffered.Contains(currentRoom) || IBlameYou.Core.RunStats.Ended) return;
            var health = player.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;
            var e = AlignmentRoomRule.Events[AlignmentRoomRule.Pick(stage.Seed, currentRoom)];
            if (!StageEvents.RaiseChoice(e.Title, AlignmentRoomRule.Texts(e), AlignmentRoomRule.Alignments(e), result => ApplyAlignmentResult(e, result)))
                return;
            alignmentOffered.Add(currentRoom);
        }

        private void ApplyAlignmentResult(AlignmentRoomRule.Event e, int result)
        {
            if (result < 0 || result >= e.Options.Length) return;
            var option = e.Options[result];
            int available = IBlameYou.Core.RunStats.Gold - IBlameYou.Core.RunGold.InFlight;
            int gold = AlignmentRoomRule.GoldChange(option, available);
            if (gold > 0) IBlameYou.Core.RunStats.AddGold(gold);
            else if (gold < 0 && !IBlameYou.Core.RunStats.TrySpendGold(-gold, IBlameYou.Core.RunGold.InFlight)) gold = 0;
            float healthChange = 0f;
            var health = player != null ? player.GetComponent<HealthSystem>() : null;
            if (health != null)
            {
                healthChange = AlignmentRoomRule.HealthChange(option, health.Current, health.Max);
                if (healthChange > 0f) health.Heal(healthChange);
                else if (healthChange < 0f) health.Drain(-healthChange);
            }
            StageEvents.PostNotice(AlignmentRoomRule.Summary(option, gold, healthChange));
            if (gold != 0 || healthChange != 0f) GameAudio.PlaySfx("Pickup", 0.6f, 0f);
        }

        // 플레이어에게서 먼 쪽(방 가로 30% 또는 70%) 근처에서 설 자리를 찾아 보물 고블린을 부른다. 자리가 없으면 부르지 않는다. 불렀으면 true.
        private bool SpawnTreasureGoblin(int index)
        {
            if (spawnEnemy == null || player == null) return false;
            var origin = Origin(index);
            int cols = RoomCols(index);
            float local = player.position.x - origin.x;
            var spot = FindStandSpot(index, Mathf.RoundToInt(cols * (local < cols / 2f ? 0.7f : 0.3f)), headroom: EnemyHeadroom);
            if (!spot.HasValue) return false;
            var health = spawnEnemy(StageEnemy.TreasureGoblin, new Vector3(spot.Value.x, spot.Value.y + SpawnLift, 0f), transform);
            if (health == null) return false;
            RegisterEnemy(health);
            treasureGoblin = health;
            treasureGoblinRoom = index;
            goblinsMet.Add(index);
            health.HealthChanged += (current, max) => { if (current <= 0f) goblinsCaught.Add(index); }; // 결과 창 방 이벤트 줄 (도망치면 HealthChanged 없이 사라진다)
            if (TreasureGoblinRule.DropsEquipment(stage.Seed, index))
            {
                bool dropped = false;
                health.HealthChanged += (current, max) =>
                {
                    if (current > 0f || dropped || health == null) return;
                    dropped = true;
                    DropGoblinEquipment(index, health.transform.position);
                };
            }
            StageEvents.PostNotice("보물 고블린이다! 10초 안에 잡아라!");
            GameAudio.PlaySfx("CoinPile", 0.6f, 0f);
            return true;
        }

        // 보물 고블린 장비 떨굼 (Chunks/TreasureGoblinRule.DropsEquipment): 잡힌 자리 근처 바닥에 장비 하나가 튀어나온다.
        // 유물 상자 장비와 같은 groundEquipment 기록을 써서, 두고 가면 다시 왔을 때 그 자리에 남아 있다 (보물 고블린 방에는 유물 상자가 없다).
        private void DropGoblinEquipment(int index, Vector3 at)
        {
            if (!loaded.TryGetValue(index, out var root) || root == null) return;
            int column = Mathf.FloorToInt(at.x - Origin(index).x);
            var spot = FindStandSpot(index, column) ?? (Vector2)at; // 설 자리를 못 찾으면 쓰러진 자리 그대로
            var item = IBlameYou.Player.EquipmentOffer.Roll(stage.Seed, index);
            groundEquipment[index] = item;
            goblinEquipmentSpots[index] = spot;
            SpawnEquipment(root, index, spot, item, true);
            StageEvents.PostNotice("자루에서 장비가 떨어졌다!");
        }

        // 방 이벤트 저주받은 상자 (2026-11-14 사람 결정, Chunks/CursedChestRule): 이동 방 가운데. 세로 방은 바닥이 막힌 방만, 궁수 탑·보물 고블린 방은 빼고 (Chunks/RoomEventRule).
        private bool IsCursedChestRoom(int index) => RoomEventRule.IsCursedChestRoom(stage.Seed, rooms.Rooms[index]);

        private void PlaceCursedChest(Transform root, int index)
        {
            if (takenRelics.Contains(index)) return;
            var spot = FindStandSpot(index, RoomCols(index) / 2, true); // 세로 방 바닥 적과 겹쳐 서지 않게 (2026-11-14 리뷰 LOW)
            if (!spot.HasValue) return;
            var position = spot.Value;
            if (cursedBroken.Contains(index))
            {
                SpawnCursedReward(root, index, position, false); // 정예는 이미 쓰러뜨렸고 유물만 남았다
                return;
            }

            // 깨운 채 떠났다가 돌아오면 상자가 처음 상태로 돌아갔다고 알려 준다 (2026-11-14 자동 실행)
            if (cursedLeftAwake.Remove(index)) StageEvents.PostNotice("상자의 저주가 다시 잠들었다...");

            CursedChest chest = null;
            bool swarm = CursedChestRule.IsSwarm(stage.Seed, index);
            chest = CursedChest.Create(root, position, () =>
            {
                if (swarm) AwakenCursedSwarm(root, index, position, chest);
                else AwakenCursedElite(root, index, position, chest);
            }, swarm);
        }

        // 무리 중 아직 살아 있는 것이 있나 (쓰러지지 않고 치워진 것 — 낭떠러지 등 — 은 셈하지 않는다)
        private static bool AnyAlive(List<HealthSystem> group)
        {
            foreach (var g in group)
                if (g != null && !g.IsDead) return true;
            return false;
        }

        // 변형 "쫄몹 무리" (CursedChestRule.IsSwarm): 상자 양옆(SwarmOffsets 칸)에 쫄몹 4마리. 모두 쓰러뜨리면 저주가 풀린다.
        // 남은 채 방을 떠나면 무리는 치워지고, 다시 오면 상자가 처음 상태로 다시 놓인다 (정예와 같음).
        private void AwakenCursedSwarm(Transform root, int index, Vector2 chestPosition, CursedChest chest)
        {
            if (spawnEnemy == null) { if (chest != null) chest.Rest(); return; }
            int chestColumn = Mathf.FloorToInt(chestPosition.x - Origin(index).x);
            var kinds = CursedChestRule.SwarmFor(stage.Seed, index, RoomDepth(index)); // 깊은 방 무리엔 정예 하나
            var spawned = new List<HealthSystem>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var spot = FindStandSpot(index, chestColumn + CursedChestRule.SwarmOffsets[i], headroom: EnemyHeadroom) ?? chestPosition;
                var position = new Vector3(spot.x, spot.y + SpawnLift, 0f);
                EffectLibrary.PlayOneShot(position + Vector3.up * 0.4f, EffectLibrary.TeleportFrames, 20f, new Color(0.66f, 0.4f, 0.98f, 0.7f), 1f);
                // 스테이지 2+는 그 스테이지 적으로 (숲: 슬라임 → 고블린·코볼트, StageRoster)
                var kind = StageRoster.AdaptSpawn(kinds[i], stage.Chunks[index].Stage, RoomDepth(index), stage.Seed, index * 100 + 90 + i);
                var health = spawnEnemy(kind, position, transform);
                if (health == null) continue;
                RegisterEnemy(health);
                spawned.Add(health);
                bool down = false;
                health.HealthChanged += (current, max) =>
                {
                    if (current > 0f || down) return;
                    down = true;
                    if (AnyAlive(spawned) || cursedBroken.Contains(index)) return;
                    cursedBroken.Add(index);
                    // 무리 처치 보너스 (2026-11-14 자동 실행): 깨어난 무리 수 × SwarmGoldPerEnemy 골드가 상자 자리에서 튄다
                    int gold = IBlameYou.Core.RunGold.OnCoinPile(player, new Vector3(chestPosition.x, chestPosition.y + 1f, 0f), CursedChestRule.SwarmClearGold(spawned.Count));
                    if (chest != null && chest.Break(gold) > CursedChestRule.OfferSize) cursedQuick.Add(index);
                    if (root != null) SpawnCursedReward(root, index, chestPosition, true);
                };
            }
            if (spawned.Count == 0) { if (chest != null) chest.Rest(); return; }
            cursedLeftAwake.Add(index); // 풀기 전에 방을 떠나면 다음에 올 때 "다시 잠들었다" (풀면 SpawnCursedReward에서 지운다)
            GameAudio.PlaySfx("Summon", 0.7f);
            if (chest != null) chest.Watch(spawned.ToArray());
        }

        // 상자 옆(플레이어 반대쪽 4칸, 막히면 반대쪽)에 정예를 부른다. 쓰러뜨리면 상자가 풀려 유물 상자(2개 중 고르기)가 된다.
        // 정예를 두고 방을 떠나면 정예는 치워지고, 다시 오면 상자가 처음 상태로 다시 놓인다.
        private void AwakenCursedElite(Transform root, int index, Vector2 chestPosition, CursedChest chest)
        {
            if (spawnEnemy == null) { if (chest != null) chest.Rest(); return; }
            int chestColumn = Mathf.FloorToInt(chestPosition.x - Origin(index).x);
            int side = player != null && player.position.x < chestPosition.x ? 1 : -1;
            var spot = FindStandSpot(index, chestColumn + side * 4, headroom: EnemyHeadroom) ?? chestPosition;
            var position = new Vector3(spot.x, spot.y + SpawnLift, 0f);
            EffectLibrary.PlayOneShot(position + Vector3.up * 0.6f, EffectLibrary.TeleportFrames, 20f, new Color(0.66f, 0.4f, 0.98f, 0.8f), 1.4f);
            GameAudio.PlaySfx("Summon", 0.7f);
            var elite = StageRoster.AdaptSpawn(CursedChestRule.EliteFor(stage.Seed, index), stage.Chunks[index].Stage, RoomDepth(index), stage.Seed, index * 100 + 99);
            var health = spawnEnemy(elite, position, transform);
            if (health == null) { if (chest != null) chest.Rest(); return; }
            cursedLeftAwake.Add(index); // 풀기 전에 방을 떠나면 다음에 올 때 "다시 잠들었다" (풀면 SpawnCursedReward에서 지운다)
            RegisterEnemy(health);
            if (chest != null) chest.Watch(health);
            // 어미 슬라임이 분열해 나온 슬라임은 SplitOnDeath가 이 스트리머(IEnemyGroup)에 넣어 방을 떠나면 함께 치워진다. 보상은 어미가 쓰러진 순간
            health.HealthChanged += (current, max) =>
            {
                if (current > 0f || cursedBroken.Contains(index)) return;
                cursedBroken.Add(index);
                if (chest != null && chest.Break() > CursedChestRule.OfferSize) cursedQuick.Add(index);
                if (root != null) SpawnCursedReward(root, index, chestPosition, true);
            };
        }

        private void SpawnCursedReward(Transform root, int index, Vector2 position, bool dropped)
        {
            cursedLeftAwake.Remove(index);
            int offer = cursedQuick.Contains(index) ? CursedChestRule.QuickOfferSize : CursedChestRule.OfferSize;
            RelicChest.Create(root, position, stage.Seed, index, dropped, () => takenRelics.Add(index), offer);
        }

        // 방 이벤트가 설 자리: center 열에서 좌우로 넓혀 가며, 바닥(가장 낮은 땅) 위 두 칸이 비고 함정·발판 같은 지형 요소가 없는 열.
        // 반환: 그 칸 바닥 가운데의 월드 좌표 (발밑). 못 찾으면 null.
        // avoidEnemies: 찾은 바닥 칸 ±1열·±1행에 청크 적이 서 있으면 그 열을 건너뛴다 (저주 상자 — 세로 방은 바닥에 적이 서 있다).
        // headroom: 바닥 위로 비어 있어야 하는 칸 수. 적은 EnemyHeadroom(3) — SpawnLift(1.2)만큼 띄워 만들어서 대전사(키 1.35)는 머리가 바닥+2.55까지 온다.
        //   두 칸만 보면 세로 방 첫 발판(바닥+3 줄) 바로 밑에서 발판에 몸이 박혀 걸린다 (2026-11-14 자동 실행).
        private Vector2? FindStandSpot(int index, int center, bool avoidEnemies = false, int headroom = 2)
        {
            var chunk = stage.Chunks[index];
            var origin = Origin(index);
            int cols = RoomCols(index);
            for (int offset = 0; offset <= 8; offset++)
            {
                for (int side = 0; side < 2; side++) // 오른쪽·왼쪽 (기다리는 보물 고블린이 매 프레임 다시 찾으므로 배열을 만들지 않는다)
                {
                    int x = side == 0 ? center + offset : center - offset;
                    if (x < 3 || x > cols - 4 || HasTerrainFeature(chunk, x)) continue;
                    for (int y = 1; y < ChunkData.Height - 3; y++)
                    {
                        if (!chunk.IsSolid(x, y - 1) || chunk.IsSolid(x, y) || chunk.IsSolid(x, y + 1)) continue;
                        if (!HeadClear(chunk, x, y, headroom)) break; // 이 열 바닥은 머리 위가 낮다 (발판 밑) → 다음 열
                        if (avoidEnemies && HasEnemyNear(chunk, x, y)) break; // 이 열 바닥엔 적이 선다 → 다음 열
                        return new Vector2(origin.x + x + 0.5f, origin.y + y);
                    }
                }
            }
            return null;
        }

        private static bool HeadClear(ChunkData chunk, int x, int y, int headroom)
        {
            for (int k = 2; k < headroom; k++)
                if (y + k < ChunkData.Height && chunk.IsSolid(x, y + k)) return false;
            return true;
        }

        private const int EnemyHeadroom = 3;

        // 그 열에 적이 아닌 지형 요소(가시·함정·발판·버섯·바람 구멍·동전 등)가 있으면 true.
        private static bool HasTerrainFeature(ChunkData chunk, int x)
        {
            foreach (var f in chunk.Features)
            {
                if (f.Kind == FeatureKind.Enemy || f.Kind == FeatureKind.PlayerStart) continue;
                if (x >= f.X - 1 && x <= f.X + Math.Max(1, f.Width)) return true;
            }
            return false;
        }

        private static bool HasEnemyNear(ChunkData chunk, int x, int y)
        {
            foreach (var f in chunk.Features)
                if (f.Kind == FeatureKind.Enemy && x >= f.X - 1 && x <= f.X + 1 && Math.Abs(f.Y - y) <= 1) return true;
            return false;
        }
    }
}
