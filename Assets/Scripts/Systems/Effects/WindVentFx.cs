using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 바람 구멍 연출 (2026-11-14): 기둥 안을 위로 흐르는 흰·연초록 바람 줄과 가끔 나뭇잎,
    // 플레이어가 들어갈 때 "휘잉" WindGust + 발밑 먼지 + 바람 줄 한 묶음. 화면 안일 때만 그린다.
    // 꼭대기에서는 작은 바람 덩이 셋이 납작한 고리를 그리며 맴돌아 어디까지 오르는지 보인다 (2026-11-14).
    // WindVent.Create가 붙인다. 바람 줄은 그림만 있는 오브젝트라 판정과 상관없다.
    public class WindVentFx : MonoBehaviour
    {
        private const float StreakGap = 0.12f;       // 바람 줄 하나 나오는 간격 (기둥 폭 1칸당)
        private const float LeafMinGap = 1.2f;
        private const float LeafMaxGap = 2.4f;
        private const float GustSoundGap = 0.3f;     // 여러 기둥을 오가도 "휘잉"이 겹치지 않게
        private static readonly Color StreakColor = new Color(0.92f, 1f, 0.95f, 0.55f);
        private static readonly Color StreakGreen = new Color(0.62f, 0.95f, 0.72f, 0.5f);
        private static readonly Color LeafColor = new Color(0.45f, 0.82f, 0.4f, 0.95f);

        private static float lastGustSound = -10f;

        private const int CurlPuffs = 3;             // 꼭대기 바람 고리: 맴도는 작은 바람 덩이 수
        private const float CurlSpin = 3.2f;         // 라디안/초
        private static readonly Color CurlColor = new Color(0.9f, 1f, 0.94f, 0.6f);

        private WindVent vent;
        private SpriteRenderer[] curl;
        private float nextStreak;
        private float nextLeaf = -1f;

        public static void Attach(WindVent vent)
        {
            if (vent == null || vent.GetComponent<WindVentFx>() != null) return;
            vent.gameObject.AddComponent<WindVentFx>().vent = vent;
        }

        // 플레이어가 기둥에 들어갈 때: "휘잉" + 발밑 먼지 + 위로 솟는 바람 줄 몇 개
        public static void Enter(WindVent vent, Vector3 feet)
        {
            if (vent == null) return;
            if (Time.time - lastGustSound >= GustSoundGap || Time.time < lastGustSound)
            {
                lastGustSound = Time.time;
                GameAudio.PlaySfx("WindGust", 0.6f);
            }
            if (!SlimeSplitFx.OnScreen(feet)) return;
            VfxPlayer.Dust(feet, 0.3f);
            var fx = vent.GetComponent<WindVentFx>();
            if (fx == null) return;
            for (int i = 0; i < 4; i++) fx.SpawnStreak(feet.y + Random.Range(-0.3f, 0.8f), 1.3f);
        }

        private void Update()
        {
            if (vent == null) return;
            var middle = transform.position + new Vector3(vent.Width * 0.5f, vent.Height * 0.5f, 0f);
            var bottom = transform.position + new Vector3(vent.Width * 0.5f, 0.3f, 0f);
            var top = transform.position + new Vector3(vent.Width * 0.5f, vent.Height - 0.3f, 0f);
            if (!SlimeSplitFx.OnScreen(middle) && !SlimeSplitFx.OnScreen(bottom) && !SlimeSplitFx.OnScreen(top)) return;
            SpinCurl();

            if (Time.time >= nextStreak)
            {
                nextStreak = Time.time + StreakGap / Mathf.Max(1f, vent.Width);
                SpawnStreak(transform.position.y + Random.Range(0.1f, 1.2f), 1f);
            }

            if (nextLeaf < 0f) nextLeaf = Time.time + Random.Range(0.3f, LeafMaxGap);
            if (Time.time >= nextLeaf)
            {
                nextLeaf = Time.time + Random.Range(LeafMinGap, LeafMaxGap);
                var start = transform.position + new Vector3(Random.Range(0.2f, vent.Width - 0.2f), 0.3f, 0f);
                WindParticle.Leaf(start, vent.Top, LeafColor);
            }
        }

        // 꼭대기 바람 고리: 기둥 폭만큼 납작한 타원을 돈다. 뒤쪽(위)을 지날 때는 옅고 작게, 앞쪽(아래)은 진하게.
        private void SpinCurl()
        {
            if (curl == null)
            {
                curl = new SpriteRenderer[CurlPuffs];
                var circle = SolidSpriteFactory.CreateCircle();
                for (int i = 0; i < CurlPuffs; i++)
                {
                    var go = new GameObject("WindCurl");
                    go.transform.SetParent(transform, false);
                    curl[i] = go.AddComponent<SpriteRenderer>();
                    curl[i].sprite = circle;
                    curl[i].sortingOrder = 4;
                }
            }

            float radius = vent.Width * 0.45f;
            for (int i = 0; i < CurlPuffs; i++)
            {
                float a = Time.time * CurlSpin + i * Mathf.PI * 2f / CurlPuffs;
                float depth = (1f - Mathf.Sin(a)) * 0.5f; // 1 = 앞(아래), 0 = 뒤(위)
                curl[i].transform.localPosition = new Vector3(vent.Width * 0.5f + Mathf.Cos(a) * radius, vent.Height - 0.45f + Mathf.Sin(a) * 0.16f, 0f);
                float size = Mathf.Lerp(0.1f, 0.18f, depth);
                curl[i].transform.localScale = new Vector3(size * 1.6f, size, 1f);
                curl[i].color = new Color(CurlColor.r, CurlColor.g, CurlColor.b, CurlColor.a * Mathf.Lerp(0.35f, 1f, depth));
            }
        }

        private void SpawnStreak(float y, float speedScale)
        {
            float x = transform.position.x + Random.Range(0.15f, vent.Width - 0.15f);
            var color = Random.value < 0.35f ? StreakGreen : StreakColor;
            WindParticle.Streak(new Vector3(x, y, 0f), vent.Top, color, Random.Range(7f, 10f) * speedScale);
        }
    }

    // 바람 줄(가늘고 긴 선) 또는 나뭇잎 하나: 기둥 꼭대기(top)까지 오르며 끝에서 옅어진다. 중력 없음.
    public class WindParticle : MonoBehaviour
    {
        private SpriteRenderer sprite;
        private Color baseColor;
        private float top;
        private float speed;
        private bool leaf;
        private float phase;
        private float originX;
        private float age;

        public static void Streak(Vector3 position, float top, Color color, float speed)
        {
            var p = Make("WindStreak", position, top, color, speed, false, SolidSpriteFactory.CreateSquare());
            p.transform.localScale = new Vector3(0.05f, Random.Range(0.45f, 0.8f), 1f);
        }

        public static void Leaf(Vector3 position, float top, Color color)
        {
            var p = Make("WindLeaf", position, top, color, Random.Range(3.5f, 5f), true, SolidSpriteFactory.CreateCircle());
            p.transform.localScale = new Vector3(0.2f, 0.11f, 1f);
        }

        private static WindParticle Make(string name, Vector3 position, float top, Color color, float speed, bool leaf, Sprite shape)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var p = go.AddComponent<WindParticle>();
            p.sprite = go.AddComponent<SpriteRenderer>();
            p.sprite.sprite = shape;
            p.sprite.sortingOrder = 4;
            p.sprite.color = color;
            p.baseColor = color;
            p.top = top;
            p.speed = speed;
            p.leaf = leaf;
            p.phase = Random.Range(0f, Mathf.PI * 2f);
            p.originX = position.x;
            return p;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var pos = transform.position;
            pos.y += speed * Time.deltaTime;
            if (leaf)
            {
                // 나뭇잎: 좌우로 살랑이며 빙글 돈다
                pos.x = originX + Mathf.Sin(age * 5f + phase) * 0.25f;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 7f + phase) * 50f);
            }
            transform.position = pos;

            float left = top - pos.y;
            if (left <= 0f || age > 4f)
            {
                Destroy(gameObject);
                return;
            }
            // 처음 0.1초에 나타나고 꼭대기 1.5칸 전부터 옅어진다
            float alpha = Mathf.Min(Mathf.Clamp01(age / 0.1f), Mathf.Clamp01(left / 1.5f));
            sprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        }
    }
}
