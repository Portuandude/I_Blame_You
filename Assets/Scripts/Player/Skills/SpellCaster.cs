using System.Collections.Generic;
using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 B의 공격 스킬 (기획서 2.5).
    // 상단 숫자키 1~4(또는 휠로 고르고 우클릭, 우클릭을 누르고 있으면 속성 → 마법 2단계 다이얼, RoleInput.Dial)로 스킬을 고르면
    // 화면 가운데에 마법진이 뜨고, 마우스 왼쪽을 누른 채 한 번에(한붓) 따라 그린다 (2026-11-16 사람 결정: 영창 타이핑 대신, Docs/MagicCircleDesign.md).
    // 마우스를 떼는 순간(또는 제한 시간이 다 되면) 일치율로 판정한다: 50% 이하면 마나만 날아가고, 그 위는 피해 × 일치율 ÷ 75% (MagicCircleRules).
    // 그리는 동안 게임은 그대로 흐르고 P1 키보드도 막히지 않는다 (마우스만 쓰므로). 우클릭·Esc는 마나 소모 없이 취소.
    // 개발자 모드에서는 상단 1~5·6~9·0으로 마법진 없이 바로 발동한다 (피해 ×1).
    // 마법진 그림은 UI(SkillHUD.MagicCircle)가 그리고, 판 자리(BoardCenter/BoardRadius)는 여기서 정한다.
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

        public bool CircleOpen { get; private set; }
        public SpellDefinition PendingSpell { get; private set; }
        public int CircleTier { get; private set; }                 // 0 견습 · 1 숙련 · 2 대마법 (순풍이면 한 단계 쉬움)
        public List<CirclePoint> CirclePath { get; private set; }   // 따라 그릴 선 (한 획, 판 좌표)
        public readonly List<CirclePoint> Drawn = new List<CirclePoint>(); // P2가 그린 선 (판 좌표)
        public bool Drawing { get; private set; }                   // 마우스 왼쪽을 누른 채 그리는 중
        private bool openedRemote;                                  // 마법진을 열 때 온라인 친구가 P2였는지
        public float CircleTolerance { get; private set; }
        public float CircleTimeLeft { get; private set; }
        public float CircleTimeLimit { get; private set; }
        public float CircleOpenedAt { get; private set; }           // 열린 시각 (unscaled, 그림 연출용)
        public float CircleEase { get; set; }                       // 수정 탑 "마법진 여유": 허용 폭 배율 보너스 (PlaytestBootstrap.ApplyTowerBonuses)
        // 마지막 판정 (HUD가 잠깐 크게 띄운다)
        public int LastPercent { get; private set; } = -1;
        public float LastJudgedAt { get; private set; } = -10f;
        public Element LastElement { get; private set; }
        public event System.Action<int> CircleJudged;               // 판정 일치율 (실패 포함). 쌍둥이 반지
        private Vector2? lockedAim;                                 // 커서 자리 마법: 마법을 고른 순간의 커서 자리 (그리는 동안 커서가 판 위에 있으므로)
        private float tailwindUntil = -1f;
        public bool TailwindActive => Time.time < tailwindUntil;
        public float TailwindRemaining => Mathf.Max(0f, tailwindUntil - Time.time);
        public bool WindingUp { get; private set; } // 바위 가시 선딜 중
        // 운석처럼 선딜이 있는 커서 자리 마법: 영창을 마친 순간의 자리를 묶어 둔다 (선딜 중 커서를 옮겨도 그 자리에 떨어짐, 미리보기도 그 자리)
        public SpellDefinition WindupSpell { get; private set; }
        public Vector2 WindupLanding { get; private set; }
        public event System.Action<Element> Casted;  // 공격 마법이 실제로 발동한 순간 (마법진 성공·개발자 모드 즉시 발동). 응원 리본 유물
        // 유물 메아리 수정: 같은 속성 마법을 연달아 쓰면 두 번째는 마나 절반 (RelicBook.EchoDiscounted). echoElement = 바로 전에 발동한 속성, 없으면 -1
        public bool EchoCrystal { get; set; }
        private int echoElement = -1;

        // 이번 마법의 마나 (TotalCostMultiplier 곱하기 전). 메아리 수정이 있고 바로 전 마법과 속성이 같으면 절반.
        private bool EchoApplies(SpellDefinition spell) => EchoCrystal && RelicBook.EchoDiscounted(echoElement, (int)spell.Element);
        private float CostOf(SpellDefinition spell) => spell.ManaCost * (EchoApplies(spell) ? RelicBook.EchoCrystalCostRate : 1f);

        private void AfterCast(SpellDefinition spell) => echoElement = RelicBook.EchoAfterCast(echoElement, (int)spell.Element);
        private const float SpikeDefaultDistance = 4f;  // 앞에 적이 없을 때 바위 가시가 솟는 거리
        private SpriteRenderer charge;               // 선딜 중 캐릭터 위에 보이는 기 모으기 표시

        private void Awake()
        {
            control = GetComponent<PlayerControlState>();
            mana = GetComponent<ManaSystem>();
            stats = GetComponent<CombatStats>();
            health = GetComponent<HealthSystem>();
            combat = GetComponent<PlayerCombat>();
            RoleInput.CloseSpellDial(); // 다이얼 상태는 static이라 새 판·씬 다시 불러오기에 남지 않게 (리뷰 LOW)
        }

        // 창이 포커스를 잃으면(Alt-Tab) Unity가 마우스 버튼을 뗀 것으로 보아 다이얼이 발동하고 그리던 마법진이 판정되던 것 → 둘 다 마나 없이 취소.
        // 온라인 친구가 P2면 호스트 창 포커스와 상관없으므로 그대로 둔다.
        private void OnApplicationFocus(bool focused)
        {
            if (focused || IBlameYou.Net.P2Input.Remote) return;
            RoleInput.CloseSpellDial();
            if (CircleOpen && !openedRemote) CancelCircle();
        }

        private void Update()
        {
            if (health.IsDead)
            {
                RoleInput.CloseSpellDial();
                if (CircleOpen) CloseCircle();
                return;
            }

            if (CircleOpen)
            {
                UpdateCircle();
                return;
            }

            if (WindingUp)
            {
                RoleInput.CloseSpellDial();
                return;
            }
            if (!RoleInput.SpellDown(control, out var element, out int slot)) return;

            var spell = SpellBook.Get(element, slot);
            if (spell == null)
            {
                SkillMessages.Post(ManaUser.PlayerB, "무속성 마력탄은 평타(좌클릭)로 바뀌었어요", true);
                return;
            }
            if (GameSession.DevMode)
            {
                float devCost = CostOf(spell);
                if (!mana.TryConsume(devCost, ManaUser.PlayerB))
                {
                    SkillMessages.Post(ManaUser.PlayerB, $"마나 부족: {spell.Name} (필요 {devCost * mana.TotalCostMultiplier:0})", true);
                    return;
                }

                AfterCast(spell);
                Cast(spell, 1f, -1);
                return;
            }

            OpenCircle(spell);
        }

        // ---- 마법진 ----

        // 판 자리 (GUI 좌표, 위가 0): 화면 가운데 조금 위. 판 반지름 = 마법진 좌표 1.
        public static Vector2 BoardCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.46f);
        public static float BoardRadius => Mathf.Min(Screen.height, Screen.width) * 0.3f;

        private static CirclePoint ToBoard(Vector2 gui)
        {
            var c = BoardCenter;
            float r = BoardRadius;
            if (r <= 0f) return new CirclePoint(0f, 0f); // 창 최소화 등 화면 크기 0: 0으로 나누지 않게 (버그 점검 2026-11-22)
            return new CirclePoint((gui.x - c.x) / r, (gui.y - c.y) / r);
        }

        private void OpenCircle(SpellDefinition spell)
        {
            // 마나가 모자라면 열지 않는다: 6초 동안 다 그린 뒤에야 "마나 부족"을 알게 되지 않게 (버그 점검 2026-11-22)
            // 피의 계약처럼 모자란 마나를 대신 내는 유물이 있으면 판정 때 TryConsume에 맡긴다
            float cost = CostOf(spell);
            if (mana.ShortfallCover == null && !mana.HasEnough(cost * mana.TotalCostMultiplier))
            {
                SkillMessages.Post(ManaUser.PlayerB, $"마나 부족: {spell.Name} (필요 {cost * mana.TotalCostMultiplier:0})", true);
                return;
            }
            PendingSpell = spell;
            openedRemote = IBlameYou.Net.P2Input.Remote;
            CircleTier = SpellBook.CircleTier(spell, TailwindActive);
            CirclePath = MagicCircleRules.Path((int)spell.Element, CircleTier);
            CircleTolerance = MagicCircleRules.ToleranceOf(CircleTier, CircleEase);
            CircleTimeLimit = CircleTimeLeft = MagicCircleRules.TimeLimitOf(CircleTier);
            CircleOpenedAt = Time.unscaledTime;
            Drawn.Clear();
            Drawing = false;
            lockedAim = null;
            lockedAim = spell.AtCursor ? CursorPoint(spell.Range) : (Vector2?)null; // 그리는 동안 커서는 판 위에 있으므로 고른 순간의 자리로
            CircleOpen = true;
            control.ChantActive = true;
        }

        private void UpdateCircle()
        {
            if (control.Locked)
            {
                CancelCircle("방 이동으로 마법진 취소");
                return;
            }
            if (openedRemote && !IBlameYou.Net.P2Input.Remote)
            {
                // 온라인 친구가 그리는 중에 끊김: 호스트 마우스로 바뀌어 그리다 만 그림이 판정되지 않게 취소 (마나 그대로)
                CancelCircle("친구 연결이 끊겨 마법진 취소");
                return;
            }
            if (RoleInput.CircleCancelDown())
            {
                RoleInput.MarkCircleCancel(); // 같은 프레임의 Esc가 일시정지 창까지 열지 않게
                CancelCircle("마법진 취소");
                return;
            }

            CircleTimeLeft -= Time.deltaTime;
            var point = ToBoard(RoleInput.CircleMouseGui());
            if (!Drawing)
            {
                if (RoleInput.CircleDrawDown())
                {
                    Drawing = true;
                    Drawn.Clear();
                    Drawn.Add(point);
                }
            }
            else
            {
                var last = Drawn[Drawn.Count - 1];
                float dx = point.X - last.X, dy = point.Y - last.Y;
                if (dx * dx + dy * dy > 0.00004f) Drawn.Add(point);
                if (!RoleInput.CircleDrawHeld()) // 한붓: 떼는 순간 판정
                {
                    if (Drawn.Count < 2)
                    {
                        // 톡 클릭만 한 것은 그린 것으로 치지 않는다: 마나를 날리지 않고 다시 그리기 (버그 점검 2026-11-22)
                        Drawing = false;
                        Drawn.Clear();
                        return;
                    }
                    JudgeCircle();
                    return;
                }
            }

            if (CircleTimeLeft <= 0f)
            {
                if (Drawn.Count > 1) JudgeCircle();
                else
                {
                    var spell = PendingSpell;
                    CloseCircle();
                    lockedAim = null;
                    SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name}: 시간 초과 (마나는 그대로)", true);
                }
            }
        }

        private void JudgeCircle()
        {
            try { JudgeAndCast(); }
            finally { lockedAim = null; } // 커서 자리는 이번 발동(운석 선딜 자리 포함)에만
        }

        private void JudgeAndCast()
        {
            var spell = PendingSpell;
            var result = MagicCircleRules.Score(CirclePath, Drawn, CircleTolerance);
            CloseCircle();
            LastPercent = result.Percent;
            LastJudgedAt = Time.unscaledTime;
            LastElement = spell.Element;

            float cost = CostOf(spell);
            if (!mana.TryConsume(cost, ManaUser.PlayerB))
            {
                SkillMessages.Post(ManaUser.PlayerB, $"마나 부족: {spell.Name} (필요 {cost * mana.TotalCostMultiplier:0})", true);
                return;
            }
            // 판정 알림(쌍둥이 반지 마나 보상 등)은 마나를 낸 뒤에: 보상 마나로 모자란 마나를 메워 시전하지 않게 (버그 점검 2026-11-22)
            CircleJudged?.Invoke(result.Percent);

            if (result.Failed)
            {
                // 피의 계약으로 체력을 냈으면 마나는 그대로다: 날린 마나 기록·집중 구슬 환급 없음 (버그 점검 2026-11-24)
                if (mana.LastPaidByCover)
                    SkillMessages.Post(ManaUser.PlayerB, $"마법진 실패 (일치율 {result.Percent}%) — 피의 계약 체력만 날림", true);
                else
                {
                    mana.MarkWasted(ManaUser.PlayerB, cost);
                    SkillMessages.Post(ManaUser.PlayerB, $"마법진 실패 (일치율 {result.Percent}%) — 마나 {cost * mana.TotalCostMultiplier:0} 날림", true);
                }
                echoElement = -1; // 메아리 수정: 실패하면 사슬이 끊긴다
                return;
            }

            AfterCast(spell);
            Cast(spell, result.Power, result.Percent);
        }

        public void CancelCircle(string reason = "마법진 취소")
        {
            if (!CircleOpen) return;
            CloseCircle();
            lockedAim = null;
            SkillMessages.Post(ManaUser.PlayerB, reason);
        }

        private void CloseCircle()
        {
            CircleOpen = false;
            Drawing = false;
            control.ChantActive = false;
        }

        // power: 피해 배율 (마법진 일치율 ÷ 75%, 개발자 모드 1). percent: 일치율 (개발자 모드 -1)
        private void Cast(SpellDefinition spell, float power, int percent)
        {
            string tag = percent >= 0 ? $" · {percent}% ×{power:0.00}" : "";
            // 공격 스킬은 마우스가 아니라 캐릭터가 바라보는 방향으로 나간다 (커서 자리 마법만 커서 자리).
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.2f);
            float facing = Mathf.Sign(transform.localScale.x);
            if (combat != null) combat.PlayCastAnimation(origin + Vector2.right * facing); // 방향은 그대로 두고 동작만
            GameAudio.PlaySfx($"Cast_{spell.Element}");
            RunStats.AddSpell((int)spell.Element); // 결과 창 B 최애 마법
            Casted?.Invoke(spell.Element);

            if (spell.Shape == SpellShape.Tailwind)
            {
                tailwindUntil = Time.time + spell.Duration;
                EffectLibrary.PlayCast(Element.Wind, transform.position + new Vector3(0f, 0.6f, 0f), facing < 0f, 1.2f);
                SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} — {spell.Duration:0}초 동안 마법진이 한 단계 쉬워진다");
                return;
            }

            if (spell.Shape == SpellShape.Mist)
            {
                var mist = new GameObject("Spell_Mist");
                mist.transform.position = CursorLanding(spell);
                mist.AddComponent<MistZone>().Begin(spell.Radius, spell.Duration);
                SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 발동{tag}");
                return;
            }

            if (spell.Shape == SpellShape.FireWall)
            {
                SpawnFireWall(spell, facing, power);
                SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 발동{tag}");
                return;
            }

            if (spell.Shape == SpellShape.GroundSpike)
            {
                // 선딜: 시전 동작 뒤 Windup초가 지나야 가시가 예고된다. 그동안 다른 공격 스킬은 못 쓴다.
                if (spell.AtCursor)
                {
                    WindupSpell = spell;
                    WindupLanding = CursorLanding(spell);
                }
                else WindupSpell = null;
                StartCoroutine(GroundSpikeAfterWindup(spell, facing, power, tag));
                SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 시전 중...");
                return;
            }

            // 첫 발사각은 바라보는 방향으로 수평. 이후 방향은 유도가 꺾는다.
            SpawnProjectile(spell, origin, new Vector2(facing, 0f), power);
            if (spell.Volley > 1) StartCoroutine(VolleyRest(spell, facing, power)); // 칼바람: 나머지 발을 0.12초 간격으로

            SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 발동{tag}");
        }

        // B의 평타: 바라보는 방향으로 무속성 마력탄을 쏜다. 마나가 모자라면 false.
        // 평타 마나 소모 감소량 (레벨업 특성).
        public float BoltCostReduction { get; set; }

        public bool FireBasicBolt()
        {
            var bolt = SpellBook.BasicBolt;
            if (health.IsDead || WindingUp || CircleOpen) return false;
            float cost = Mathf.Max(0f, bolt.ManaCost - BoltCostReduction);
            if (!mana.TryConsume(cost, ManaUser.PlayerB))
            {
                SkillMessages.Post(ManaUser.PlayerB, $"마나 부족: 평타 마력탄 (필요 {cost:0})", true);
                return false;
            }

            Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.2f);
            float facing = Mathf.Sign(transform.localScale.x);
            SpawnProjectile(bolt, origin, new Vector2(facing, 0f));
            GameAudio.PlaySfx("Cast_Neutral", 0.8f);
            return true;
        }

        private System.Collections.IEnumerator GroundSpikeAfterWindup(SpellDefinition spell, float facing, float power, string tag)
        {
            WindingUp = true;
            charge = EffectLibrary.CreateVisual(transform, "EarthCharge", EffectLibrary.CastFrames(Element.Earth), 10f, true,
                new Color(0.75f, 0.55f, 0.3f, 0.5f), 0.9f, 6);
            charge.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            charge.color = new Color(1f, 1f, 1f, 0.75f); // 머리 위를 덮지 않게 반투명

            yield return new WaitForSeconds(spell.Windup);

            if (charge != null) Destroy(charge.gameObject);
            charge = null;
            WindingUp = false;
            bool locked = WindupSpell == spell;
            Vector2 landing = WindupLanding;
            WindupSpell = null;
            if (health.IsDead) yield break;

            SpawnGroundSpike(spell, facing, power, locked ? landing : (Vector2?)null);
            SkillMessages.Post(ManaUser.PlayerB, $"{spell.Name} 발동{tag}");
        }

        private void SpawnProjectile(SpellDefinition spell, Vector2 origin, Vector2 direction, float power = 1f)
        {
            EffectLibrary.PlayCast(spell.Element, origin + direction.normalized * 0.35f, direction.x < 0f);

            var go = new GameObject($"Spell_{spell.Element}");
            go.transform.position = origin;
            EffectLibrary.CreateVisual(go.transform, "Visual", EffectLibrary.FlightFrames(spell.Element), 16f, true,
                ElementInfo.Color(spell.Element), spell.Radius * 2.5f);
            go.AddComponent<ProjectileTrail>().Begin(spell.Element);

            var projectile = go.AddComponent<Projectile>();
            projectile.Launch(stats, true, DamageKind.Magic, spell.Element, direction, spell.Speed,
                spell.Radius, spell.Range, spell.Shape == SpellShape.Piercing, spell.DamageMultiplier * power);
            projectile.SetHoming(spell.HomingTurnRate, SpellBook.HomingRange);
            projectile.Hit = target =>
            {
                EffectLibrary.PlayHit(spell.Element, HitPoint(target));
                ScreenFlash.Pulse();
                GameAudio.PlaySfx($"Hit_{spell.Element}");
                // 얼음 족쇄(반드시 거는 젖음): 정예·중간보스·보스는 둔화 20%, 일반 몹 40% (ShackleRules)
                bool shackle = spell.DebuffAlways && spell.Debuff == StatusDebuff.Wet;
                float wetSlow = shackle ? ShackleRules.Slow(IsStrong(target)) : StatusEffects.WetSlow;
                StatusEffects.TryApply(target, spell.Debuff, stats, spell.DebuffAlways, wetSlow);
            };
            projectile.Impact = point =>
            {
                EffectLibrary.PlayHit(spell.Element, point, 0.7f);
                GameAudio.PlaySfx($"Hit_{spell.Element}", 0.5f);
            };
        }

        private void SpawnGroundSpike(SpellDefinition spell, float facing, float power, Vector2? lockedLanding = null)
        {
            // 바라보는 방향 사거리 안에서 가장 가까운 적을 노린다. 없으면 앞쪽 SpikeDefaultDistance 지점에서 솟는다.
            Vector2 self = transform.position;
            var target = spell.AtCursor ? null : FindSpikeTarget(self, facing, spell.Range); // 운석은 커서 자리 (따라가지 않음)
            Vector2 aim = spell.AtCursor ? CursorPoint(spell.Range) : target != null ? (Vector2)target.transform.position : self + new Vector2(facing * SpikeDefaultDistance, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            LayerMask groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)1;
            if (!spell.AtCursor && target == null) aim = StopAtWall(self, aim, facing, groundMask, 0.3f); // 벽에 붙어 쓰면 벽 속·벽 너머에 솟던 것
            Vector2 point;
            if (spell.AtCursor) point = lockedLanding ?? CursorLanding(spell);
            else
            {
                var hit = Physics2D.Raycast(aim + Vector2.up * 0.5f, Vector2.down, 30f, groundMask);
                point = hit.collider != null ? hit.point : new Vector2(aim.x, self.y - 0.6f);
            }

            IBlameYou.Puzzles.ElementGate.NotifyNearby(self, facing, spell.Range, Element.Earth); // 봉인문 앞이면 문이 튕겨내며 번쩍임

            var go = new GameObject("Spell_Earth");
            go.transform.position = point;
            go.AddComponent<GroundSpike>().Begin(stats, spell.Radius, spell.DamageMultiplier * power, spell.CastDelay, target,
                groundMask, health =>
                {
                    EffectLibrary.PlayHit(Element.Earth, HitPoint(health));
                    ScreenFlash.Pulse();
                    StatusEffects.TryApply(health, spell.Debuff, stats);
                });
        }

        private System.Collections.IEnumerator VolleyRest(SpellDefinition spell, float facing, float power)
        {
            for (int i = 1; i < spell.Volley; i++)
            {
                yield return new WaitForSeconds(0.12f);
                if (health.IsDead) yield break;
                Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.2f + (i % 2 == 1 ? 0.25f : -0.2f));
                SpawnProjectile(spell, origin, new Vector2(facing, 0f), power);
            }
        }

        // 커서 자리 (사거리 안으로 줄임). 불꽃 비·물안개·운석. 마법진을 그리는 동안·그 직후 발동은 고른 순간의 자리(lockedAim).
        private Vector2 CursorPoint(float range)
        {
            Vector2 self = transform.position;
            Vector2 aim = lockedAim ?? RoleInput.AimWorldPoint(self);
            Vector2 offset = aim - self;
            if (offset.magnitude > range) aim = self + offset.normalized * range;
            return aim;
        }

        // 커서 자리 마법이 실제로 놓일 자리. 발동과 마법진 그리는 중 미리보기(CursorSpellPreview)가 같은 계산을 쓴다.
        // 물안개는 커서 자리 그대로, 불꽃 비는 아래 12칸 안 땅 (없으면 커서 1칸 아래), 운석은 아래 30칸 안 땅 (없으면 캐릭터 발 높이).
        public Vector2 CursorLanding(SpellDefinition spell)
        {
            Vector2 aim = CursorPoint(spell.Range);
            if (spell.Shape == SpellShape.Mist) return aim;
            int groundLayer = LayerMask.NameToLayer("Ground");
            LayerMask groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)1;
            bool rain = spell.Shape == SpellShape.FireWall;
            var hit = Physics2D.Raycast(aim + Vector2.up * 0.5f, Vector2.down, rain ? 12f : 30f, groundMask);
            if (hit.collider != null) return hit.point;
            return rain ? new Vector2(aim.x, aim.y - 1f) : new Vector2(aim.x, transform.position.y - 0.6f);
        }

        // 불꽃 장막: 바라보는 쪽 바로 앞 땅 위에 불벽 (가장 가까운 끝이 캐릭터 0.5칸 앞).
        private void SpawnFireWall(SpellDefinition spell, float facing, float power)
        {
            Vector2 self = transform.position;
            Vector2 aim = spell.AtCursor ? CursorPoint(spell.Range) : self + new Vector2(facing * (spell.Radius + 0.5f), 0f);
            int groundLayer = LayerMask.NameToLayer("Ground");
            LayerMask groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)1;
            // 벽 앞이면 먼 끝이 벽에 닿게 당긴다 (벽 속에서 시작한 아래 광선이 벽 안·벽 너머 땅에 불벽을 놓아 반대편 적까지 태우던 것)
            if (!spell.AtCursor) aim = StopAtWall(self, aim, facing, groundMask, spell.Radius);
            Vector2 point;
            if (spell.AtCursor) point = CursorLanding(spell);
            else
            {
                var hit = Physics2D.Raycast(aim + Vector2.up * 0.5f, Vector2.down, 6f, groundMask);
                point = hit.collider != null ? hit.point : new Vector2(aim.x, self.y - 0.6f);
            }

            IBlameYou.Puzzles.ElementGate.NotifyNearby(self, facing, spell.Range, Element.Fire);
            VineFloor.BurnNear(self, facing, spell.Range); // 숲 바닥 덩굴을 태운다 (스테이지 2)

            var go = new GameObject("Spell_FireWall");
            go.transform.position = point;
            go.AddComponent<FireWall>().Begin(stats, spell.Radius, spell.DamageMultiplier * power, spell.Duration, (health, first) =>
            {
                EffectLibrary.PlayHit(Element.Fire, HitPoint(health), 0.6f);
                if (first) StatusEffects.TryApply(health, spell.Debuff, stats, true);
            }, spell.AtCursor, spell.Height > 0f ? spell.Height : FireWall.WallHeight);
        }

        // 정예·중간보스·보스인지 (골드 표와 같은 등급, RunGold.RankOf)
        private static bool IsStrong(HealthSystem target)
        {
            var enemy = target != null ? target.GetComponent<IBlameYou.Enemies.EnemyController>() : null;
            return enemy != null && RunGold.RankOf(enemy) != EnemyRank.Normal;
        }

        // 적중 이펙트를 띄울 자리: 맞은 대상 몸통의 가운데.
        private static Vector3 HitPoint(HealthSystem target)
        {
            var body = target.GetComponent<Collider2D>();
            return body != null ? body.bounds.center : target.transform.position + new Vector3(0f, 0.5f, 0f);
        }

        // 몸 높이에서 aim + back까지 가로로 벽이 있으면 벽 앞 back칸 자리로 (가로 거리만 줄인다)
        private static Vector2 StopAtWall(Vector2 self, Vector2 aim, float facing, LayerMask groundMask, float back)
        {
            Vector2 from = self + Vector2.up * 0.5f;
            if (!FirstWall(from, new Vector2(aim.x + facing * back, from.y), groundMask, out var wallPoint)) return aim; // 먼 끝(aim + back)까지 본다
            float x = wallPoint.x - facing * back;
            if ((x - self.x) * facing < 0f) x = self.x; // 벽에 딱 붙어 있으면 캐릭터 자리
            return new Vector2(x, aim.y);
        }

        // from→to 사이 첫 벽. 한쪽 발판(PlatformEffector)은 벽으로 치지 않는다 (발판 위·아래 적도 예전처럼 노림)
        private static bool FirstWall(Vector2 from, Vector2 to, LayerMask groundMask, out Vector2 point)
        {
            foreach (var hit in Physics2D.LinecastAll(from, to, groundMask))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.usedByEffector) continue;
                point = hit.point;
                return true;
            }
            point = to;
            return false;
        }

        private static HealthSystem FindSpikeTarget(Vector2 self, float facing, float range)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            LayerMask groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)1;
            Vector2 eye = self + Vector2.up * 0.5f;
            HealthSystem best = null;
            float bestDistance = float.MaxValue;
            foreach (var col in Physics2D.OverlapCircleAll(self, range))
            {
                var health = col.GetComponentInParent<HealthSystem>();
                if (health == null || health.IsDead || health.GetComponent<IBlameYou.Enemies.EnemyController>() == null) continue;

                Vector2 offset = (Vector2)health.transform.position - self;
                if (offset.x * facing < 0f) continue; // 등 뒤의 적은 노리지 않는다
                if (FirstWall(eye, col.bounds.center, groundMask, out _)) continue; // 벽 너머 적은 노리지 않는다

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
            WindupSpell = null;
            if (charge != null) Destroy(charge.gameObject);
            if (CircleOpen) CloseCircle();
        }
    }
}
