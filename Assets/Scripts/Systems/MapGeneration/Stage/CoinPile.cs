using UnityEngine;

namespace IBlameYou.Systems
{
    // 동전 무더기 (FeatureKind.CoinPile): 위험한 길 끝에 놓인 작은 골드 보상. 살아 있는 플레이어가 닿으면 골드를 받고 사라진다.
    // 동전이 무더기 자리에서 톡 튀었다가 플레이어에게 빨려 간다 (RunGold.OnCoinPile). 방마다 한 번만: 주운 기록은 ChunkStreamer가 들고 있다.
    // 가끔 반짝여서 멀리서도 보인다. 몸이 없어 길을 막지 않는다.
    public class CoinPile : MonoBehaviour
    {
        private const float CoinScale = 0.6f;
        private static readonly Vector3[] CoinOffsets =
        {
            new Vector3(-0.18f, 0.12f, 0f), new Vector3(0.18f, 0.12f, 0f), new Vector3(0f, 0.32f, 0f)
        };
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);
        private const float SparkleInterval = 1.4f;
        private const float SparkleTime = 0.35f;
        private const float SparkleSize = 0.35f;

        private int amount;
        private System.Action onCollected;
        private bool collected;
        private SpriteRenderer sparkle;
        private float sparkleTimer;

        // position: 무더기가 놓이는 서는 칸의 바닥 가운데 (월드). amount: 받을 골드.
        public static CoinPile Create(Transform parent, Vector2 position, int amount, System.Action onCollected)
        {
            var go = new GameObject("CoinPile");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1f, 1.4f);
            trigger.offset = new Vector2(0f, 0.7f);

            var pile = go.AddComponent<CoinPile>();
            pile.amount = amount;
            pile.onCollected = onCollected;
            pile.sparkleTimer = (position.x * 0.37f) % SparkleInterval; // 무더기마다 반짝이는 때가 다르게

            // 동전 그림(Resources/Effects/Coin, 몬스터 골드와 같은 것) 셋을 쌓는다. 첫 프레임만 써서 가만히 놓인 무더기처럼
            var frames = EffectLibrary.LoadRuntimeFrames("Effects/Coin");
            for (int i = 0; i < CoinOffsets.Length; i++)
            {
                var coin = new GameObject("Coin");
                coin.transform.SetParent(go.transform, false);
                coin.transform.localPosition = CoinOffsets[i];
                var renderer = coin.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 3 + i;
                if (frames != null && frames.Length > 0)
                {
                    renderer.sprite = frames[i % frames.Length];
                    coin.transform.localScale = new Vector3(CoinScale, CoinScale, 1f);
                }
                else
                {
                    renderer.sprite = SolidSpriteFactory.CreateCircle();
                    renderer.color = GoldColor;
                    coin.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
                }
            }

            var sparkleGo = new GameObject("Sparkle");
            sparkleGo.transform.SetParent(go.transform, false);
            sparkleGo.transform.localPosition = new Vector3(0.15f, 0.5f, 0f);
            pile.sparkle = sparkleGo.AddComponent<SpriteRenderer>();
            pile.sparkle.sprite = SolidSpriteFactory.CreateCircle();
            pile.sparkle.color = new Color(1f, 0.97f, 0.75f, 0f);
            pile.sparkle.sortingOrder = 7;
            return pile;
        }

        private void Update()
        {
            if (collected || sparkle == null) return;
            sparkleTimer += Time.deltaTime;
            if (sparkleTimer >= SparkleInterval) sparkleTimer -= SparkleInterval;
            // 짧게 커졌다 작아지는 반짝 (나머지 시간은 안 보임)
            float t = sparkleTimer / SparkleTime;
            float glow = t < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
            sparkle.color = new Color(1f, 0.97f, 0.75f, 0.9f * glow);
            float size = SparkleSize * glow;
            sparkle.transform.localScale = new Vector3(size, size, 1f);
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
            if (IBlameYou.Core.RunStats.Ended) return; // 판이 끝난 뒤에는 줍지 않고 그대로 둔다 (기록에도 남기지 않음)

            collected = true;
            int got = IBlameYou.Core.RunGold.OnCoinPile(player.transform, transform.position + Vector3.up * 0.4f, amount);
            if (got > 0) StageEvents.PostNotice($"동전 무더기: 골드 +{got}");
            onCollected?.Invoke();
            Destroy(gameObject);
        }
    }
}
