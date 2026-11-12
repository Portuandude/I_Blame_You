using System.Collections.Generic;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 방 격자 모드: 방의 문 자리를 빛으로 표시해 출구가 한눈에 보이게 한다 (Docs/LevelDesign.md).
    // 좌우 문: 문 구멍을 채우는 옅은 빛 커튼 + 위쪽 문틀 띠. 위/아래 문: 구멍 가장자리를 따라 빛 띠.
    // 평소엔 aqua로 천천히 숨 쉬듯 깜빡이고, 이 방의 전투 구역이 잠겨 있는 동안은 붉게 바뀐다 (ArenaZone.IsLocked).
    // 잠김이 풀리는 순간(구역 돌파) 흰빛으로 한 번 번쩍였다가 aqua로 돌아온다.
    // 보스·중간보스 방으로 들어가는 문 (2026-11-12 플레이테스트 "보스 방 입구를 알아볼 수 있게"): 빛 커튼이 그 방 색(보스 빨강, 중간보스 주황)이고,
    // 문 양옆에 굵은 돌기둥 + 금빛 테두리, 문 위에 왕관(보스)·투구(중간보스) 그림이 둥실거리며, 바닥에 그 색 빛 웅덩이가 숨 쉰다.
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
        private readonly List<(SpriteRenderer renderer, Color color)> bossCurtains = new List<(SpriteRenderer, Color)>();
        private readonly List<(Transform icon, Vector3 home)> bossIcons = new List<(Transform, Vector3)>();
        private readonly List<(SpriteRenderer renderer, Color color)> bossGlows = new List<(SpriteRenderer, Color)>();
        private static readonly Color BossDoor = new Color(1f, 0.32f, 0.3f);    // 미니맵 보스 색과 같게
        private static readonly Color MidBossDoor = new Color(1f, 0.6f, 0.2f);  // 미니맵 중간보스 색
        private static readonly Color Pillar = new Color(0.25f, 0.2f, 0.3f);
        private static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        private const float UnlockFlashTime = 0.45f;

        private ArenaZone[] arenas;
        private bool wasLocked;
        private float flashUntil = -1f;

        // root: 방 지형 루트 (방을 치우면 함께 사라진다). origin: 방 왼쪽 아래 월드 좌표. geometry: 문 자리.
        // layout: 이웃 방 종류를 보려고 (보스·중간보스 입구 장식). null이면 장식 없음.
        public static RoomDoorMarkers Create(Transform root, Vector2 origin, RoomData room, RoomGeometry geometry, RoomLayoutData layout = null)
        {
            var go = new GameObject("RoomDoorMarkers");
            go.transform.SetParent(root, false);
            var markers = go.AddComponent<RoomDoorMarkers>();
            var sprite = SolidSpriteFactory.CreateSquare();

            if ((room.Doors & DoorDir.Left) != 0 && geometry.Arrivals.TryGetValue(DoorDir.Left, out var left))
                markers.AddSideDoor(sprite, origin, 0, left.y, BossKindBehind(layout, room, DoorDir.Left), 1);
            if ((room.Doors & DoorDir.Right) != 0 && geometry.Arrivals.TryGetValue(DoorDir.Right, out var right))
                markers.AddSideDoor(sprite, origin, ChunkData.Width - 1, right.y, BossKindBehind(layout, room, DoorDir.Right), -1);

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

        // 그 문 너머가 보스·중간보스 방이면 그 종류 (지금 방이 그 방이면 장식하지 않는다: 안에서는 이미 안다).
        private static RoomKind? BossKindBehind(RoomLayoutData layout, RoomData room, DoorDir dir)
        {
            if (layout == null || room.Kind == RoomKind.Boss || room.Kind == RoomKind.MidBoss) return null;
            var next = layout.Neighbor(room, dir);
            if (next == null) return null;
            return next.Kind == RoomKind.Boss || next.Kind == RoomKind.MidBoss ? next.Kind : (RoomKind?)null;
        }

        // 좌우 문: column 열(벽)의 floorY부터 DoorHeight칸이 뚫린 자리. inward: 방 안쪽 방향 (왼쪽 문 +1, 오른쪽 문 -1).
        private void AddSideDoor(Sprite sprite, Vector2 origin, int column, int floorY, RoomKind? bossBehind, int inward)
        {
            float x = origin.x + column + 0.5f;
            float bottom = origin.y + floorY;
            float height = RoomShaper.DoorHeight;
            if (bossBehind.HasValue)
            {
                AddBossGate(sprite, x, bottom, height, bossBehind.Value, inward);
                return;
            }
            Add(curtains, sprite, new Vector2(x, bottom + height / 2f), new Vector2(1f, height), CurtainOrder);
            Add(frames, sprite, new Vector2(x, bottom + height - 0.1f), new Vector2(1f, 0.2f), FrameOrder); // 문틀 윗면 (방 경계 밖으로 나가지 않게 벽 한 칸 폭)
        }

        // 보스 입구: 진한 색 커튼, 방 안쪽에 돌기둥(문 높이 + 1) 하나와 금빛 테두리, 기둥 위 가로 들보, 그 위 왕관/투구, 바닥 빛 웅덩이.
        // 장식은 문 열(벽)과 그 안쪽 한 칸에만 둬 길을 막지 않는다 (콜라이더 없음).
        private void AddBossGate(Sprite sprite, float x, float bottom, float height, RoomKind kind, int inward)
        {
            var tint = kind == RoomKind.Boss ? BossDoor : MidBossDoor;
            var curtain = Add(null, sprite, new Vector2(x, bottom + height / 2f), new Vector2(1f, height), CurtainOrder);
            bossCurtains.Add((curtain, tint));
            float pillarX = x + inward * 1.1f;
            var pillar = Add(null, sprite, new Vector2(pillarX, bottom + (height + 1f) / 2f), new Vector2(0.5f, height + 1f), FrameOrder);
            pillar.color = Pillar;
            var rim = Add(null, sprite, new Vector2(pillarX + inward * 0.3f, bottom + (height + 1f) / 2f), new Vector2(0.1f, height + 1f), FrameOrder + 1);
            rim.color = Gold;
            var lintel = Add(null, sprite, new Vector2(x + inward * 0.55f, bottom + height + 0.85f), new Vector2(1.9f, 0.35f), FrameOrder);
            lintel.color = Pillar;
            var lintelRim = Add(null, sprite, new Vector2(x + inward * 0.55f, bottom + height + 0.65f), new Vector2(1.9f, 0.08f), FrameOrder + 1);
            lintelRim.color = Gold;
            var glow = Add(null, sprite, new Vector2(x + inward * 1.2f, bottom + 0.08f), new Vector2(3f, 0.16f), CurtainOrder);
            bossGlows.Add((glow, tint));

            var texture = Resources.Load<Texture2D>(kind == RoomKind.Boss ? "UI/HUD/Crown" : "UI/HUD/MapHelmet");
            if (texture == null) return; // 그림이 없으면 기둥·색만
            var go = new GameObject("BossGateIcon");
            go.transform.SetParent(transform, false);
            var home = new Vector3(x + inward * 0.55f, bottom + height + 1.7f, 0f);
            go.transform.position = home;
            var icon = go.AddComponent<SpriteRenderer>();
            icon.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                Mathf.Max(texture.width, texture.height) / 1.1f); // 약 1.1유닛 크기
            icon.sortingOrder = FrameOrder + 2;
            bossIcons.Add((go.transform, home));
        }

        private SpriteRenderer Add(List<SpriteRenderer> list, Sprite sprite, Vector2 center, Vector2 size, int order)
        {
            var go = new GameObject(list == curtains ? "DoorCurtain" : list == frames ? "DoorFrame" : "BossGate");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            list?.Add(renderer);
            return renderer;
        }

        private void Update() => Apply();

        private void Apply()
        {
            bool locked = false;
            if (arenas != null)
                foreach (var arena in arenas) if (arena != null && arena.IsLocked) { locked = true; break; }

            if (wasLocked && !locked) flashUntil = Time.time + UnlockFlashTime;
            wasLocked = locked;

            var color = locked ? Locked : Open;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (locked ? 6f : 2.5f)); // 잠기면 빠르게 깜빡임
            float curtainAlpha = CurtainAlpha * pulse;
            float frameAlpha = FrameAlpha * pulse;
            if (Time.time < flashUntil)
            {
                // 번쩍임: 처음엔 거의 흰빛·진하게, 끝에 원래 aqua로
                float k = (flashUntil - Time.time) / UnlockFlashTime; // 1 → 0
                color = Color.Lerp(color, Color.white, 0.8f * k);
                curtainAlpha = Mathf.Lerp(curtainAlpha, 0.75f, k);
                frameAlpha = Mathf.Lerp(frameAlpha, 1f, k);
            }

            foreach (var r in curtains) r.color = new Color(color.r, color.g, color.b, curtainAlpha);
            foreach (var r in frames) r.color = new Color(color.r, color.g, color.b, frameAlpha);

            // 보스 입구: 잠겨 있으면 다른 문처럼 붉게, 아니면 그 방 색으로 진하게 숨 쉰다
            float bossPulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 3f);
            foreach (var (r, tint) in bossCurtains)
            {
                var c = locked ? color : tint;
                r.color = new Color(c.r, c.g, c.b, (locked ? curtainAlpha : 0.4f * bossPulse));
            }
            foreach (var (r, tint) in bossGlows) r.color = new Color(tint.r, tint.g, tint.b, 0.55f * bossPulse);
            for (int i = 0; i < bossIcons.Count; i++)
            {
                var (icon, home) = bossIcons[i];
                if (icon != null) icon.position = home + new Vector3(0f, 0.12f * Mathf.Sin(Time.time * 2.2f + i), 0f);
            }
        }
    }
}
