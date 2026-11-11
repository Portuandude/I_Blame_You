using System.Collections;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 플레이어나 적이 밟으면 잠깐 흔들리다가 무너지고, 몇 초 뒤 다시 생기는 발판 (아래에서 뚫고 올라갈 수 있다).
    // 떨어졌을 때 돌아갈 "안전한 땅"으로는 치지 않는다 (FallRecovery가 UnsafeGround를 본다).
    public class CrumblingPlatform : MonoBehaviour
    {
        private const float ShakeTime = 0.55f;
        private const float RespawnTime = 3f;
        private const float ShakeAmount = 0.06f;
        private const float WarnTime = 0.6f;     // 다시 생기기 전 윤곽이 깜빡이는 시간
        private const float WarnBlink = 0.1f;    // 깜빡임 간격
        private const float GoneAlpha = 0.15f;
        private const float WarnAlpha = 0.5f;
        public const float Thickness = 0.35f;
        private const float SoundRange = 12f;    // 카메라가 이만큼 가까울 때만 소리 (FlameJetTrap과 같다)

        private BoxCollider2D solid;
        private Transform visualRoot;
        private SpriteRenderer[] renderers;
        private bool crumbling;
        private int width;

        // topLeft: 발판 윗면의 왼쪽 끝 (월드), width: 칸 수.
        public static CrumblingPlatform Create(Transform parent, Vector2 topLeft, int width)
        {
            var go = new GameObject("CrumblingPlatform");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(topLeft.x + width / 2f, topLeft.y - Thickness / 2f, 0f);
            go.layer = TrapCommon.GroundLayer;
            go.AddComponent<UnsafeGround>();

            var platform = go.AddComponent<CrumblingPlatform>();
            platform.solid = go.AddComponent<BoxCollider2D>();
            platform.solid.size = new Vector2(width, Thickness);
            platform.solid.usedByEffector = true;
            platform.solid.sharedMaterial = PhysicsMaterialFactory.Frictionless();
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;

            platform.visualRoot = new GameObject("Visual").transform;
            platform.visualRoot.SetParent(go.transform, false);
            var sprite = TrapCommon.Frame("Crumble");
            for (int i = 0; i < width; i++)
            {
                TrapCommon.AddSprite(platform.visualRoot, $"Plank_{i}", sprite, new Vector2(i - (width - 1) / 2f, -0.05f),
                    new Vector2(1f, Thickness + 0.1f), new Color(0.6f, 0.45f, 0.3f), 0);
            }

            platform.renderers = platform.visualRoot.GetComponentsInChildren<SpriteRenderer>();
            platform.width = width;
            return platform;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (crumbling || !Crumbles(collision.collider)) return;

            // 위에 올라섰을 때만 (아래에서 뚫고 올라오는 중이면 발이 윗면보다 아래에 있다).
            float top = solid.bounds.max.y;
            if (collision.collider.bounds.min.y >= top - 0.12f) StartCoroutine(Crumble(ShakeTimeFor(collision.collider)));
        }

        // 플레이어나 살아 있는 적(슬라임·고블린 등)이 올라서면 무너진다. 무너지면 위의 적도 떨어지고,
        // 구덩이 아래 낙사선(ChunkStreamer.KillLine)에 닿은 적은 처치로 사라진다 (다시 생기지 않는다).
        private static bool Crumbles(Collider2D other)
        {
            if (TrapCommon.PlayerFrom(other) != null) return true;
            var enemy = other != null ? other.GetComponentInParent<IBlameYou.Enemies.EnemyController>() : null;
            if (enemy == null) return false;
            var health = enemy.GetComponent<HealthSystem>();
            return health != null && !health.IsDead;
        }

        // 젤리 방석을 가진 플레이어가 밟으면 더 오래 흔들린다 (적이 밟으면 늘 기본 시간).
        private static float ShakeTimeFor(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return ShakeTime;
            var relics = player.GetComponent<IBlameYou.Player.PlayerRelics>();
            return relics != null && relics.Has(IBlameYou.Player.RelicId.JellyCushion)
                ? IBlameYou.Player.RelicBook.JellyCushionShakeTime : ShakeTime;
        }

        private IEnumerator Crumble(float shakeTime)
        {
            crumbling = true;
            if (PlayerNear()) GameAudio.PlaySfx("Crumble", 0.7f);
            float elapsed = 0f;
            while (elapsed < shakeTime)
            {
                elapsed += Time.deltaTime;
                visualRoot.localPosition = new Vector3(Random.Range(-ShakeAmount, ShakeAmount), Random.Range(-ShakeAmount, ShakeAmount), 0f);
                yield return null;
            }

            visualRoot.localPosition = Vector3.zero;
            solid.enabled = false;
            SetAlpha(GoneAlpha);
            // 무너질 때 발판 칸마다 나무 먼지가 펑 (발판 윗면 기준)
            float topY = transform.position.y + Thickness / 2f;
            for (int i = 0; i < width; i++)
                VfxPlayer.Dust(new Vector3(transform.position.x + i - (width - 1) / 2f, topY, 0f), 0.6f);
            yield return new WaitForSeconds(RespawnTime - WarnTime);

            // 다시 생기기 직전: 흐린 윤곽이 깜빡여 "곧 생긴다"를 알린다
            float warn = 0f;
            while (warn < WarnTime)
            {
                SetAlpha(Mathf.FloorToInt(warn / WarnBlink) % 2 == 0 ? WarnAlpha : GoneAlpha);
                warn += Time.deltaTime;
                yield return null;
            }

            solid.enabled = true;
            SetAlpha(1f);
            if (PlayerNear()) GameAudio.PlaySfx("Crumble_Back", 0.5f);
            VfxPlayer.AimGlint(new Vector3(transform.position.x + (width - 1) / 2f, topY + 0.15f, 0f)); // 오른쪽 끝에서 작게 반짝
            crumbling = false;
        }

        private bool PlayerNear()
        {
            var cam = Camera.main;
            return cam != null && Mathf.Abs(cam.transform.position.x - transform.position.x) < SoundRange;
        }

        private void SetAlpha(float alpha)
        {
            foreach (var renderer in renderers)
            {
                var color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }
        }
    }
}
