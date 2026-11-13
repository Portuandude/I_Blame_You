using IBlameYou.Enemies;
using UnityEngine;

namespace IBlameYou.Systems
{
    // 스위치 발판 연출 (그림·소리만, 가동 시간·출발 여유·판정은 PlatformSwitch·MovingPlatform 그대로, 2026-11-13).
    // - Stream: 룬을 맞히면 문양에서 발판까지 속성 색 빛 방울이 차례로 날아가 닿는 자리에서 반짝
    // - Wake: 잠든 발판이 깨어나면 칸마다 속성 색 반짝 조각이 튀고 "우웅-팅" (PlatformWake, 화면 안만)
    // - Depart: 출발하는 순간 양 끝 아래 작은 먼지
    // - Sleep: 가동이 끝나 다시 잠들 때 속성 색 조각이 아래로 흩날리고 내려가는 작은 종소리 (PlatformSleep, 화면 안만)
    // 판자가 톡 튀는 것·출발 여유 동안 밝아지는 것은 MovingPlatform이 직접 그린다 (판자 그림을 가진 곳).
    public class SwitchPlatformFx : MonoBehaviour
    {
        private const int StreamMotes = 5;
        private const float StreamGap = 0.06f;    // 방울 사이 간격 (초)
        private const float StreamFlight = 0.4f;  // 방울 하나가 날아가는 시간
        private const float StreamArc = 1.2f;     // 위로 휘는 높이 (유닛)

        private Transform target;
        private Vector3 from;
        private Vector3 lastTarget;
        private SpriteRenderer sprite;
        private Color color;
        private float delay;
        private float age;
        private float size;
        private bool last; // 마지막 방울만 닿는 자리에서 반짝인다

        // from: 룬 문양 자리 (월드), target: 발판. 발판이 움직이면 따라간다.
        public static void Stream(Vector3 from, Transform target, Color color)
        {
            if (target == null) return;
            var glow = new Color(Mathf.Lerp(color.r, 1f, 0.35f), Mathf.Lerp(color.g, 1f, 0.35f), Mathf.Lerp(color.b, 1f, 0.35f), 0.95f);
            for (int i = 0; i < StreamMotes; i++)
            {
                var go = new GameObject("SwitchStreamMote");
                go.transform.position = from;
                var fx = go.AddComponent<SwitchPlatformFx>();
                fx.target = target;
                fx.from = from;
                fx.lastTarget = target.position;
                fx.color = glow;
                fx.delay = i * StreamGap;
                fx.last = i == StreamMotes - 1;
                fx.size = Mathf.Lerp(0.22f, 0.14f, i / (float)(StreamMotes - 1));
                fx.sprite = go.AddComponent<SpriteRenderer>();
                fx.sprite.sprite = SolidSpriteFactory.CreateCircle();
                fx.sprite.sortingOrder = 7;
                fx.sprite.enabled = false; // 차례가 오기 전엔 보이지 않는다
                go.transform.localScale = Vector3.one * fx.size;
            }
        }

        // center: 발판 가운데, width: 칸 수.
        public static void Wake(Vector3 center, int width, Color color)
        {
            if (SlimeSplitFx.OnScreen(center)) GameAudio.PlaySfx("PlatformWake", 0.5f);
            var spark = new Color(Mathf.Lerp(color.r, 1f, 0.4f), Mathf.Lerp(color.g, 1f, 0.4f), Mathf.Lerp(color.b, 1f, 0.4f), 0.95f);
            for (int i = 0; i < width; i++)
            {
                var cell = center + new Vector3(i - (width - 1) / 2f, 0.15f, 0f);
                for (int k = 0; k < 2; k++)
                {
                    var v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(2.5f, 4.5f), 0f);
                    SlimeSplitFx.Drop(cell, v, k == 0 ? spark : new Color(1f, 1f, 1f, 0.9f), Random.Range(0.06f, 0.1f));
                }
            }
        }

        // center: 발판 가운데, width: 칸 수, thickness: 발판 두께.
        public static void Depart(Vector3 center, int width, float thickness)
        {
            if (!SlimeSplitFx.OnScreen(center)) return;
            float y = center.y - thickness / 2f;
            VfxPlayer.Dust(new Vector3(center.x - width / 2f + 0.2f, y, 0f), 0.35f);
            VfxPlayer.Dust(new Vector3(center.x + width / 2f - 0.2f, y, 0f), 0.35f);
        }

        // center: 발판 가운데, width: 칸 수.
        public static void Sleep(Vector3 center, int width, Color color)
        {
            if (SlimeSplitFx.OnScreen(center)) GameAudio.PlaySfx("PlatformSleep", 0.45f, 0f);
            var flake = new Color(color.r, color.g, color.b, 0.75f);
            for (int i = 0; i < width; i++)
            {
                var cell = center + new Vector3(i - (width - 1) / 2f + Random.Range(-0.3f, 0.3f), 0f, 0f);
                for (int k = 0; k < 2; k++)
                {
                    // 위로 살짝 떴다가 천천히 떨어지며 옅어진다
                    var v = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.4f), 0f);
                    SlimeSplitFx.Drop(cell, v, flake, Random.Range(0.06f, 0.1f));
                }
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < delay) return;
            if (target != null) lastTarget = target.position; // 발판이 사라져도 마지막 자리로 날아간다
            float t = Mathf.Clamp01((age - delay) / StreamFlight);
            if (t >= 1f)
            {
                if (last) VfxPlayer.AimGlint(lastTarget + Vector3.up * 0.2f);
                Destroy(gameObject);
                return;
            }

            sprite.enabled = true;
            float e = t * t * (3f - 2f * t);
            var pos = Vector3.Lerp(from, lastTarget, e) + Vector3.up * (StreamArc * 4f * t * (1f - t));
            transform.position = pos;
            transform.localScale = Vector3.one * size * (1f + 0.3f * Mathf.Sin(t * Mathf.PI));
            var c = color;
            c.a *= t < 0.85f ? 1f : (1f - t) / 0.15f;
            sprite.color = c;
        }
    }
}
