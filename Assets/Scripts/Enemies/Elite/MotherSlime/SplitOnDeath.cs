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
            var center = transform.position + Vector3.up * 0.5f;
            var body = GetComponent<Collider2D>();
            if (body != null) center = body.bounds.center;
            for (int i = 0; i < count; i++)
            {
                float offset = OverGround(WallClamped((i - (count - 1) / 2f) * spread));
                var slime = SlimeSpawner.Spawn(transform.position + new Vector3(offset, 0.3f, 0f), EffectLibrary.Config);
                if (slime == null) continue;
                EnemySquash.For(slime.gameObject).Spawn(); // 새끼는 길쭉하게 "뿅" 솟았다가 제 모양으로 (그림만)
                SlimeSplitFx.JellyString(transform, center - transform.position, slime.transform, Vector3.up * 0.3f,
                    SlimeSplitFx.GooColor, 0.22f, 0.25f + 0.05f * i); // 어미 자리와 새끼를 잇는 짧은 젤리 끈이 하나씩 톡톡 끊긴다
                if (GetComponent<BossSummon>() != null) slime.gameObject.AddComponent<BossSummon>(); // 보스 소환 어미의 새끼도 골드 없음
                if (group != null)
                {
                    slime.transform.SetParent(((Component)group).transform, true);
                    group.RegisterEnemy(slime.GetComponent<HealthSystem>());
                }
            }

            DamageNumbers.ShowText(transform.position + Vector3.up * 1.5f, "분열!", new Color(0.8f, 0.5f, 1f), 1.2f);
            SlimeSplitFx.Burst(center, SlimeSplitFx.GooColor, 6, 0.9f, 0.8f); // "뿌직-쪼옥" + 보라 젤리 방울 (그림·소리만)
        }

        // 벽·좁은 통로 앞에서 죽으면 바깥쪽 새끼가 벽 안에 생겨 끼던 것 (버그 점검 2026-11-23): 그쪽 벽까지 거리 안으로 당긴다.
        private float WallClamped(float offset)
        {
            if (Mathf.Abs(offset) < 0.01f) return offset;
            int ground = LayerMask.NameToLayer("Ground"); // TrapCommon.GroundLayer는 없으면 Default(0)를 돌려줘 어미 몸에 맞을 수 있다
            if (ground < 0) return offset;
            var origin = (Vector2)transform.position + new Vector2(0f, 0.3f);
            float dir = Mathf.Sign(offset);
            var hit = Physics2D.Raycast(origin, new Vector2(dir, 0f), Mathf.Abs(offset) + SlimeHalfWidth, 1 << ground);
            if (hit.collider == null) return offset;
            return dir * Mathf.Max(0f, hit.distance - SlimeHalfWidth);
        }

        // 낭떠러지 끝에서 죽으면 바깥쪽 새끼가 허공에 생겨 바로 떨어지던 것 (버그 점검 2026-11-25):
        // 그 자리 아래에 바닥이 없으면 어미 쪽으로 반씩 당겨 바닥 위에 내려놓는다. 끝까지 없으면 어미 자리(0).
        private float OverGround(float offset)
        {
            if (Mathf.Abs(offset) < 0.01f) return offset;
            int ground = LayerMask.NameToLayer("Ground");
            if (ground < 0) return offset;
            int mask = 1 << ground;
            for (int step = 0; step < 3; step++)
            {
                var origin = (Vector2)transform.position + new Vector2(offset, 0.3f);
                if (Physics2D.Raycast(origin, Vector2.down, GroundProbe, mask).collider != null) return offset;
                offset *= 0.5f;
            }
            return 0f;
        }

        private const float GroundProbe = 2.5f; // 어미 발밑보다 이만큼 아래까지 바닥이 없으면 낭떠러지로 본다

        private const float SlimeHalfWidth = 0.45f; // 새끼 몸 반 폭쯤 (벽에서 이만큼 띄운다)

        private void OnDestroy()
        {
            if (health != null) health.HealthChanged -= OnHealthChanged;
        }
    }
}
