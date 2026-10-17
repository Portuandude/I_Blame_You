using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Enemies
{
    // 어미 슬라임 패턴 (스탯 문서): 사망 시 일반 슬라임 3기로 분열한다.
    // 분열된 슬라임도 같은 방의 클리어 조건에 들어간다.
    [RequireComponent(typeof(HealthSystem))]
    public class SplitOnDeath : MonoBehaviour
    {
        [SerializeField] private int count = 3;
        [SerializeField] private float spread = 1.2f;

        private HealthSystem health;
        private bool split;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            health.HealthChanged += OnHealthChanged;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current > 0f || split) return;
            split = true;

            var group = GetComponentInParent<IEnemyGroup>(); // 전투 구역이면 분열한 슬라임도 다 잡아야 문이 열린다
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) / 2f) * spread;
                var slime = SlimeSpawner.Spawn(transform.position + new Vector3(offset, 0.3f, 0f), EffectLibrary.Config);
                if (group != null)
                {
                    slime.transform.SetParent(((Component)group).transform, true);
                    group.RegisterEnemy(slime.GetComponent<HealthSystem>());
                }
            }

            DamageNumbers.ShowText(transform.position + Vector3.up * 1.5f, "분열!", new Color(0.8f, 0.5f, 1f), 1.2f);
        }

        private void OnDestroy()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
        }
    }
}
