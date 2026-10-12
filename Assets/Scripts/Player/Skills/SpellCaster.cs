using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 B의 공격 스킬 (기획서 2.5).
    // 상단 숫자키 1~5로 스킬을 고르면 영창문이 뜨고, 똑같이 타이핑한 뒤 Enter를 누르면 발동한다.
    // 오타가 있는 채로 Enter를 누르면 마나만 날아간다. Esc는 마나 소모 없이 취소.
    // 개발자 모드에서는 상단 6~9·0으로 영창 없이 바로 발동한다.
    // 영창 입력창 자체는 UI(SkillHUD)가 그리고, 입력이 끝나면 SubmitChant/CancelChant를 부른다.
    [RequireComponent(typeof(PlayerControlState))]
    [RequireComponent(typeof(ManaSystem))]
    [RequireComponent(typeof(CombatStats))]
    public class SpellCaster : MonoBehaviour
    {
        private PlayerControlState control;
        private ManaSystem mana;
        private CombatStats stats;
        private HealthSystem health;
        private PlayerCombat combat;

        public bool ChantOpen { get; private set; }
        public SpellDefinition PendingSpell { get; private set; }
        public int ChantOpenedFrame { get; private set; }

        private void Awake()
        {
            control = GetComponent<PlayerControlState>();
            mana = GetComponent<ManaSystem>();
            stats = GetComponent<CombatStats>();
            health = GetComponent<HealthSystem>();
            combat = GetComponent<PlayerCombat>();
        }

        private void Update()
        {
            if (health.IsDead)
            {
                if (ChantOpen) CloseChant();
                return;
            }

            if (ChantOpen && control.Locked)
            {
                CancelChant();
                return;
            }

            if (!RoleInput.SpellDown(control, out var element)) return;

            var spell = SpellBook.Get(element);
            if (GameSession.DevMode)
            {
                if (!mana.TryConsume(spell.ManaCost, ManaUser.PlayerB))
                {
                    SkillMessages.Post(ManaUser.PlayerB, $"마나 부족: {spell.Name} (필요 {spell.ManaCost:0})", true);
                    return;
                }

                Cast(spell);
                return;
            }

            PendingSpell = spell;
            ChantOpen = true;
            ChantOpenedFrame = Time.frameCount;
            control.ChantActive = true;
        }

        // 영창 입력창에서 Enter를 눌렀을 때.
        public void SubmitChant(string typed)
        {
            if (!ChantOpen) return;

            var spell = PendingSpell;
            CloseChant();

            if (!mana.TryConsume(spell.ManaCost, ManaUser.PlayerB))
            {
                SkillMessages.Post(ManaUser.PlayerB, $"마나 부족: {spell.Name} (필요 {spell.ManaCost:0})", true);
                return;
            }

            if (typed != spell.Chant)
            {
                mana.MarkWasted(ManaUser.PlayerB, spell.ManaCost);
                SkillMessages.Post(ManaUser.PlayerB, $"영창 실패 (오타) — 마나 {spell.ManaCost:0} 날림", true);
                return;
            }

            Cast(spell);
        }

        public void CancelChant()
        {
            if (!ChantOpen) return;
            CloseChant();
            SkillMessages.Post(ManaUser.PlayerB, "영창 취소");
        }

        private void CloseChant()
        {
            ChantOpen = false;
            control.ChantActive = false;
        }

        private void Cast(SpellDefinition spell)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.2f);
            Vector2 aim = RoleInput.AimWorldPoint(origin + Vector2.right * Mathf.Sign(transform.localScale.x));
            if (combat != null) combat.PlayCastAnimation(aim);

            if (spell.Shape == SpellShape.GroundSpike)
            {
                SpawnGroundSpike(spell, aim);
            }
            else
            {
                var direction = aim - origin;
                if (direction.sqrMagnitude < 0.01f) direction = Vector2.right * Mathf.Sign(transform.localScale.x);
                SpawnProjectile(spell, origin, direction.normalized);
            }

            SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 발동");
        }

        private void SpawnProjectile(SpellDefinition spell, Vector2 origin, Vector2 direction)
        {
            var go = new GameObject($"Spell_{spell.Element}");
            go.transform.position = origin;
            EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.SpellFrames(spell.Element), 14f, true,
                ElementInfo.Color(spell.Element), spell.Radius * 2.5f);

            var projectile = go.AddComponent<Projectile>();
            projectile.Launch(stats, true, DamageKind.Magic, spell.Element, direction, spell.Speed,
                spell.Radius, spell.Range, spell.Shape == SpellShape.Piercing, spell.DamageMultiplier);
            projectile.SetHoming(spell.HomingTurnRate, SpellBook.HomingRange);
            projectile.Hit = target => StatusEffects.TryApply(target, spell.Debuff, stats);
        }

        private void SpawnGroundSpike(SpellDefinition spell, Vector2 aim)
        {
            // 조준점이 사거리 밖이면 사거리 끝으로 당긴다. 사거리 안에서 조준점에 가장 가까운 적을 노린다.
            Vector2 self = transform.position;
            if (Vector2.Distance(self, aim) > spell.Range) aim = self + (aim - self).normalized * spell.Range;
            var target = FindSpikeTarget(self, aim, spell.Range);

            int groundLayer = LayerMask.NameToLayer("Ground");
            LayerMask groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)1;
            var hit = Physics2D.Raycast(aim + Vector2.up * 0.5f, Vector2.down, 30f, groundMask);
            Vector2 point = hit.collider != null ? hit.point : new Vector2(aim.x, self.y - 0.6f);

            var go = new GameObject("Spell_Earth");
            go.transform.position = point;
            go.AddComponent<GroundSpike>().Begin(stats, spell.Radius, spell.DamageMultiplier, spell.CastDelay, target,
                groundMask, health => StatusEffects.TryApply(health, spell.Debuff, stats));
        }

        private static HealthSystem FindSpikeTarget(Vector2 self, Vector2 aim, float range)
        {
            HealthSystem best = null;
            float bestDistance = float.MaxValue;
            foreach (var col in Physics2D.OverlapCircleAll(self, range))
            {
                var health = col.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;

                float distance = ((Vector2)health.transform.position - aim).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = health;
                }
            }

            return best;
        }

        private void OnDisable()
        {
            if (ChantOpen) CloseChant();
        }
    }
}
