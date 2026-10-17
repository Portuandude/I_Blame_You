using UnityEngine;

namespace IBlameYou.Systems
{
    // 휴식 청크의 수정 제단. 처음 닿으면 체력 절반과 마나를 모두 채우고, 낭떠러지에 떨어졌을 때 돌아올 자리가 된다.
    public class Checkpoint : MonoBehaviour
    {
        private const float HealRatio = 0.5f;

        private bool activated;
        private SpriteRenderer glow;
        private System.Action onActivated;

        // position: 제단이 놓일 바닥 위 가운데 (월드).
        public static Checkpoint Create(Transform parent, Vector2 position, bool alreadyActivated, System.Action onActivated)
        {
            var go = new GameObject("Checkpoint");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.6f, 3f);
            trigger.offset = new Vector2(0f, 1.5f);

            var checkpoint = go.AddComponent<Checkpoint>();
            checkpoint.onActivated = onActivated;
            var frames = TrapCommon.Frames("Checkpoint");
            checkpoint.glow = EffectLibrary.CreateVisual(go.transform, "Crystal", frames, 8f, true, new Color(0.4f, 0.85f, 1f), 0.9f, 1);
            checkpoint.glow.transform.localPosition = new Vector3(0f, 1f, 0f);
            checkpoint.SetActivated(alreadyActivated);
            return checkpoint;
        }

        private void SetActivated(bool value)
        {
            activated = value;
            var color = glow.color;
            glow.color = new Color(color.r, color.g, color.b, value ? 1f : 0.55f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;

            var recovery = player.GetComponent<FallRecovery>();
            if (recovery != null) recovery.SetSafePoint(transform.position + Vector3.up * ChunkStreamer.SpawnLift);
            if (activated) return;

            SetActivated(true);
            var health = player.GetComponent<HealthSystem>();
            if (health != null) health.Heal(health.Max * HealRatio);
            var mana = player.GetComponent<ManaSystem>();
            if (mana != null) mana.Restore(mana.Max);

            EffectLibrary.PlayHit(Element.Water, transform.position + Vector3.up, 1.5f);
            GameAudio.PlaySfx("Hit_Water", 0.7f);
            StageEvents.PostNotice("체크포인트: 체력과 마나 회복");
            onActivated?.Invoke();
        }
    }
}
