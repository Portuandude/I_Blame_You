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
        public bool WindingUp { get; private set; } // 바위 가시 선딜 중
        private const float SpikeDefaultDistance = 4f;  // 앞에 적이 없을 때 바위 가시가 솟는 거리
        private SpriteRenderer charge;               // 선딜 중 캐릭터 위에 보이는 기 모으기 표시

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

            if (WindingUp) return;
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
            // 공격 스킬은 마우스가 아니라 캐릭터가 바라보는 방향으로 나간다 (B는 영창 중 손이 키보드에 있음).
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.2f);
            float facing = Mathf.Sign(transform.localScale.x);
            if (combat != null) combat.PlayCastAnimation(origin + Vector2.right * facing); // 방향은 그대로 두고 동작만

            if (spell.Shape == SpellShape.GroundSpike)
            {
                // 선딜: 시전 동작 뒤 Windup초가 지나야 가시가 예고된다. 그동안 다른 공격 스킬은 못 쓴다.
                StartCoroutine(GroundSpikeAfterWindup(spell, facing));
                SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 시전 중...");
                return;
            }

            // 첫 발사각은 바라보는 방향으로 수평. 이후 방향은 유도가 꺾는다.
            SpawnProjectile(spell, origin, new Vector2(facing, 0f));

            SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 발동");
        }

        private System.Collections.IEnumerator GroundSpikeAfterWindup(SpellDefinition spell, float facing)
        {
            WindingUp = true;
            charge = EffectLibrary.CreateVisual(transform, "EarthCharge", null, 0f, false,
                new Color(0.75f, 0.55f, 0.3f, 0.5f), 0.9f, 6);
            charge.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            yield return new WaitForSeconds(spell.Windup);

            if (charge != null) Destroy(charge.gameObject);
            charge = null;
            WindingUp = false;
            if (health.IsDead) yield break;

            SpawnGroundSpike(spell, facing);
            SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 발동");
        }

        private void SpawnProjectile(SpellDefinition spell, Vector2 origin, Vector2 direction)
        {
            var go = new GameObject($"Spell_{spell.Element}");
            go.transform.position = origin;
            EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.FlightFrames(spell.Element), 16f, true,
                ElementInfo.Color(spell.Element), spell.Radius * 2.5f);
            go.AddComponent<ProjectileTrail>().Begin(spell.Element);

            var projectile = go.AddComponent<Projectile>();
            projectile.Launch(stats, true, DamageKind.Magic, spell.Element, direction, spell.Speed,
                spell.Radius, spell.Range, spell.Shape == SpellShape.Piercing, spell.DamageMultiplier);
            projectile.SetHoming(spell.HomingTurnRate, SpellBook.HomingRange);
            projectile.Hit = target =>
            {
                EffectLibrary.PlayHit(spell.Element, HitPoint(target));
                StatusEffects.TryApply(target, spell.Debuff, stats);
            };
            projectile.Impact = point => EffectLibrary.PlayHit(spell.Element, point, 0.7f);
        }

        private void SpawnGroundSpike(SpellDefinition spell, float facing)
        {
            // 바라보는 방향 사거리 안에서 가장 가까운 적을 노린다. 없으면 앞쪽 SpikeDefaultDistance 지점에서 솟는다.
            Vector2 self = transform.position;
            var target = FindSpikeTarget(self, facing, spell.Range);
            Vector2 aim = target != null ? (Vector2)target.transform.position : self + new Vector2(facing * SpikeDefaultDistance, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            LayerMask groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)1;
            var hit = Physics2D.Raycast(aim + Vector2.up * 0.5f, Vector2.down, 30f, groundMask);
            Vector2 point = hit.collider != null ? hit.point : new Vector2(aim.x, self.y - 0.6f);

            var go = new GameObject("Spell_Earth");
            go.transform.position = point;
            go.AddComponent<GroundSpike>().Begin(stats, spell.Radius, spell.DamageMultiplier, spell.CastDelay, target,
                groundMask, health =>
                {
                    EffectLibrary.PlayHit(Element.Earth, HitPoint(health));
                    StatusEffects.TryApply(health, spell.Debuff, stats);
                });
        }

        // 적중 이펙트를 띄울 자리: 맞은 대상 몸통의 가운데.
        private static Vector3 HitPoint(HealthSystem target)
        {
            var body = target.GetComponent<Collider2D>();
            return body != null ? body.bounds.center : target.transform.position + new Vector3(0f, 0.5f, 0f);
        }

        private static HealthSystem FindSpikeTarget(Vector2 self, float facing, float range)
        {
            HealthSystem best = null;
            float bestDistance = float.MaxValue;
            foreach (var col in Physics2D.OverlapCircleAll(self, range))
            {
                var health = col.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;

                Vector2 offset = (Vector2)health.transform.position - self;
                if (offset.x * facing < 0f) continue; // 등 뒤의 적은 노리지 않는다

                float distance = offset.sqrMagnitude;
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
            WindingUp = false;
            if (charge != null) Destroy(charge.gameObject);
            if (ChantOpen) CloseChant();
        }
    }
}
