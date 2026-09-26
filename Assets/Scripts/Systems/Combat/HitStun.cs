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

            IsStunned = true;
            remaining = stunDuration;
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
