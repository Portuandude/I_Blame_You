using System.Collections;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 밟으면 잠깐 흔들리다가 무너지고, 몇 초 뒤 다시 생기는 발판 (아래에서 뚫고 올라갈 수 있다).
    // 떨어졌을 때 돌아갈 "안전한 땅"으로는 치지 않는다 (FallRecovery가 UnsafeGround를 본다).
    public class CrumblingPlatform : MonoBehaviour
    {
        private const float ShakeTime = 0.55f;
        private const float RespawnTime = 3f;
        private const float ShakeAmount = 0.06f;
        public const float Thickness = 0.35f;

        private BoxCollider2D solid;
        private Transform visualRoot;
        private SpriteRenderer[] renderers;
        private bool crumbling;

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
            return platform;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (crumbling || TrapCommon.PlayerFrom(collision.collider) == null) return;

            // 위에 올라섰을 때만 (아래에서 뚫고 올라오는 중이면 발이 윗면보다 아래에 있다).
            float top = solid.bounds.max.y;
            if (collision.collider.bounds.min.y >= top - 0.12f) StartCoroutine(Crumble());
        }

        private IEnumerator Crumble()
        {
            crumbling = true;
            float elapsed = 0f;
            while (elapsed < ShakeTime)
            {
                elapsed += Time.deltaTime;
                visualRoot.localPosition = new Vector3(Random.Range(-ShakeAmount, ShakeAmount), Random.Range(-ShakeAmount, ShakeAmount), 0f);
                yield return null;
            }

            visualRoot.localPosition = Vector3.zero;
            solid.enabled = false;
            SetAlpha(0.15f);
            yield return new WaitForSeconds(RespawnTime);

            solid.enabled = true;
            SetAlpha(1f);
            crumbling = false;
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
