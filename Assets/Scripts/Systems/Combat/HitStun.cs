using UnityEngine;

namespace IBlameYou.Systems
{
    // 피격 시 일정 시간 경직(IsStunned)시킨다. 이동/공격 등은 각 컨트롤러가 IsStunned를 보고 멈춘다.
    // invulnerableWhileStunned가 켜져 있으면 경직 동안 무적(플레이어용).
    // 경직 연출 (전용 애니메이션 대체): 피격 순간 몸 그림("Visual" 자식)이 뒤로 젖혀졌다가 경직이 끝날 때까지 돌아온다.
    // 회전만 쓰므로 Animator(스프라이트)·보스의 찌그러짐(크기)·좌우 반전(루트 크기)과 겹치지 않는다.
    // 스프라이트 모양으로 판정을 만드는 몸(SpriteColliderFitter, 슬라임류)은 이 회전을 빼고 판정을 만든다.
    // 젖히는 방향은 바라보는 방향 기준 "뒤"다 (맞은 방향과는 무관).
    [RequireComponent(typeof(HealthSystem))]
    public class HitStun : MonoBehaviour
    {
        [SerializeField] private float stunDuration = 0.4f;
        [SerializeField] private bool invulnerableWhileStunned;

        private const float PoseTilt = 14f;        // 도 (뒤로 젖히는 최대 각도)
        private const float PoseMaxDuration = 0.4f; // 경직이 길어도(대지 디버프) 자세는 이 안에 돌아온다

        private HealthSystem health;
        private float remaining;
        private Transform poseVisual;
        private float poseRemaining;
        private float poseDuration;

        public bool IsStunned { get; private set; }

        // 경직에 걸리지 않는 상태 (보스의 슈퍼아머).
        public bool Immune { get; set; }

        public void Configure(float duration, bool invulnerable)
        {
            stunDuration = duration;
            invulnerableWhileStunned = invulnerable;
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.Damaged += OnDamaged;
            if (GetComponent<HitFlash>() == null) gameObject.AddComponent<HitFlash>(); // 피격 번쩍임 + 파편 (프리팹 재생성 없이 적용)
        }

        private void Update()
        {
            if (!IsStunned) return;

            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                EndStun();
            }
        }

        private void OnDamaged(float amount)
        {
            if (health.IsDead || Immune) return;

            // 대지 디버프로 더 긴 경직이 남아 있으면 줄이지 않는다.
            remaining = IsStunned ? Mathf.Max(remaining, stunDuration) : stunDuration;
            IsStunned = true;
            if (invulnerableWhileStunned) health.Invulnerable = true;
            StartPose();
        }

        private void StartPose()
        {
            if (poseVisual == null) poseVisual = transform.Find("Visual");
            if (poseVisual == null) return;
            poseDuration = Mathf.Min(stunDuration, PoseMaxDuration);
            poseRemaining = poseDuration;
        }

        // Animator가 그 프레임의 자세를 쓴 뒤에 덮어쓴다.
        private void LateUpdate()
        {
            if (poseRemaining <= 0f || poseVisual == null) return;

            poseRemaining -= Time.deltaTime;
            if (poseRemaining <= 0f || health.IsDead)
            {
                ResetPose();
                return;
            }

            float t = poseRemaining / poseDuration; // 1 → 0
            // 루트의 좌우 반전(localScale.x = -1)이 회전 방향도 뒤집으므로, 같은 각도가 양쪽 모두 "뒤로" 젖힌다.
            poseVisual.localRotation = Quaternion.Euler(0f, 0f, PoseTilt * t * t);
        }

        private void ResetPose()
        {
            poseRemaining = 0f;
            if (poseVisual != null) poseVisual.localRotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            if (poseRemaining > 0f) ResetPose();
        }

        // 피격과 별개로 정해진 시간만큼 경직시킨다 (대지 속성 디버프). 이미 더 긴 경직 중이면 유지.
        public void Apply(float duration)
        {
            if (health.IsDead || Immune || duration <= 0f) return;

            IsStunned = true;
            remaining = Mathf.Max(remaining, duration);
            if (invulnerableWhileStunned) health.Invulnerable = true;
        }

        private void EndStun()
        {
            IsStunned = false;
            if (invulnerableWhileStunned) health.Invulnerable = false;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }
    }
}
