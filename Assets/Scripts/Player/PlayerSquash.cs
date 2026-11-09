using UnityEngine;

namespace IBlameYou.Player
{
    // 점프·착지 squash & stretch (Docs/ArtStyle.md "포즈는 과장되게", 15~25%).
    // 뛰어오를 때 세로로 늘어나고(가로는 좁게), 세게 떨어져 착지하면 납작해졌다가 0.12초 만에 돌아온다.
    // 그림(Visual 자식)의 크기만 바꾸고, 발 위치(PlayerMovement.FeetLocalY)가 그대로 남도록 Visual 위치를 같이 옮긴다.
    // Animator가 그 프레임 그림을 정한 뒤에 덮어쓴다 (HitStun의 젖히기는 회전만 쓰므로 겹치지 않는다).
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerSquash : MonoBehaviour
    {
        private const float JumpStretch = 0.2f;   // 세로 +20%, 가로 -12%
        private const float LandSquash = 0.22f;   // 가장 세게 떨어졌을 때 세로 -22%, 가로 +15%
        private const float JumpTime = 0.16f;
        private const float LandTime = 0.12f;
        private const float LandMinSpeed = 4f;    // 이보다 느리게 떨어지면 착지 찌그러짐 없음 (작은 턱)
        private const float LandFullSpeed = 16f;
        private const float JumpMinRise = 2f;

        private PlayerMovement movement;
        private Rigidbody2D body;
        private Transform visual;
        private Vector3 baseScale;
        private Vector3 basePosition;
        private bool wasGrounded = true;
        private float airMinVelocityY;
        private float amount;    // 양수: 늘어남, 음수: 납작
        private float duration;
        private float remaining;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            body = GetComponent<Rigidbody2D>();
            visual = transform.Find("Visual");
            if (visual != null)
            {
                baseScale = visual.localScale;
                basePosition = visual.localPosition;
            }
        }

        private void LateUpdate()
        {
            if (visual == null || movement == null || body == null) return;

            bool grounded = movement.IsGrounded;
            float vy = body.linearVelocity.y;
            if (!grounded) airMinVelocityY = Mathf.Min(airMinVelocityY, vy);

            if (wasGrounded && !grounded && vy > JumpMinRise && !movement.IsDashing && !(remaining > 0f && amount > 0f))
            {
                Begin(JumpStretch, JumpTime);
            }
            else if (!wasGrounded && grounded && airMinVelocityY < -LandMinSpeed)
            {
                Begin(-LandSquash * Mathf.InverseLerp(LandMinSpeed, LandFullSpeed, -airMinVelocityY), LandTime);
            }

            if (grounded) airMinVelocityY = 0f;
            wasGrounded = grounded;
            Apply();
        }

        // 순간이동·낭떠러지 복귀·방 이동처럼 위치를 바로 옮길 때 낙하 기록을 지운다 (PlayerMovement.ResetAirborne이 부른다).
        public void ResetAirborne()
        {
            airMinVelocityY = 0f;
            wasGrounded = true;
        }

        private void Begin(float strength, float time)
        {
            amount = strength;
            duration = time;
            remaining = time;
        }

        private void Apply()
        {
            float k = 0f;
            if (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                float t = Mathf.Clamp01(remaining / duration); // 1 → 0
                k = amount * t * t;
            }

            float sy = 1f + k;
            float sx = 1f - k * 0.6f; // 부피가 대략 유지되게 가로는 반대로 덜 바뀐다
            visual.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
            // 발(그림 원점에서 feet만큼 아래)이 제자리에 남도록: 원점 = 원래 원점 + feet × (1 - sy)
            float feet = movement.FeetLocalY - basePosition.y;
            visual.localPosition = new Vector3(basePosition.x, basePosition.y + feet * (1f - sy), basePosition.z);
        }

        private void OnDisable()
        {
            if (visual == null) return;
            visual.localScale = baseScale;
            visual.localPosition = basePosition;
        }
    }
}
