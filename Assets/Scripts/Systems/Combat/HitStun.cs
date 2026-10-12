using UnityEngine;

namespace IBlameYou.Systems
{
    // 피격 시 일정 시간 경직(IsStunned)시킨다. 이동/공격 등은 각 컨트롤러가 IsStunned를 보고 멈춘다.
    // invulnerableWhileStunned가 켜져 있으면 경직 동안 무적(플레이어용).
    [RequireComponent(typeof(HealthSystem))]
    public class HitStun : MonoBehaviour
    {
        [SerializeField] private float stunDuration = 0.4f;
        [SerializeField] private bool invulnerableWhileStunned;

        private HealthSystem health;
        private float remaining;

        public bool IsStunned { get; private set; }

        public void Configure(float duration, bool invulnerable)
        {
            stunDuration = duration;
            invulnerableWhileStunned = invulnerable;
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.Damaged += OnDamaged;
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
            if (health.IsDead) return;

            // 대지 디버프로 더 긴 경직이 남아 있으면 줄이지 않는다.
            remaining = IsStunned ? Mathf.Max(remaining, stunDuration) : stunDuration;
            IsStunned = true;
            if (invulnerableWhileStunned) health.Invulnerable = true;
        }

        // 피격과 별개로 정해진 시간만큼 경직시킨다 (대지 속성 디버프). 이미 더 긴 경직 중이면 유지.
        public void Apply(float duration)
        {
            if (health.IsDead || duration <= 0f) return;

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
