using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 A의 속성 방어막 (기획서 2.5).
    // 넘패드 1~5(불/물/바람/땅/무)를 누르면 마나를 쓰고 방향키 순서 미니게임이 뜬다. 그동안 캐릭터는 멈춘다.
    // 순서대로 W/A/S/D를 누르면 방어막이 켜지고, 틀리거나 시간이 다 되면 마나만 날아간다.
    // 개발자 모드에서는 상단 숫자키 1~5로 미니게임 없이 바로 켜진다.
    [RequireComponent(typeof(PlayerControlState))]
    [RequireComponent(typeof(ManaSystem))]
    [RequireComponent(typeof(ElementShield))]
    public class ElementShieldSkill : MonoBehaviour
    {
        // 속성마다 고정된 입력 순서. 불(WDAS)과 바람(WSDA)은 기획 확정 예시, 나머지는 가안.
        private static readonly string[] Sequences = { "WDAS", "WASD", "WSDA", "SWAD", "DAWS" };
        private static readonly float[] ManaCosts = { 25f, 25f, 25f, 25f, 30f };

        [SerializeField] private float inputTimeLimit = 2f;
        [SerializeField] private float shieldDuration = 3f;

        private PlayerControlState control;
        private ManaSystem mana;
        private ElementShield shield;
        private HealthSystem health;

        public bool MinigameActive { get; private set; }
        public Element PendingElement { get; private set; }
        public string Sequence => Sequences[(int)PendingElement];
        public int Progress { get; private set; }
        public float TimeLeft { get; private set; }
        public float TimeLimit => inputTimeLimit;

        public static string SequenceFor(Element element) => Sequences[(int)element];
        public static float CostFor(Element element) => ManaCosts[(int)element];

        private void Awake()
        {
            control = GetComponent<PlayerControlState>();
            mana = GetComponent<ManaSystem>();
            shield = GetComponent<ElementShield>();
            health = GetComponent<HealthSystem>();
        }

        private void Update()
        {
            if (health != null && health.IsDead)
            {
                if (MinigameActive) EndMinigame();
                return;
            }

            if (MinigameActive)
            {
                UpdateMinigame();
                return;
            }

            if (!RoleInput.ShieldDown(control, out var element)) return;

            float cost = CostFor(element);
            string label = $"{ElementInfo.KoreanName(element)} 방어막";
            if (!mana.TryConsume(cost, ManaUser.PlayerA))
            {
                SkillMessages.Post(ManaUser.PlayerA, $"마나 부족: {label} (필요 {cost:0})", true);
                return;
            }

            if (GameSession.DevMode)
            {
                Activate(element);
                return;
            }

            PendingElement = element;
            Progress = 0;
            TimeLeft = inputTimeLimit;
            MinigameActive = true;
            control.ShieldMinigameActive = true;
        }

        private void UpdateMinigame()
        {
            if (control.Locked)
            {
                Fail("방 이동으로 취소");
                return;
            }

            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                Fail("시간 초과");
                return;
            }

            if (!RoleInput.MinigameArrowDown(out char key)) return;

            if (key != Sequence[Progress])
            {
                Fail($"{Sequence[Progress]} 대신 {key}");
                return;
            }

            Progress++;
            if (Progress >= Sequence.Length)
            {
                EndMinigame();
                Activate(PendingElement);
            }
        }

        private void Activate(Element element)
        {
            shield.Activate(element, shieldDuration);
            SkillMessages.Post(ManaUser.PlayerA, $"{ElementInfo.KoreanName(element)} 방어막 발동");
        }

        private void Fail(string reason)
        {
            float cost = CostFor(PendingElement);
            EndMinigame();
            mana.MarkWasted(ManaUser.PlayerA, cost);
            SkillMessages.Post(ManaUser.PlayerA, $"방어막 실패 ({reason}) — 마나 {cost:0} 날림", true);
        }

        private void EndMinigame()
        {
            MinigameActive = false;
            control.ShieldMinigameActive = false;
        }

        private void OnDisable()
        {
            if (MinigameActive) EndMinigame();
        }
    }
}
