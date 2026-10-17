using UnityEngine;

namespace IBlameYou.Systems
{
    // 벽에 박힌 화살 발사대. 플레이어가 사정거리 안(같은 높이 근처)에 있으면 일정 간격으로 직선 화살을 쏜다 (유도 없음).
    // 쏘기 직전 발사대가 붉게 깜빡여 예고한다.
    public class ArrowTrap : MonoBehaviour
    {
        private const float Interval = 2.6f;
        private const float WarnTime = 0.45f;
        private const float Range = 14f;
        private const float HeightTolerance = 3f;
        private const float ArrowSpeed = 9f;
        private const float DamageMultiplier = 0.8f;

        private float direction;
        private float timer;
        private SpriteRenderer body;
        private Color baseColor;
        private Transform player;

        // position: 발사대 칸의 가운데 (월드). direction: -1 왼쪽, 1 오른쪽.
        public static ArrowTrap Create(Transform parent, Vector2 position, int direction)
        {
            var go = new GameObject("ArrowTrap");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trap = go.AddComponent<ArrowTrap>();
            trap.direction = direction < 0 ? -1f : 1f;
            trap.body = TrapCommon.AddSprite(go.transform, "Body", TrapCommon.Frame("ArrowTrap"), Vector2.zero, new Vector2(1f, 1f),
                new Color(0.45f, 0.4f, 0.38f), 1);
            trap.body.flipX = trap.direction > 0f;
            trap.baseColor = trap.body.color;
            trap.timer = Interval * 0.6f;
            return trap;
        }

        private void Update()
        {
            if (player == null)
            {
                var movement = FindFirstObjectByType<Player.PlayerMovement>();
                if (movement == null) return;
                player = movement.transform;
            }

            if (!InRange())
            {
                timer = Mathf.Min(timer, Interval - WarnTime - 0.3f); // 다시 들어오면 바로 쏘지 않고 예고부터
                body.color = baseColor;
                return;
            }

            timer += Time.deltaTime;
            bool warning = timer >= Interval - WarnTime;
            body.color = warning && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? new Color(1f, 0.45f, 0.4f) : baseColor;

            if (timer >= Interval)
            {
                timer = 0f;
                Fire();
            }
        }

        private bool InRange()
        {
            Vector2 offset = player.position - transform.position;
            return offset.x * direction > 0f && Mathf.Abs(offset.x) <= Range && Mathf.Abs(offset.y) <= HeightTolerance;
        }

        private void Fire()
        {
            var go = new GameObject("TrapArrow");
            go.transform.position = transform.position + new Vector3(direction * 0.6f, 0.05f, 0f);
            EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.ArrowFrames, 12f, true, new Color(0.9f, 0.8f, 0.5f), 0.5f);
            go.AddComponent<Projectile>().Launch(TrapCommon.Source, false, DamageKind.Physical, Element.Neutral, new Vector2(direction, 0f),
                ArrowSpeed, 0.2f, Range * 1.3f, false, DamageMultiplier);
        }
    }
}
