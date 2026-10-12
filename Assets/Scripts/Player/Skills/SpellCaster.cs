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
            float size = spell.Shape == SpellShape.Wave ? spell.Radius * 2f : spell.Radius * 2.5f;
            EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.SpellFrames(spell.Element), 14f, true,
                ElementInfo.Color(spell.Element), size);

            go.AddComponent<Projectile>().Launch(stats, true, DamageKind.Magic, spell.Element, direction, spell.Speed,
                spell.Radius, spell.Range, spell.Shape != SpellShape.Projectile, spell.DamageMultiplier);
        }

        private void SpawnGroundSpike(SpellDefinition spell, Vector2 aim)
        {
            // 조준점이 사거리 밖이면 사거리 끝으로 당기고, 그 아래 땅을 찾는다.
            Vector2 self = transform.position;
            if (Vector2.Distance(self, aim) > spell.Range) aim = self + (aim - self).normalized * spell.Range;

            int groundLayer = LayerMask.NameToLayer("Ground");
            LayerMask groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)1;
            var hit = Physics2D.Raycast(aim + Vector2.up * 0.5f, Vector2.down, 30f, groundMask);
            Vector2 point = hit.collider != null ? hit.point : new Vector2(aim.x, self.y - 0.6f);

            var go = new GameObject("Spell_Earth");
            go.transform.position = point;
            var frames = EffectLibrary.SpellFrames(Element.Earth);
            var visual = EffectLibrary.CreateVisual(go.transform, "Visual", frames, 14f, false,
                ElementInfo.Color(Element.Earth), spell.Radius * 1.5f);
            if (frames == null) visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            go.AddComponent<GroundSpike>().Begin(stats, spell.Radius, spell.DamageMultiplier);
        }

        private void OnDisable()
        {
            if (ChantOpen) CloseChant();
        }
    }
}
