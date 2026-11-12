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
        // 젤리 방석: 흔들리는 대신 분홍빛으로 말랑하게 눌렸다 튀어 오른다
        private static readonly Color JellyTint = new Color(1f, 0.62f, 0.82f);
        private const float JellySquash = 0.22f;   // 세로로 눌리는 최대 비율
        private const float JellyWobbles = 2.5f;   // 흔들림 시간 동안 눌렸다 펴지는 횟수

        private BoxCollider2D solid;
        private Transform visualRoot;
        private SpriteRenderer[] renderers;
        private Color[] baseColors;
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
            platform.baseColors = new Color[platform.renderers.Length];
            for (int i = 0; i < platform.renderers.Length; i++) platform.baseColors[i] = platform.renderers[i].color;
            platform.width = width;
            return platform;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (crumbling || !Crumbles(collision.collider)) return;

            // 위에 올라섰을 때만 (아래에서 뚫고 올라오는 중이면 발이 윗면보다 아래에 있다).
            float top = solid.bounds.max.y;
            if (collision.collider.bounds.min.y >= top - 0.12f) StartCoroutine(Crumble(ShakeTimeFor(collision.collider), HasJellyCushion(collision.collider)));
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
            return HasJellyCushion(other) ? IBlameYou.Player.RelicBook.JellyCushionShakeTime : ShakeTime;
        }

        private static bool HasJellyCushion(Collider2D other)
        {
            var player = TrapCommon.PlayerFrom(other);
            if (player == null) return false;
            var relics = player.GetComponent<IBlameYou.Player.PlayerRelics>();
            return relics != null && relics.Has(IBlameYou.Player.RelicId.JellyCushion);
        }

        private IEnumerator Crumble(float shakeTime, bool jelly)
        {
            crumbling = true;
            if (PlayerNear()) GameAudio.PlaySfx(jelly ? "JellySquish" : "Crumble", 0.7f); // 젤리 방석이면 말랑한 "뭉-뿅"
            float elapsed = 0f;
            while (elapsed < shakeTime)
            {
                elapsed += Time.deltaTime;
                if (jelly) Jiggle(Mathf.Clamp01(elapsed / shakeTime));
                else visualRoot.localPosition = new Vector3(Random.Range(-ShakeAmount, ShakeAmount), Random.Range(-ShakeAmount, ShakeAmount), 0f);
                yield return null;
            }

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localScale = Vector3.one;
            if (jelly) SetTint(0f);
            solid.enabled = false;
            SetAlpha(GoneAlpha);
            // 무너질 때 발판 칸마다 나무 먼지가 펑 (발판 윗면 기준), 젤리 방석이면 분홍 방울도
            float topY = transform.position.y + Thickness / 2f;
            for (int i = 0; i < width; i++)
            {
                var cell = new Vector3(transform.position.x + i - (width - 1) / 2f, topY, 0f);
                VfxPlayer.Dust(cell, 0.6f);
                if (jelly) VfxPlayer.JellyDrops(cell, 2); // 젤리 방석이면 먼지와 함께 분홍 방울이 톡톡
            }
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

        // 젤리 방석으로 버티는 동안: 분홍빛이 차오르며 세로로 눌렸다 펴지고(가로는 반대로 늘어남),
        // 끝무렵엔 위로 살짝 튀어 오른다. 윗면 높이가 거의 그대로 보이도록 눌린 만큼 아래로 내린다.
        private void Jiggle(float t)
        {
            float wave = Mathf.Sin(t * JellyWobbles * 2f * Mathf.PI);
            float squash = JellySquash * Mathf.Abs(wave) * (1f - 0.5f * t); // 점점 덜 눌림
            float bounce = t > 0.8f ? Mathf.Sin((t - 0.8f) / 0.2f * Mathf.PI) * 0.08f : 0f;
            visualRoot.localScale = new Vector3(1f + squash * 0.3f, 1f - squash, 1f);
            visualRoot.localPosition = new Vector3(0f, -squash * Thickness * 0.5f + bounce, 0f);
            SetTint(Mathf.Min(1f, t * 4f) * (0.7f + 0.3f * Mathf.Abs(wave)));
        }

        // amount 0이면 원래 나무색, 1이면 젤리 분홍. 알파는 건드리지 않는다.
        private void SetTint(float amount)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var color = Color.Lerp(baseColors[i], JellyTint, amount);
                color.a = renderers[i].color.a;
                renderers[i].color = color;
            }
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
