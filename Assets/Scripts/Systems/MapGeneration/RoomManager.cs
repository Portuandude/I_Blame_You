using System.Collections;
using System.Collections.Generic;
using IBlameYou.Core;
using IBlameYou.Player;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 방 목록과 현재 방을 관리하고, 문을 통한 방 이동(화면 암전 → 플레이어를 반대편 문 앞으로 이동 → 카메라를 새 방 범위로 전환)을 처리한다.
    public class RoomManager : MonoBehaviour
    {
        private const float FadeDuration = 0.15f;
        private const float ArrivalCooldown = 0.75f; // 도착 직후 바로 되돌아가지 않도록

        private readonly Dictionary<Vector2Int, RoomInstance> rooms = new Dictionary<Vector2Int, RoomInstance>();

        private CameraFollow cameraFollow;
        private SpriteRenderer fadeOverlay;
        private bool transitioning;
        private float nextTravelTime;

        public RoomInstance CurrentRoom { get; private set; }

        public void Register(RoomInstance room)
        {
            rooms[room.Node.GridPosition] = room;
        }

        // 격자에서 이웃한 방이 있는 방향마다 문을 만든다.
        public void BuildDoors()
        {
            foreach (var room in rooms.Values)
            {
                foreach (RoomDirection direction in System.Enum.GetValues(typeof(RoomDirection)))
                {
                    var neighbor = room.Node.GridPosition + direction.ToOffset();
                    if (rooms.ContainsKey(neighbor)) DoorBuilder.Build(room, direction, neighbor, this);
                }
            }
        }

        public void Begin(RoomInstance startRoom, CameraFollow follow)
        {
            CurrentRoom = startRoom;
            cameraFollow = follow;
            CreateFadeOverlay(follow.transform);
        }

        public void RequestTravel(RoomDoor door, PlayerMovement player)
        {
            if (transitioning || Time.time < nextTravelTime) return;
            if (player.IsDashing) return; // 대쉬 중(중력 0 상태)에 컴포넌트를 끄면 중력이 복구되지 않으므로 끝난 뒤에 이동
            if (!rooms.ContainsKey(door.TargetGrid)) return;

            StartCoroutine(TravelRoutine(door, player));
        }

        private IEnumerator TravelRoutine(RoomDoor door, PlayerMovement player)
        {
            transitioning = true;

            var target = rooms[door.TargetGrid];
            var arrival = target.Center + DoorBuilder.GetArrivalLocalPosition(target.Size, door.Direction.Opposite());

            var rb = player.GetComponent<Rigidbody2D>();
            var combat = player.GetComponent<PlayerCombat>();

            // 이동 중에는 조작/물리(피격 포함)를 멈춘다.
            player.enabled = false;
            if (combat != null) combat.enabled = false;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;

            yield return Fade(0f, 1f);

            player.transform.position = arrival;
            rb.position = arrival;
            CurrentRoom = target;
            cameraFollow.SetBounds(target.WorldBounds);
            cameraFollow.SnapToTarget();

            yield return Fade(1f, 0f);

            rb.simulated = true;
            player.enabled = true;
            if (combat != null) combat.enabled = true;

            nextTravelTime = Time.time + ArrivalCooldown;
            transitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(from, to, elapsed / FadeDuration));
                yield return null;
            }

            SetFade(to);
        }

        private void SetFade(float alpha)
        {
            if (fadeOverlay == null) return;
            var color = fadeOverlay.color;
            color.a = alpha;
            fadeOverlay.color = color;
        }

        // 카메라 앞에 붙인 커다란 검은 사각형의 알파로 암전을 표현한다 (UI 캔버스 없이).
        private void CreateFadeOverlay(Transform cameraTransform)
        {
            var go = new GameObject("RoomFade");
            go.transform.SetParent(cameraTransform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 5f);
            go.transform.localScale = new Vector3(400f, 400f, 1f);

            fadeOverlay = go.AddComponent<SpriteRenderer>();
            fadeOverlay.sprite = SolidSpriteFactory.CreateSquare();
            fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
            fadeOverlay.sortingOrder = 1000;
        }
    }
}
