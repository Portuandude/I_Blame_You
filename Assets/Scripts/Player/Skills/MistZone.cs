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
        private SpriteRenderer cloud;

        public void Begin(float zoneRadius, float lifetime)
        {
            radius = zoneRadius;
            duration = lifetime;
            var color = ElementInfo.Color(Element.Water);
            color.a = 0.28f;
            cloud = EffectLibrary.CreateVisual(transform, "Mist", null, 0f, false, color, radius * 2f, 3);
            GameAudio.PlaySfx("Hit_Water", 0.7f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (cloud != null) // 둥실 숨 쉬는 안개, 끝날 때 옅어짐
            {
                float fade = Mathf.Clamp01((duration - age) / 0.5f) * Mathf.Clamp01(age / 0.3f);
                var c = cloud.color;
                c.a = 0.28f * fade * (0.85f + 0.15f * Mathf.Sin(age * 3f));
                cloud.color = c;
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
            var now = new HashSet<MistMark>();
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
            {
                var health = col.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;
                var mark = health.GetComponent<MistMark>();
                if (mark == null) mark = health.gameObject.AddComponent<MistMark>();
                if (now.Add(mark) && inside.Add(mark)) mark.Enter();
            }
            inside.RemoveWhere(mark =>
            {
                if (mark == null) return true;
                if (now.Contains(mark)) return false;
                mark.Exit();
                return true;
            });
        }

        private void OnDestroy()
        {
            foreach (var mark in inside) if (mark != null) mark.Exit();
            inside.Clear();
        }
    }
}
