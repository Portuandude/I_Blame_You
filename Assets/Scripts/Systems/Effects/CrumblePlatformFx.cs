using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 무너지는 발판 연출 (그림·소리만, 흔들림·사라짐·다시 생기는 시간과 판정은 CrumblingPlatform 그대로, 2026-11-14).
    // - Splinters: 무너지는 순간 칸마다 나무 조각이 튀어 떨어진다 (화면 안만)
    // - Creak: 흔들리는 동안 발판 밑에서 작은 나무 부스러기가 톡 떨어진다
    // - Gather: 다시 생기기 전 깜빡임 동안 발판 아래에서 나무 조각이 모여 올라온다
    // - Back: 다시 생기는 순간 양 끝 반짝 + "Crumble_Back" (카메라 가까이, 0.15초 간격 제한으로 여러 발판이 동시에 생겨도 겹치지 않게)
    // 판자가 차례로 톡 커지는 것과 흔들릴 때 어두워지는 것은 CrumblingPlatform이 직접 그린다 (판자 그림을 가진 곳).
    public class CrumblePlatformFx : MonoBehaviour
    {
        public const float SoundRange = 12f;      // 카메라와의 거리 (가로·세로 모두, 세로 방에서 먼 층 소리가 나지 않게)
        private const float BackSfxGap = 0.15f;
        private static readonly Color Wood = new Color(0.6f, 0.45f, 0.3f, 0.95f);
        private static readonly Color WoodDark = new Color(0.42f, 0.3f, 0.2f, 0.95f);

        private static float lastBackSfx = -10f;

        // 모여드는 나무 조각 하나 (중력 없이 from → to로 감속하며 날아오고, 도착할 때쯤 옅어진다)
        private SpriteRenderer sprite;
        private Vector3 from;
        private Vector3 to;
        private Color color;
        private float lifetime;
        private float age;

        public static bool CameraNear(Vector3 position)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector2 d = cam.transform.position - position;
            return d.sqrMagnitude <= SoundRange * SoundRange;
        }

        // top: 발판 윗면 가운데, width: 칸 수.
        public static void Splinters(Vector3 top, int width)
        {
            if (!SlimeSplitFx.OnScreen(top)) return;
            for (int i = 0; i < width; i++)
            {
                var cell = top + new Vector3(i - (width - 1) / 2f, -0.15f, 0f);
                for (int k = 0; k < 2; k++)
                {
                    var v = new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(0.4f, 1.8f), 0f);
                    SlimeSplitFx.Drop(cell + new Vector3(Random.Range(-0.35f, 0.35f), 0f, 0f), v,
                        k == 0 ? Wood : WoodDark, Random.Range(0.07f, 0.12f));
                }
            }
        }

        // bottom: 발판 아랫면 가운데, width: 칸 수. 칸 하나를 골라 부스러기 하나.
        public static void Creak(Vector3 bottom, int width)
        {
            if (!SlimeSplitFx.OnScreen(bottom)) return;
            var cell = bottom + new Vector3(Random.Range(0, width) - (width - 1) / 2f + Random.Range(-0.4f, 0.4f), 0f, 0f);
            SlimeSplitFx.Drop(cell, new Vector3(Random.Range(-0.3f, 0.3f), -0.2f, 0f), WoodDark, Random.Range(0.05f, 0.08f));
        }

        // center: 발판 가운데, width: 칸 수, duration: 도착까지 시간 (깜빡임 시간).
        public static void Gather(Vector3 center, int width, float duration)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            for (int i = 0; i < width; i++)
            {
                var cell = center + new Vector3(i - (width - 1) / 2f, 0f, 0f);
                for (int k = 0; k < 2; k++)
                {
                    var to = cell + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.05f, 0.05f), 0f);
                    var from = to + new Vector3(Random.Range(-0.5f, 0.5f), -Random.Range(0.7f, 1.1f), 0f);
                    Chip(from, to, k == 0 ? Wood : WoodDark, Random.Range(0.06f, 0.1f), duration * Random.Range(0.8f, 1f));
                }
            }
        }

        // top: 발판 윗면 가운데, width: 칸 수.
        public static void Back(Vector3 top, int width)
        {
            if (CameraNear(top) && (Time.time - lastBackSfx >= BackSfxGap || Time.time < lastBackSfx))
            {
                lastBackSfx = Time.time;
                GameAudio.PlaySfx("Crumble_Back", 0.5f);
            }
            if (!SlimeSplitFx.OnScreen(top)) return;
            float half = (width - 1) / 2f;
            VfxPlayer.AimGlint(top + new Vector3(half, 0.15f, 0f));
            if (width > 1) VfxPlayer.AimGlint(top + new Vector3(-half, 0.15f, 0f));
        }

        private static void Chip(Vector3 from, Vector3 to, Color color, float size, float lifetime)
        {
            var go = new GameObject("CrumbleGatherChip");
            go.transform.position = from;
            go.transform.localScale = Vector3.one * size;
            var fx = go.AddComponent<CrumblePlatformFx>();
            fx.sprite = go.AddComponent<SpriteRenderer>();
            fx.sprite.sprite = SolidSpriteFactory.CreateSquare();
            fx.sprite.sortingOrder = 6;
            fx.from = from;
            fx.to = to;
            fx.color = color;
            fx.lifetime = Mathf.Max(0.05f, lifetime);
            fx.sprite.color = new Color(color.r, color.g, color.b, 0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            float ease = 1f - (1f - t) * (1f - t); // 처음엔 빠르게, 도착할 때 느리게
            transform.position = Vector3.Lerp(from, to, ease);
            transform.rotation = Quaternion.Euler(0f, 0f, (1f - t) * 220f);
            // 처음 25%에 나타나고 마지막 25%에 발판 속으로 스며들 듯 옅어진다
            float alpha = t < 0.25f ? t / 0.25f : (t > 0.75f ? (1f - t) / 0.25f : 1f);
            sprite.color = new Color(color.r, color.g, color.b, color.a * alpha);
        }
    }
}
