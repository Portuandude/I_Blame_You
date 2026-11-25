using System;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 바위 가시: 예고 시간 동안 대상의 발밑을 따라가다가 땅에서 솟아올라 범위 안의 적에게 한 번 피해를 준다.
    // 대상이 없으면 처음 자리에서 솟는다.
    public class GroundSpike : MonoBehaviour
    {
        private const float Lifetime = 0.45f; // 솟아오른 뒤 남아 있는 시간

        private CombatStats owner;
        private float halfWidth;
        private float multiplier;
        private float delay;
        private HealthSystem target;
        private LayerMask groundMask;
        private Action<HealthSystem> onHit;
        private float age;
        private bool struck;
        private GameObject telegraph;
        private Sprite[] art;       // 전용 그림 (pixelfx). 피해와 따로 끝까지 재생하고 사라진다
        private float artLead;
        private bool artShown;

        public void Begin(CombatStats attacker, float width, float damageMultiplier, float strikeDelay, HealthSystem followTarget,
            LayerMask ground, Action<HealthSystem> hit, Sprite[] spellArt = null, float spellArtLead = 0f)
        {
            owner = attacker;
            halfWidth = width;
            multiplier = damageMultiplier;
            delay = strikeDelay;
            target = followTarget;
            groundMask = ground;
            onHit = hit;
            art = spellArt;
            artLead = spellArtLead;

            // 예고: 솟아오를 자리에 납작한 갈색 표시.
            var color = ElementInfo.Color(Element.Earth);
            color.a = 0.55f;
            var marker = EffectLibrary.CreateVisual(transform, "Telegraph", null, 0f, false, color, 1f, 4);
            marker.transform.localScale = new Vector3(halfWidth * 2f, 0.25f, 1f);
            telegraph = marker.gameObject;
            if (target != null && !target.IsDead) Follow();
        }

        private void Update()
        {
            age += Time.deltaTime;

            if (!struck && !artShown && target != null && !target.IsDead) Follow(); // 그림이 시작되면 자리를 고정 (그림과 판정이 같은 자리)

            if (art != null && !artShown && age >= delay - artLead) ShowArt();

            if (!struck && age >= delay)
            {
                struck = true;
                GameAudio.PlaySfx("Hit_Earth");
                if (telegraph != null) Destroy(telegraph);
                if (art == null)
                {
                    var frames = EffectLibrary.SpellFrames(Element.Earth);
                    var visual = EffectLibrary.CreateVisual(transform, "Visual", frames, 14f, false,
                        ElementInfo.Color(Element.Earth), halfWidth * 1.5f);
                    if (frames == null) visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                }
                var center = (Vector2)transform.position + new Vector2(0f, 0.8f);
                var hit = new System.Collections.Generic.HashSet<HealthSystem>();
                foreach (var col in Physics2D.OverlapBoxAll(center, new Vector2(halfWidth * 2f, 1.6f), 0f))
                {
                    var health = col.GetComponentInParent<HealthSystem>();
                    if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;
                    if (!hit.Add(health)) continue;
                    DamageCalculator.Strike(owner, health, DamageKind.Magic, Element.Earth, multiplier);
                    onHit?.Invoke(health);
                }
            }

            if (age >= delay + Lifetime) Destroy(gameObject);
        }

        // 그림은 피해 판정(Lifetime)보다 길어서 따로 두고 끝까지 재생한 뒤 사라진다 (바닥 = 아래 가운데 피벗).
        private void ShowArt()
        {
            artShown = true;
            var root = new GameObject("SpellArt_Earth");
            root.transform.position = transform.position;
            EffectLibrary.CreateVisual(root.transform, "Visual", art, 14f, false, ElementInfo.Color(Element.Earth), 1f, 5, destroyAtEnd: true);
            Destroy(root, art.Length / 14f + 0.1f);
        }

        private void Follow()
        {
            Vector2 feet = target.transform.position;
            var ground = Physics2D.Raycast(feet + Vector2.up * 0.5f, Vector2.down, 30f, groundMask);
            transform.position = ground.collider != null ? (Vector3)ground.point : (Vector3)feet;
        }
    }
}
