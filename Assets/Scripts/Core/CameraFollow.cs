using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 대상(플레이어)을 부드럽게 따라가고, 대상이 실제로 피해를 입으면 화면을 흔든다.
    // bounds가 지정되면 카메라 시야가 그 영역(방) 밖으로 벗어나지 않게 제한한다.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private Vector2 offset = new Vector2(0f, 1f);
        [SerializeField] private float zDistance = -10f;

        [Header("Look Ahead")]
        [Tooltip("움직이는 쪽을 이만큼(유닛) 더 보여 준다")]
        [SerializeField] private float lookAheadDistance = 2f;
        [SerializeField] private float lookAheadSmoothTime = 0.6f;
        [Tooltip("이보다 빨리 떨어지면 아래를 더 보여 주기 시작 (유닛/초)")]
        [SerializeField] private float fallLookStartSpeed = 8f;
        [SerializeField] private float fallLookFullSpeed = 18f;
        [SerializeField] private float fallLookDistance = 3f;
        [SerializeField] private float fallLookSmoothTime = 0.25f;

        [Header("Shake On Hit")]
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeMagnitude = 0.3f;

        private Camera cam;
        private Transform target;
        private Rigidbody2D targetBody;
        private HealthSystem targetHealth;
        private float lookX, lookXVelocity;   // 앞보기: 움직이는 쪽으로 더 보기
        private float lookY, lookYVelocity;   // 빠르게 떨어질 때 아래로 더 보기
        private Rect? bounds;

        // 흔들림을 얹기 전의 순수 추적 위치. SmoothDamp 상태가 흔들림에 오염되지 않도록 따로 들고 있는다.
        private Vector3 followPosition;
        private Vector3 velocity;

        private float shakeTimeRemaining;
        private float currentShakeDuration;
        private float currentShakeMagnitude;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        // 대상이 HealthSystem을 가지고 있으면 피격(Damaged) 때마다 자동으로 흔들린다.
        public void SetTarget(Transform newTarget)
        {
            if (targetHealth != null) targetHealth.Damaged -= OnTargetDamaged;

            target = newTarget;
            targetBody = newTarget != null ? newTarget.GetComponent<Rigidbody2D>() : null;
            targetHealth = newTarget != null ? newTarget.GetComponent<HealthSystem>() : null;
            if (targetHealth != null) targetHealth.Damaged += OnTargetDamaged;

            SnapToTarget();
        }

        // 부드럽게 따라가지 않고 즉시 대상 위치(범위 안으로 제한)로 옮긴다. 방 이동처럼 순간이동할 때 쓴다.
        public void SnapToTarget()
        {
            if (target == null) return;

            lookX = lookXVelocity = 0f; // 방을 옮기면 앞보기는 다시 천천히 쌓는다
            lookY = lookYVelocity = 0f;
            followPosition = Clamp(DesiredPosition());
            velocity = Vector3.zero;
            transform.position = followPosition;
        }

        public Rect? Bounds => bounds;

        public void SetBounds(Rect worldBounds)
        {
            bounds = worldBounds;
        }

        public void Shake(float duration, float magnitude)
        {
            currentShakeDuration = duration;
            currentShakeMagnitude = magnitude;
            shakeTimeRemaining = duration;
        }

        // 메인 카메라에 CameraFollow가 있으면 흔든다 (연출용 짧은 흔들림).
        public static void ShakeMain(float duration, float magnitude)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(duration, magnitude);
        }

        private void OnTargetDamaged(float amount)
        {
            Shake(shakeDuration, shakeMagnitude);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateLook();
            followPosition = Clamp(Vector3.SmoothDamp(followPosition, DesiredPosition(), ref velocity, smoothTime));

            Vector3 shake = Vector3.zero;
            if (shakeTimeRemaining > 0f)
            {
                shakeTimeRemaining -= Time.deltaTime;
                float falloff = Mathf.Clamp01(shakeTimeRemaining / currentShakeDuration);
                shake = (Vector3)(Random.insideUnitCircle * (currentShakeMagnitude * falloff));
            }

            transform.position = followPosition + shake;
        }

        private Vector3 DesiredPosition()
        {
            return new Vector3(target.position.x + offset.x + lookX, target.position.y + offset.y + lookY, zDistance);
        }

        // 앞보기: 가로로 움직이면 그쪽으로 lookAheadDistance만큼 더 보여 준다 (멈추면 그대로 두어 화면이 왔다 갔다 하지 않게).
        // 아래 보기: 빨리 떨어질수록 아래를 더 보여 줘 세로 방·낭떠러지에서 착지할 곳이 먼저 보이게. 방 경계 제한은 그대로 받는다.
        private void UpdateLook()
        {
            if (targetBody == null) { lookX = 0f; lookY = 0f; return; }
            Vector2 v = targetBody.linearVelocity;
            float dt = Time.deltaTime;

            float wantX = lookX;
            if (Mathf.Abs(v.x) > 1f) wantX = Mathf.Sign(v.x) * lookAheadDistance;
            lookX = Mathf.SmoothDamp(lookX, wantX, ref lookXVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);

            float wantY = -fallLookDistance * Mathf.InverseLerp(fallLookStartSpeed, fallLookFullSpeed, -v.y);
            lookY = Mathf.SmoothDamp(lookY, wantY, ref lookYVelocity, fallLookSmoothTime, Mathf.Infinity, dt);
        }

        private Vector3 Clamp(Vector3 position)
        {
            if (!bounds.HasValue) return position;

            var rect = bounds.Value;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // 방이 시야보다 작으면 그 축은 가운데에 고정.
            float x = rect.width <= halfWidth * 2f
                ? rect.center.x
                : Mathf.Clamp(position.x, rect.xMin + halfWidth, rect.xMax - halfWidth);
            float y = rect.height <= halfHeight * 2f
                ? rect.center.y
                : Mathf.Clamp(position.y, rect.yMin + halfHeight, rect.yMax - halfHeight);

            return new Vector3(x, y, position.z);
        }

        private void OnDestroy()
        {
            if (targetHealth != null) targetHealth.Damaged -= OnTargetDamaged;
        }
    }
}
