using UnityEngine;

namespace IBlameYou.Systems
{
    // 전투 구역을 돌파하면 구역 가운데에 떨어지는 회복 구슬. 닿으면 체력과 마나를 일부 채우고 사라진다.
    // 줍지 않고 지나가도 청크 기록(ChunkStreamer)에 남아서, 다시 돌아오면 같은 자리에 또 있다.
    public class ArenaReward : MonoBehaviour
    {
        public const float HealRatio = 0.3f; // 최대 체력의 30%
        public const float ManaRatio = 0.5f; // 최대 마나의 50%
        private const float FloatHeight = 0.9f;
        private const float BobAmplitude = 0.15f;
        private const float BobSpeed = 3f;

        private System.Action onCollected;
        private Transform visual;
        private bool collected;
        private float phase;

        // position: 구슬이 놓일 바닥 위 가운데 (월드). dropped: 방금 떨어진 것이면 등장 이펙트를 보여준다 (청크를 다시 만들 때는 false).
        public static ArenaReward Create(Transform parent, Vector2 position, bool dropped, System.Action onCollected)
        {
            var go = new GameObject("ArenaReward");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.2f, 2f);
            trigger.offset = new Vector2(0f, 1f);

            var reward = go.AddComponent<ArenaReward>();
            reward.onCollected = onCollected;
            reward.phase = position.x;
            // 통통한 하트 구슬 (Tools/ArtGen/generate_vfx.py HeartOrb), 없으면 예전 구슬 그림
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/HeartOrb") ?? TrapCommon.Frames("Reward");
            var renderer = EffectLibrary.CreateVisual(go.transform, "Orb", frames, 10f, true,
                new Color(1f, 0.45f, 0.55f), 0.7f, 3);
            reward.visual = renderer.transform;
            reward.visual.localPosition = new Vector3(0f, FloatHeight, 0f);

            if (dropped) EffectLibrary.PlayHit(Element.Neutral, go.transform.position + Vector3.up * FloatHeight, 1.2f);
            return reward;
        }

        private void Update()
        {
            if (visual == null) return;
            float y = FloatHeight + Mathf.Sin((Time.time + phase) * BobSpeed) * BobAmplitude;
            visual.localPosition = new Vector3(0f, y, 0f);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);

        // 쓰러진 채 겹쳐 있다가 되살아난 경우에도 주울 수 있게.
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (collected) return;
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return;

            var health = player.GetComponent<HealthSystem>();
            if (health != null && health.IsDead) return;

            collected = true;
            var progression = player.GetComponent<IBlameYou.Player.PlayerProgression>();
            float healRatio = progression != null ? progression.RewardHealRatio : HealRatio; // 생명 5 특성이면 45%
            var relics = player.GetComponent<IBlameYou.Player.PlayerRelics>();
            if (relics != null && relics.Has(IBlameYou.Player.RelicId.HoneyJar))
                healRatio += IBlameYou.Player.RelicBook.HoneyJarHealBonus; // 꿀단지: +15%p
            if (health != null)
            {
                float before = health.Current;
                health.Heal(health.Max * healRatio);
                DamageNumbers.ShowHeal(player.transform.position, health.Current - before);
            }
            var mana = player.GetComponent<ManaSystem>();
            if (mana != null) mana.Restore(mana.Max * ManaRatio);

            var orbCenter = transform.position + Vector3.up * FloatHeight;
            var pop = EffectLibrary.LoadRuntimeFrames("Effects/HeartPop");
            if (pop != null) EffectLibrary.PlayOneShot(orbCenter, pop, 18f, Color.clear, 0f, false, 1.1f, sortingOrder: 7); // 하트·별이 톡톡
            else EffectLibrary.PlayHit(Element.Water, orbCenter, 1.2f);
            GameAudio.PlaySfx("Pickup", 0.7f);
            StageEvents.PostNotice($"보상: 체력 {healRatio * 100f:0}% · 마나 {ManaRatio * 100f:0}% 회복");
            onCollected?.Invoke();
            Destroy(gameObject);
        }
    }
}
