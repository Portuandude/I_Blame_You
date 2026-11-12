using System;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // NPC 마을 건물 (Docs/EquipmentDesign.md 9절). 마을 방(RoomKind.Village)에 식당·장비 상점 두 채가 선다.
    public enum VillageService
    {
        Restaurant, // 식당: 체력·마나 음식
        Shop        // 장비 상점: 구매·판매·수식어 변경
    }

    // 건물 앞에 서서 W(또는 ↑)를 누르면 StageEvents.RaiseVillageInteract로 창을 열어 달라고 알린다.
    // 받는 쪽(HUD 창)이 없으면 "곧 열려요" 알림만. 앞에 서 있는 동안 문 위에 작은 W 키 표시가 뜬다.
    public class VillageBuilding : MonoBehaviour
    {
        public const float PixelsPerUnit = 16f; // Tools/ArtGen/generate_village.py (56픽셀 = 3.5유닛)
        private const float PromptHeight = 3.9f;

        private VillageService service;
        private PlayerMovement touching;
        private PlayerControlState control;
        private GameObject prompt;

        public VillageService Service => service;

        public static VillageBuilding Create(Transform parent, Vector2 groundCenter, VillageService service)
        {
            var go = new GameObject(service == VillageService.Restaurant ? "Village_Restaurant" : "Village_Shop");
            go.transform.SetParent(parent, false);
            go.transform.position = groundCenter;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -3; // 플레이어(1)·바닥보다 뒤
            var tex = Resources.Load<Texture2D>(service == VillageService.Restaurant ? "Village/Restaurant" : "Village/Shop");
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                renderer.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0f), PixelsPerUnit);
            }

            // 문 앞 범위 (건물 가운데 2.4유닛 폭, 사람 키 높이)
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2.4f, 2f);
            trigger.offset = new Vector2(0f, 1f);

            var building = go.AddComponent<VillageBuilding>();
            building.service = service;
            building.prompt = CreatePrompt(go.transform);
            building.prompt.SetActive(false);
            return building;
        }

        private static GameObject CreatePrompt(Transform parent)
        {
            var go = new GameObject("Prompt");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, PromptHeight, 0f);
            var mesh = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = "[W]";
            mesh.fontSize = 48;
            mesh.fontStyle = FontStyle.Bold;
            mesh.characterSize = 0.05f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(1f, 0.957f, 0.839f); // cream
            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 5;
            return go;
        }

        // 방금 생긴 자리에 플레이어가 가만히 서 있으면 Enter가 오지 않을 수 있어 처음 한 번 직접 겹침을 본다 (RelicChest와 같음).
        private void Start()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            var center = (Vector2)transform.position + box.offset;
            foreach (var hit in Physics2D.OverlapBoxAll(center, box.size, 0f))
            {
                var player = TrapCommon.PlayerFrom(hit);
                if (player != null) { SetTouching(player); break; }
            }
        }

        private void Update()
        {
            if (touching == null) return;
            if (prompt != null) prompt.transform.localPosition = new Vector3(0f, PromptHeight + Mathf.Sin(Time.time * 4f) * 0.06f, 0f);
            if (Time.timeScale <= 0f || control == null || !RoleInput.InteractDown(control)) return;
            var health = touching.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;
            if (!StageEvents.RaiseVillageInteract(service))
                StageEvents.PostNotice(service == VillageService.Restaurant ? "식당 — 곧 문을 열어요" : "장비 상점 — 곧 문을 열어요");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player != null) SetTouching(player);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null || player != touching) return;
            touching = null;
            control = null;
            if (prompt != null) prompt.SetActive(false);
        }

        private void SetTouching(PlayerMovement player)
        {
            touching = player;
            control = player.GetComponent<PlayerControlState>();
            if (prompt != null) prompt.SetActive(true);
        }
    }
}
