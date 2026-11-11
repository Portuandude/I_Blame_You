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
        private static readonly Vector3 HoneyGlintOffset = new Vector3(0f, 1.5f, 0f); // 꿀단지 시작 높이 (머리 위, 훈장·도토리와 같게)
        private const float BobAmplitude = 0.15f;
        private const float BobSpeed = 3f;
        // 꿀단지를 가졌으면 구슬 뒤에 금빛 테두리가 숨 쉬듯 빛나고 가끔 반짝인다 ("더 많이 찬다")
        private static readonly Color HoneyRimColor = new Color(1f, 0.82f, 0.28f);
        private const float HoneyRimSize = 1.05f;      // 구슬(약 0.7유닛)보다 조금 크게
        private const float HoneyCheckInterval = 0.5f; // 유물을 다시 확인하는 간격
        private const float HoneySparkleInterval = 1.1f;

        private System.Action onCollected;
        private Transform visual;
        private bool collected;
        private float phase;
        private SpriteRenderer honeyRim;
        private float honeyCheckTimer;
        private float honeySparkleTimer;

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

            var rimGo = new GameObject("HoneyRim");
            rimGo.transform.SetParent(reward.visual, false);
            float rimScale = HoneyRimSize / Mathf.Max(0.01f, reward.visual.localScale.x); // 그림이 없어 구슬이 0.7배 원이어도 월드 크기는 같게
            rimGo.transform.localScale = new Vector3(rimScale, rimScale, 1f);
            reward.honeyRim = rimGo.AddComponent<SpriteRenderer>();
            reward.honeyRim.sprite = SolidSpriteFactory.CreateCircle();
            reward.honeyRim.color = HoneyRimColor;
            reward.honeyRim.sortingOrder = 2; // 구슬(3) 뒤
            reward.honeyRim.enabled = AnyPlayerHasHoney();

            if (dropped) EffectLibrary.PlayHit(Element.Neutral, go.transform.position + Vector3.up * FloatHeight, 1.2f);
            return reward;
        }

        private void Update()
        {
            if (visual == null) return;
            float y = FloatHeight + Mathf.Sin((Time.time + phase) * BobSpeed) * BobAmplitude;
            visual.localPosition = new Vector3(0f, y, 0f);
            UpdateHoneyRim();
        }

        private void UpdateHoneyRim()
        {
            if (honeyRim == null) return;
            honeyCheckTimer -= Time.deltaTime;
            if (honeyCheckTimer <= 0f)
            {
                honeyCheckTimer = HoneyCheckInterval;
                honeyRim.enabled = AnyPlayerHasHoney();
            }
            if (!honeyRim.enabled) return;

            var color = HoneyRimColor;
            color.a = 0.35f + 0.25f * (0.5f + 0.5f * Mathf.Sin((Time.time + phase) * 4f));
            honeyRim.color = color;

            honeySparkleTimer -= Time.deltaTime;
            if (honeySparkleTimer <= 0f)
            {
                honeySparkleTimer = HoneySparkleInterval;
                // 테두리 위쪽 가장자리에서 번갈아 좌우로 작게 반짝
                float side = Mathf.FloorToInt(Time.time / HoneySparkleInterval) % 2 == 0 ? 1f : -1f;
                VfxPlayer.AimGlint(visual.position + new Vector3(0.32f * side, 0.3f, 0f));
            }
        }

        // 둘(A·B가 함께 쓰는 캐릭터) 중 누구라도 꿀단지를 가졌는지.
        private static bool AnyPlayerHasHoney()
        {
            foreach (var relics in FindObjectsByType<IBlameYou.Player.PlayerRelics>(FindObjectsSortMode.None))
                if (relics.Has(IBlameYou.Player.RelicId.HoneyJar)) return true;
            return false;
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
            bool honey = relics != null && relics.Has(IBlameYou.Player.RelicId.HoneyJar);
            if (honey) healRatio += IBlameYou.Player.RelicBook.HoneyJarHealBonus; // 꿀단지: +15%p
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
            if (honey) VfxPlayer.HoneyGlint(player.transform, HoneyGlintOffset); // 머리 위로 작은 꿀단지가 톡
            string honeyNote = honey ? $" · 꿀단지 +{IBlameYou.Player.RelicBook.HoneyJarHealBonus * 100f:0}%" : "";
            StageEvents.PostNotice($"보상: 체력 {healRatio * 100f:0}% · 마나 {ManaRatio * 100f:0}% 회복{honeyNote}");
            onCollected?.Invoke();
            Destroy(gameObject);
        }
    }
}
