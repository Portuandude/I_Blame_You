using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 방 격자 모드: 방의 문 자리를 빛으로 표시해 출구가 한눈에 보이게 한다 (Docs/LevelDesign.md).
    // 좌우 문: 문 구멍을 채우는 옅은 빛 커튼 + 위쪽 문틀 띠. 위/아래 문: 구멍 가장자리를 따라 빛 띠.
    // 평소엔 aqua로 천천히 숨 쉬듯 깜빡이고, 이 방의 전투 구역이 잠겨 있는 동안은 붉게 바뀐다 (ArenaZone.IsLocked).
    public class RoomDoorMarkers : MonoBehaviour
    {
        private const int CurtainOrder = -5; // 배경(-25 이하)보다 앞, 지형(-10)보다 앞, 캐릭터(1)보다 뒤
        private const int FrameOrder = 2;
        private const float CurtainAlpha = 0.22f;
        private const float FrameAlpha = 0.85f;
        private static readonly Color Open = new Color(0.5f, 0.97f, 1f);    // 팔레트 aqua
        private static readonly Color Locked = new Color(1f, 0.35f, 0.4f);  // 전투 구역 문(ArenaZone)과 같은 붉은색

        private readonly List<SpriteRenderer> curtains = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> frames = new List<SpriteRenderer>();
        private ArenaZone[] arenas;

        // root: 방 지형 루트 (방을 치우면 함께 사라진다). origin: 방 왼쪽 아래 월드 좌표. geometry: 문 자리.
        public static RoomDoorMarkers Create(Transform root, Vector2 origin, RoomData room, RoomGeometry geometry)
        {
            var go = new GameObject("RoomDoorMarkers");
            go.transform.SetParent(root, false);
            var markers = go.AddComponent<RoomDoorMarkers>();
            var sprite = SolidSpriteFactory.CreateSquare();

            if ((room.Doors & DoorDir.Left) != 0 && geometry.Arrivals.TryGetValue(DoorDir.Left, out var left))
                markers.AddSideDoor(sprite, origin, 0, left.y);
            if ((room.Doors & DoorDir.Right) != 0 && geometry.Arrivals.TryGetValue(DoorDir.Right, out var right))
                markers.AddSideDoor(sprite, origin, ChunkData.Width - 1, right.y);

            float holeLeft = origin.x + RoomShaper.HoleX0;
            float holeWidth = RoomShaper.HoleX1 - RoomShaper.HoleX0 + 1;
            if ((room.Doors & DoorDir.Up) != 0)
            {
                // 천장 구멍: 구멍 칸을 옅게 채우고 그 아래 가장자리에 띠
                float y = origin.y + ChunkData.Height - 1;
                markers.Add(markers.curtains, sprite, new Vector2(holeLeft + holeWidth / 2f, y + 0.5f), new Vector2(holeWidth, 1f), CurtainOrder);
                markers.Add(markers.frames, sprite, new Vector2(holeLeft + holeWidth / 2f, y + 0.08f), new Vector2(holeWidth + 0.6f, 0.16f), FrameOrder);
            }

            if ((room.Doors & DoorDir.Down) != 0)
            {
                // 바닥 구멍: 바닥 높이 바로 아래 한 칸을 옅게 채우고 가장자리 양쪽에 짧은 띠
                float floor = origin.y + RoomShaper.ShaftGround;
                markers.Add(markers.curtains, sprite, new Vector2(holeLeft + holeWidth / 2f, floor - 0.5f), new Vector2(holeWidth, 1f), CurtainOrder);
                markers.Add(markers.frames, sprite, new Vector2(holeLeft - 0.3f, floor - 0.08f), new Vector2(0.6f, 0.16f), FrameOrder);
                markers.Add(markers.frames, sprite, new Vector2(holeLeft + holeWidth + 0.3f, floor - 0.08f), new Vector2(0.6f, 0.16f), FrameOrder);
            }

            markers.arenas = root.GetComponentsInChildren<ArenaZone>(true);
            markers.Apply();
            return markers;
        }

        // 좌우 문: column 열(벽)의 floorY부터 DoorHeight칸이 뚫린 자리.
        private void AddSideDoor(Sprite sprite, Vector2 origin, int column, int floorY)
        {
            float x = origin.x + column + 0.5f;
            float bottom = origin.y + floorY;
            float height = RoomShaper.DoorHeight;
            Add(curtains, sprite, new Vector2(x, bottom + height / 2f), new Vector2(1f, height), CurtainOrder);
            Add(frames, sprite, new Vector2(x, bottom + height - 0.1f), new Vector2(1.6f, 0.2f), FrameOrder); // 문틀 윗면
        }

        private void Add(List<SpriteRenderer> list, Sprite sprite, Vector2 center, Vector2 size, int order)
        {
            var go = new GameObject(list == curtains ? "DoorCurtain" : "DoorFrame");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            list.Add(renderer);
        }

        private void Update() => Apply();

        private void Apply()
        {
            bool locked = false;
            if (arenas != null)
                foreach (var arena in arenas) if (arena != null && arena.IsLocked) { locked = true; break; }

            var color = locked ? Locked : Open;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (locked ? 6f : 2.5f)); // 잠기면 빠르게 깜빡임
            foreach (var r in curtains) r.color = new Color(color.r, color.g, color.b, CurtainAlpha * pulse);
            foreach (var r in frames) r.color = new Color(color.r, color.g, color.b, FrameAlpha * pulse);
        }
    }
}
