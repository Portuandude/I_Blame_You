using System;
using IBlameYou.Core;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 바닥에 놓인 장비 하나 (유물 상자를 열면 옆에 나온다, Docs/EquipmentDesign.md 7절).
    // 닿으면 HUD가 비교 카드(지금 장비 ↔ 이 장비)를 연다 (게임 정지). "바꿔 끼기"면 끼고, 빠진 옛 장비가 그 자리에 대신 놓인다.
    // "두고 가기"면 그대로 남고, 한 번 떨어졌다가 다시 닿아야 또 묻는다. 남은 장비는 청크 기록(ChunkStreamer)에 남는다.
    public class EquipmentPickup : MonoBehaviour
    {
        // HUD가 등록한다: (새 장비, 지금 그 칸 장비 또는 null, 결정 콜백 true=바꿔 끼기) → 창을 열었으면 true.
        public static Func<EquipmentItem, EquipmentItem, Action<bool>, bool> OpenRequested;

        private const float IconHeight = 0.55f;

        private EquipmentItem item;
        private Action<EquipmentItem> onChanged; // 바닥에 남은 장비가 바뀌면 (null = 없어짐)
        private SpriteRenderer icon;
        private PlayerMovement touching;
        private bool waiting;
        private bool askedWhileTouching; // 두고 가기를 골랐으면 떨어졌다가 다시 닿을 때까지 묻지 않는다
        private bool gone;

        public static EquipmentPickup Create(Transform parent, Vector2 position, EquipmentItem item, bool dropped, Action<EquipmentItem> onChanged)
        {
            var go = new GameObject("EquipmentPickup");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.2f, 1.4f);
            trigger.offset = new Vector2(0f, 0.7f);

            var pickup = go.AddComponent<EquipmentPickup>();
            pickup.onChanged = onChanged;
            var visual = new GameObject("Icon");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, IconHeight, 0f);
            pickup.icon = visual.AddComponent<SpriteRenderer>();
            pickup.icon.sortingOrder = 3;
            pickup.SetItem(item);
            VfxPlayer.AttachSparkle(go.transform, IconHeight + 0.3f);
            if (dropped) EffectLibrary.PlayHit(Element.Neutral, go.transform.position + Vector3.up * IconHeight, 1f);
            return pickup;
        }

        private void SetItem(EquipmentItem value)
        {
            item = value;
            var tex = Resources.Load<Texture2D>("UI/HUD/Equipment/" + value.Id);
            if (tex == null) return;
            tex.filterMode = FilterMode.Point;
            icon.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width); // 1유닛
        }

        // 방금 생긴 자리에 플레이어가 가만히 서 있으면(Rigidbody 수면) Enter가 오지 않을 수 있어, 처음 한 번 직접 겹침을 본다 (RelicChest와 같음).
        private void Start()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            var center = (Vector2)transform.position + box.offset;
            foreach (var hit in Physics2D.OverlapBoxAll(center, box.size, 0f))
            {
                var player = TrapCommon.PlayerFrom(hit);
                if (player != null) { touching = player; break; }
            }
        }

        // 위아래로 살짝 떠 있는다
        private void LateUpdate()
        {
            if (icon != null) icon.transform.localPosition = new Vector3(0f, IconHeight + Mathf.Sin(Time.time * 2.5f) * 0.08f, 0f);
            if (touching != null) TryOpen(touching);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;
            touching = player;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null || player != touching) return;
            touching = null;
            askedWhileTouching = false;
        }

        private void TryOpen(PlayerMovement player)
        {
            if (gone || waiting || askedWhileTouching || OpenRequested == null || item == null) return;
            var health = player.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;

            var equipment = PlayerEquipment.For(player.gameObject);
            waiting = true;
            bool opened = OpenRequested(item, equipment.Get(item.Slot), take =>
            {
                waiting = false;
                askedWhileTouching = true;
                if (!take || gone) return;
                var old = equipment.Equip(item);
                GameAudio.PlaySfx("RelicOpen", 0.5f);
                StageEvents.PostNotice($"장비: {item.Name}");
                if (old != null)
                {
                    SetItem(old); // 빠진 장비가 그 자리에 남는다
                    onChanged?.Invoke(old);
                    return;
                }

                gone = true;
                onChanged?.Invoke(null);
                EffectLibrary.PlayHit(Element.Neutral, transform.position + Vector3.up * IconHeight, 1f);
                Destroy(gameObject);
            });

            if (!opened) waiting = false; // 지금 열 수 없으면(영창 중 등) 닿아 있는 동안 다시 묻는다
        }
    }
}
