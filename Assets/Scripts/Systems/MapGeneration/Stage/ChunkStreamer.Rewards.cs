using System;
using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // ChunkStreamer 나눈 파일: 보상(회복 구슬·동전 무더기·유물 상자·장비)과 런 저장 기록 (Export/ImportRecords).
    public partial class ChunkStreamer
    {
        // 일반 전투 구역 보상 (보스 구역은 출구가 보상이라 없음). 이미 주웠으면 만들지 않는다.
        private void SpawnReward(Transform root, int chunkIndex, Vector2 position, bool dropped, System.Action alsoOnCollect = null)
        {
            if (collectedRewards.Contains(chunkIndex)) return;
            ArenaReward.Create(root, position, dropped, () =>
            {
                collectedRewards.Add(chunkIndex);
                alsoOnCollect?.Invoke();
            });
        }

        // 동전 무더기: 청크마다 여럿일 수 있어서(넓은 방은 구간 여러 개) 청크 번호와 청크 안 순번으로 기록한다
        private static int CoinPileKey(int chunkIndex, int ordinal) => chunkIndex * 16 + ordinal;

        private void SpawnCoinPile(Transform root, int key, Vector2 position, float difficulty)
        {
            if (collectedCoins.Contains(key)) return;
            CoinPile.Create(root, position, GoldDropTable.CoinPileGold(difficulty), () => collectedCoins.Add(key));
        }

        // 즉시 회복 (최대 체력 비율, 중간보스 처치·용사의 훈장). 쓰러진 뒤면 아무 일도 하지 않고 false. notice가 null이면 알림 없이.
        private bool HealPlayer(float ratio, string notice)
        {
            var health = player != null ? player.GetComponent<HealthSystem>() : null;
            if (health == null || health.IsDead) return false;
            float before = health.Current;
            health.Heal(health.Max * ratio);
            DamageNumbers.ShowHeal(player.position, health.Current - before);
            EffectLibrary.PlayHit(Element.Water, player.position + Vector3.up, 1.5f);
            GameAudio.PlaySfx("Pickup", 0.8f);
            if (notice != null) StageEvents.PostNotice(notice);
            return true;
        }

        private static readonly Vector3 MedalGlintOffset = new Vector3(0f, 1.5f, 0f); // 용사의 훈장 메달 시작 높이 (발밑 기준, 머리 위)

        // 정예 구역 유물 상자. 이미 유물을 골랐으면 만들지 않는다.
        private void SpawnRelicChest(Transform root, int chunkIndex, Vector2 position, bool dropped)
        {
            var equipmentPosition = position + new Vector2(EquipmentOffsetX, 0f);
            if (takenRelics.Contains(chunkIndex))
            {
                if (groundEquipment.TryGetValue(chunkIndex, out var left)) SpawnEquipment(root, chunkIndex, equipmentPosition, left, false);
                return;
            }

            RelicChest.Create(root, position, stage.Seed, chunkIndex, dropped, () =>
            {
                takenRelics.Add(chunkIndex);
                // 유물 상자를 열면 옆에 장비 하나 (Docs/EquipmentDesign.md 7절).
                var item = IBlameYou.Player.EquipmentOffer.Roll(stage.Seed, chunkIndex);
                groundEquipment[chunkIndex] = item;
                if (root != null) SpawnEquipment(root, chunkIndex, equipmentPosition, item, true);
            });
        }

        private const float EquipmentOffsetX = 1.6f; // 유물 상자 오른쪽

        private void SpawnEquipment(Transform root, int chunkIndex, Vector2 position, IBlameYou.Player.EquipmentItem item, bool dropped)
        {
            EquipmentPickup.Create(root, position, item, dropped, left =>
            {
                if (left == null) groundEquipment.Remove(chunkIndex);
                else groundEquipment[chunkIndex] = left;
            });
        }

        // 바닥에 남은 장비(groundEquipment)는 런 저장에 넣지 않는다: 2026-11-10부터 런 이어하기 저장을 쓰지 않음 (RunSave). 스테이지 2 저장을 다시 쓰게 되면 함께 넣을 것.
        // 런 진행 저장용: 지금까지의 기록을 꺼내거나(Export), 저장된 기록으로 되돌린다(Import, 청크를 만들기 전에 부를 것).
        public StageRecords ExportRecords()
        {
            var records = new StageRecords
            {
                killed = ToArray(killed),
                clearedArenas = ToArray(clearedArenas),
                collectedRewards = ToArray(collectedRewards),
                collectedCoins = ToArray(collectedCoins),
                takenRelics = ToArray(takenRelics),
                openedGates = ToArray(openedGates),
                gateStepChunks = new int[gateSteps.Count],
                gateStepValues = new int[gateSteps.Count],
                visitedRooms = ToArray(visitedRooms),
                bossCleared = bossCleared
            };

            int i = 0;
            foreach (var pair in gateSteps)
            {
                records.gateStepChunks[i] = pair.Key;
                records.gateStepValues[i] = pair.Value;
                i++;
            }

            return records;
        }

        public void ImportRecords(StageRecords records)
        {
            if (records == null) return;
            Fill(killed, records.killed);
            Fill(clearedArenas, records.clearedArenas);
            Fill(collectedRewards, records.collectedRewards);
            Fill(collectedCoins, records.collectedCoins);
            Fill(takenRelics, records.takenRelics);
            Fill(openedGates, records.openedGates);
            Fill(visitedRooms, records.visitedRooms);
            gateSteps.Clear();
            if (records.gateStepChunks != null && records.gateStepValues != null)
            {
                int count = Mathf.Min(records.gateStepChunks.Length, records.gateStepValues.Length);
                for (int i = 0; i < count; i++) gateSteps[records.gateStepChunks[i]] = records.gateStepValues[i];
            }

            bossCleared = records.bossCleared;
        }

        private static int[] ToArray(HashSet<int> set)
        {
            var array = new int[set.Count];
            set.CopyTo(array);
            return array;
        }

        private static void Fill(HashSet<int> set, int[] values)
        {
            set.Clear();
            if (values != null) foreach (int value in values) set.Add(value);
        }
    }
}
