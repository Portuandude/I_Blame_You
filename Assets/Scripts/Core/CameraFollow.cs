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

        [Header("Shake On Hit")]
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeMagnitude = 0.3f;

        private Camera cam;
        private Transform target;
        private HealthSystem targetHealth;
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
            targetHealth = newTarget != null ? newTarget.GetComponent<HealthSystem>() : null;
            if (targetHealth != null) targetHealth.Damaged += OnTargetDamaged;

            if (target != null)
            {
                followPosition = Clamp(DesiredPosition());
                velocity = Vector3.zero;
                transform.position = followPosition;
            }
        }

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

        private void OnTargetDamaged(float amount)
        {
            Shake(shakeDuration, shakeMagnitude);
        }

        private void LateUpdate()
        {
            if (target == null) return;

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
            return new Vector3(target.position.x + offset.x, target.position.y + offset.y, zDistance);
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
