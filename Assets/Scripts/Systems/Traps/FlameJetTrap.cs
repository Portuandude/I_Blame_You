using UnityEngine;

namespace IBlameYou.Systems
{
    // 바닥 분출구: 쉬다가 → 연기로 예고하고 → 불기둥을 뿜는다. 불기둥에 닿으면 불 속성 피해 + 화상 확률.
    public class FlameJetTrap : MonoBehaviour
    {
        private const float IdleTime = 2.2f;
        private const float WarnTime = 0.7f;
        private const float BurnTime = 1.3f;
        private const float ColumnHeight = 3.2f;
        private const float TickInterval = 0.5f;
        private const float DamageMultiplier = 0.6f;
        private const float SoundRange = 12f;

        private enum Phase { Idle, Warn, Burn }

        private Phase phase = Phase.Idle;
        private float timer;
        private float nextTick;
        private BoxCollider2D column;
        private SpriteRenderer flame;
        private SpriteRenderer smoke;

        // position: 분출구가 놓인 바닥 칸의 아래 가운데 (월드). 분출구마다 시작 박자를 다르게 해서 동시에 뿜지 않게 한다.
        public static FlameJetTrap Create(Transform parent, Vector2 position, float phaseOffset)
        {
            var go = new GameObject("FlameJet");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            TrapCommon.AddSprite(go.transform, "Vent", TrapCommon.Frame("FlameVent"), new Vector2(0f, 0.15f), new Vector2(1f, 0.3f),
                new Color(0.3f, 0.25f, 0.25f), 1);

            var trap = go.AddComponent<FlameJetTrap>();
            trap.column = go.AddComponent<BoxCollider2D>();
            trap.column.isTrigger = true;
            trap.column.size = new Vector2(0.8f, ColumnHeight);
            trap.column.offset = new Vector2(0f, ColumnHeight / 2f + 0.2f);
            trap.column.enabled = false;

            var flameFrames = TrapCommon.Frames("Flame");
            trap.flame = EffectLibrary.CreateVisual(go.transform, "Flame", flameFrames, 14f, true, new Color(1f, 0.5f, 0.15f, 0.85f), 1f, 3);
            trap.flame.transform.localPosition = new Vector3(0f, ColumnHeight / 2f + 0.2f, 0f);
            if (flameFrames == null) trap.flame.transform.localScale = new Vector3(0.8f, ColumnHeight, 1f);
            trap.flame.gameObject.SetActive(false);

            trap.smoke = EffectLibrary.CreateVisual(go.transform, "Smoke", TrapCommon.Frames("Smoke"), 10f, true,
                new Color(0.5f, 0.45f, 0.45f, 0.6f), 0.6f, 3);
            trap.smoke.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            trap.smoke.gameObject.SetActive(false);

            trap.timer = IdleTime * Mathf.Repeat(phaseOffset, 1f);
            return trap;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            switch (phase)
            {
                case Phase.Idle:
                    if (timer >= IdleTime) Enter(Phase.Warn);
                    break;
                case Phase.Warn:
                    if (timer >= WarnTime) Enter(Phase.Burn);
                    break;
                case Phase.Burn:
                    if (timer >= BurnTime) Enter(Phase.Idle);
                    break;
            }
        }

        private void Enter(Phase next)
        {
            phase = next;
            timer = 0f;
            smoke.gameObject.SetActive(next == Phase.Warn);
            flame.gameObject.SetActive(next == Phase.Burn);
            column.enabled = next == Phase.Burn;
            if (next == Phase.Burn)
            {
                nextTick = 0f;
                if (PlayerNear()) GameAudio.PlaySfx("Cast_Fire", 0.6f);
            }
        }

        private bool PlayerNear()
        {
            var cam = Camera.main;
            return cam != null && Mathf.Abs(cam.transform.position.x - transform.position.x) < SoundRange;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (phase != Phase.Burn || Time.time < nextTick) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;

            var health = player.GetComponent<HealthSystem>();
            if (health == null || health.IsDead || health.Invulnerable) return;

            TrapCommon.Strike(player, DamageKind.Magic, Element.Fire, DamageMultiplier);
            StatusEffects.TryApply(player, StatusDebuff.Burn, TrapCommon.Source);
            nextTick = Time.time + TickInterval;
        }
    }
}
