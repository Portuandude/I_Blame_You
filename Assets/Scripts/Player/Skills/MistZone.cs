using System.Collections.Generic;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 물안개 (아카데미 마법, Docs/CityDesign.md 5절 — 물 = 디버프): 커서 자리에 Duration초 동안 둥근 안개.
    // 안에 있는 적은 주는 피해 -30%(CombatStats.DamageMultiplier), 받는 피해 +15%(MistMark, IDamageModifier). 나가거나 안개가 걷히면 원래대로.
    public class MistZone : MonoBehaviour
    {
        public const float DealtMultiplier = 0.7f;
        public const float TakenMultiplier = 1.15f;
        private const float CheckInterval = 0.2f;

        private float radius;
        private float duration;
        private float age;
        private float nextCheck;
        private readonly HashSet<MistMark> inside = new HashSet<MistMark>();
        private readonly HashSet<MistMark> now = new HashSet<MistMark>(); // 이번 검사에 안에 있는 적 (검사마다 비워 다시 씀)
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private SpriteRenderer cloud;
        private float cloudAlpha = 0.28f;
        private readonly List<SpriteRenderer> layers = new List<SpriteRenderer>(); // 전용 그림 안개 띠 (pixelfx)

        public void Begin(float zoneRadius, float lifetime, Sprite[] art = null)
        {
            radius = zoneRadius;
            duration = lifetime;
            var color = ElementInfo.Color(Element.Water);
            if (art != null) cloudAlpha = 0.12f; // 그림이 있으면 둥근 판은 범위만 옅게 알린다
            color.a = cloudAlpha;
            cloud = EffectLibrary.CreateVisual(transform, "Mist", null, 0f, false, color, radius * 2f, 3);
            if (art != null && art.Length > 0)
            {
                // 낮게 깔리는 물안개 띠 셋을 원 안에 위아래로 쌓는다 (가운데가 가장 넓게). 프레임을 어긋나게 시작
                float artWidth = art[0].bounds.size.x;
                float[] ys = { -0.55f, 0f, 0.55f };
                float[] widths = { 0.8f, 1f, 0.8f };
                for (int i = 0; i < ys.Length; i++)
                {
                    var layer = EffectLibrary.CreateVisual(transform, "MistLayer", art, 12f, true, color, 1f, 4);
                    float sx = radius * 2f * widths[i] / Mathf.Max(0.01f, artWidth);
                    layer.transform.localPosition = new Vector3(0f, ys[i] * radius, 0f);
                    layer.transform.localScale = new Vector3(i == 1 ? sx : -sx, sx, 1f);
                    layer.GetComponent<SpriteFrameAnimator>()?.SkipFrames(i * 13);
                    layers.Add(layer);
                }
            }
            GameAudio.PlaySfx("Hit_Water", 0.7f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (cloud != null) // 둥실 숨 쉬는 안개, 끝날 때 옅어짐
            {
                float fade = Mathf.Clamp01((duration - age) / 0.5f) * Mathf.Clamp01(age / 0.3f);
                var c = cloud.color;
                c.a = cloudAlpha * fade * (0.85f + 0.15f * Mathf.Sin(age * 3f));
                cloud.color = c;
                foreach (var layer in layers)
                    if (layer != null) layer.color = new Color(1f, 1f, 1f, 0.9f * fade);
            }
            if (age >= nextCheck)
            {
                nextCheck += CheckInterval;
                Refresh();
            }
            if (age >= duration) Destroy(gameObject);
        }

        private void Refresh()
        {
            now.Clear();
            foreach (var col in OverlapQuery.Circle(transform.position, radius, overlaps))
            {
                var health = col.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;
                var mark = health.GetComponent<MistMark>();
                if (mark == null) mark = health.gameObject.AddComponent<MistMark>();
                if (now.Add(mark) && inside.Add(mark)) mark.Enter();
            }
            inside.RemoveWhere(leftZone);
        }

        // RemoveWhere에 넘길 판정 (검사마다 람다를 새로 만들지 않게 한 번만 만든다)
        private System.Predicate<MistMark> leftZone;

        private void Awake()
        {
            leftZone = mark =>
            {
                if (mark == null) return true;
                if (now.Contains(mark)) return false;
                mark.Exit();
                return true;
            };
        }

        private void OnDestroy()
        {
            foreach (var mark in inside) if (mark != null) mark.Exit();
            inside.Clear();
        }
    }
}
