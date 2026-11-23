using IBlameYou.Core;
using IBlameYou.Net;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 A/B 역할별 입력. 한 컴퓨터로 할 때는 로컬 키보드/마우스에서 읽는다.
    // 온라인(호스트 방식, Docs/OnlinePlay.md)에서는 호스트 컴퓨터가 A를, 친구 컴퓨터가 B를 맡는다:
    // B 입력은 모두 P2Input을 거쳐서 친구가 보낸 마우스·키를 읽는다 (호스트 사람의 마우스는 무시).
    //
    // 플레이어 A (이동)                       플레이어 B (공격)
    //   A/D 이동, Space 점프                     마우스 조준, 좌클릭 평타, 휠 속성 고르기 → 우클릭 마법 (누르고 있으면 다이얼)
    //   Shift 달리기, Left Ctrl 대쉬             상단 1~4: 공격 스킬 선택 → 마법진을 좌클릭 꾹 한붓으로 따라 그리기 (우클릭·Esc 취소)
    //   W/A/S/D + Enter: 순간이동 (2026-11-12 방향키에서 바꿈)
    //   ↑ 키는 모두 기본값. P1 키는 시작 메뉴·일시정지의 "조작 키" 창에서 바꿀 수 있다 (KeyBindings, 2026-11-23)
    //   W(또는 ↑): 마을 건물 앞에서 들어가기
    //
    // 개발자 모드: 상단 1~5·6~9·0 = B 공격 스킬 즉시 (미니게임 생략). 속성 방어막은 2026-11-16 사람 결정으로 빠졌다.
    // 기능 테스트의 A/B 전환 (2026-11-14 사람 결정 ④ 두 사람 플레이 첫 단계): 한 사람이 한 역할만 조작해 보며 각자 무엇을 하는지 시험한다.
    public enum ControlFocus { Both, A, B }

    public static class RoleInput
    {
        // Both = 평소 (한 키보드로 둘 다). A = A 입력만 받고 B(마우스 공격·공격 스킬)는 막는다. B = 그 반대.
        // 기능 테스트에서 ` 키로 바꾼다 (FeatureTestTools). 시험 맵을 나가면 Both로 돌아간다.
        public static ControlFocus Focus { get; set; } = ControlFocus.Both;

        private static bool ABlocked(PlayerControlState state) => state.AKeyboardBlocked || Focus == ControlFocus.B;
        private static bool BBlocked => Focus == ControlFocus.A;

        private static readonly KeyCode[] TopRowFirstFive = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
        private static readonly KeyCode[] TopRowLastFive = { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };

        // ---- 스토리 소개 (Core/StoryIntro) ----
        // P1(A)·P2(B) 각자의 "다음/건너뛰기" 버튼. 둘이 동시에 꾹 누르면 건너뛴다.
        // 지금은 한 키보드를 같이 써서 둘이 같은 Space를 따로 누를 수 없으므로 P2는 마우스 왼쪽(평타 버튼)이다.
        // 입력 장치가 나뉘면(패드·온라인) 여기만 P2의 Space로 바꾸면 된다.
        // F12(임시, 2026-11-14 사람 지시): 혼자 시험할 때 두 사람이 동시에 누른 것으로 친다 — 꾹 누르면 건너뛰기 게이지가 찬다.
        public static bool StorySkipBoth => Input.GetKey(KeyCode.F12);
        public static bool StoryButtonA => Input.GetKey(KeyCode.Space) || KeyBindings.Get(P1Action.Jump) || StorySkipBoth; // 점프 키를 바꿨으면 그 키도
        public static bool StoryButtonB => P2Input.GetMouseButton(0) || (P2Input.Remote && P2Input.GetKey(KeyCode.Space)) || StorySkipBoth; // 온라인이면 친구의 Space도
        // 이번 프레임에 누른 순간 (짧게 톡 눌렀다 뗀 것을 놓치지 않게 — 온라인은 친구가 보낸 누름 횟수로 센다)
        public static bool StoryButtonADown => Input.GetKeyDown(KeyCode.Space) || KeyBindings.GetDown(P1Action.Jump);
        public static bool StoryButtonBDown => P2Input.GetMouseButtonDown(0) || (P2Input.Remote && P2Input.GetKeyDown(KeyCode.Space));

        // ---- 플레이어 A ----

        public static float MoveAxis(PlayerControlState state)
        {
            if (ABlocked(state) || state.MovementFrozen) return 0f;
            float axis = 0f;
            if (KeyBindings.Get(P1Action.Right)) axis += 1f;
            if (KeyBindings.Get(P1Action.Left)) axis -= 1f;
            return axis;
        }

        public static bool JumpDown(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen && KeyBindings.GetDown(P1Action.Jump);

        // 점프 키를 누르고 있는지 (짧게 누르면 낮은 점프). 막혀 있으면 뗀 것으로 본다.
        public static bool JumpHeld(PlayerControlState state) =>
            !ABlocked(state) && KeyBindings.Get(P1Action.Jump);

        public static bool RunHeld(PlayerControlState state) =>
            !ABlocked(state) && KeyBindings.Get(P1Action.Run);

        public static bool DashDown(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen && KeyBindings.GetDown(P1Action.Dash);

        public static bool TeleportDown(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen && KeyBindings.GetDown(P1Action.Teleport);

        // 마을 건물 등 앞에서 상호작용: ↑를 누르거나 W를 짧게 톡 (떼는 순간).
        // 순간이동은 W를 누른 채 Enter라, W를 누르는 동안 Enter가 눌렸으면 상호작용으로 치지 않는다. 매 프레임 불러야 W 기록이 맞다.
        private const float InteractTapSeconds = 0.35f;
        private static float interactWDownAt = -1f;
        private static bool interactEnterDuringW;

        public static bool InteractDown(PlayerControlState state)
        {
            if (KeyBindings.GetDown(P1Action.Up))
            {
                interactWDownAt = ABlocked(state) || state.MovementFrozen ? -1f : Time.unscaledTime; // 잠긴 동안(창을 W로 닫은 프레임 등) 누른 W는 톡으로 치지 않는다
                interactEnterDuringW = false;
            }
            if (KeyBindings.Get(P1Action.Up) && KeyBindings.GetDown(P1Action.Teleport)) interactEnterDuringW = true;
            bool tap = KeyBindings.GetUp(P1Action.Up) && interactWDownAt >= 0f && !interactEnterDuringW
                && Time.unscaledTime - interactWDownAt <= InteractTapSeconds;
            if (KeyBindings.GetUp(P1Action.Up)) interactWDownAt = -1f;
            return !ABlocked(state) && !state.MovementFrozen && (tap || (Input.GetKeyDown(KeyCode.UpArrow) && !KeyBindings.Uses(KeyCode.UpArrow))); // ↑를 P1 키로 바꿨으면 그 키는 위 톡으로만 (누르자마자 들어가지 않게)
        }

        // 바람 구멍 기둥 안에서 S를 누르고 있으면 천천히 내려간다 (WindVent). 순간이동은 Enter를 눌러야 하므로 겹치지 않는다.
        public static bool DescendHeld(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen && KeyBindings.Get(P1Action.Down);

        // 순간이동 방향: 누르고 있는 W/A/S/D (대각선 가능, 네 방향 고르기는 TeleportRules.Cardinal). 아무것도 안 누르면 zero.
        public static Vector2 TeleportDirection()
        {
            var dir = Vector2.zero;
            if (KeyBindings.Get(P1Action.Right)) dir.x += 1f;
            if (KeyBindings.Get(P1Action.Left)) dir.x -= 1f;
            if (KeyBindings.Get(P1Action.Up)) dir.y += 1f;
            if (KeyBindings.Get(P1Action.Down)) dir.y -= 1f;
            return dir.normalized;
        }

        // ---- 플레이어 B ----

        public static bool AttackDown(PlayerControlState state) => !BBlocked && !state.Locked && !state.ChantActive && !Dial.IsOpen && Time.frameCount != dialCancelFrame && P2Input.GetMouseButtonDown(0);

        public static Vector2 AimWorldPoint(Vector2 fallback)
        {
            var cam = Camera.main;
            if (cam == null) return fallback;
            var mouse = P2Input.mousePosition;
            mouse.z = -cam.transform.position.z;
            return cam.ScreenToWorldPoint(mouse);
        }

        // P2가 마우스만으로 공격 스킬을 쓰는 길 (2026-11-15 2인 점검): 한 키보드에서 숫자줄 1~4는 P1 왼손(W/A/S/D) 바로 위라 두 사람 손이 겹친다.
        // 휠로 속성을 고르고(불 → 물 → 바람 → 땅 → 불) 우클릭하면 그 스킬의 마법진이 열린다. 숫자줄 1~4도 그대로 된다.
        // 2026-11-15 사람 결정: 우클릭을 누르고 있으면 2단계 원형 다이얼(속성 → 그 속성의 배운 마법)이 펼쳐지고, 떼면 고른 마법의 마법진 (SpellDialRules).
        //   짧게 톡 누르면 예전처럼 지금 고른 마법. 다이얼이 열린 동안 게임은 멈추지 않는다. 좌클릭은 다이얼 취소 (평타는 안 나감).
        // 휠·숫자줄·다이얼은 같은 상태(MouseSpellIndex + 속성별 마지막 마법 칸)를 쓴다.
        // 마법진은 마우스로만 그리므로 그동안에도 P1 키보드는 막히지 않는다 (2026-11-16, 예전 영창 타이핑 때는 막혔다).
        public const int MouseSpellCount = SpellDialRules.ElementCount; // 불·물·바람·땅 (무속성은 평타)
        public static int MouseSpellIndex { get; private set; }
        public static Element MouseSpell => (Element)MouseSpellIndex;
        public static SpellDialState Dial { get; } = new SpellDialState();
        private static readonly int[] SpellSlotOf = new int[MouseSpellCount]; // 속성별 마지막으로 고른 마법 칸
        private static readonly int[] DialCounts = new int[MouseSpellCount];

        public static int SelectedSlot(Element element)
        {
            int e = (int)element;
            return e >= 0 && e < MouseSpellCount ? SpellSlotOf[e] : 0;
        }

        private static int dialCancelFrame = -1;
        private static bool dialRemote; // 다이얼을 열 때 온라인 친구가 P2였는지 (도중에 끊기거나 들어오면 다른 마우스로 놓여 바로 발동하던 것)

        public static void CloseSpellDial() => Dial.Cancel();

        public static bool SpellDown(PlayerControlState state, out Element element, out int slot)
        {
            element = Element.Neutral;
            slot = 0;
            if (BBlocked || state.Locked || state.ChantActive)
            {
                Dial.Cancel();
                return false;
            }
            // 개발자 모드: 상단 6~9·0은 기본 마법 즉시. 우클릭 다이얼도 똑같이 쓴다 (아카데미에서 배운 마법을 고르는 길, 리뷰 MEDIUM)
            if (GameSession.DevMode && !Dial.IsOpen && GetIndexedKeyDownB(TopRowLastFive, out element)) return true; // 다이얼이 열린 채면 막는다 (놓을 때 한 번 더 나가던 것)

            float wheel = P2Input.mouseScrollDelta.y;
            Vector2 mouse = P2Input.mousePosition;
            if (Dial.IsOpen)
            {
                if (P2Input.Remote != dialRemote)
                {
                    Dial.Cancel();
                    return false;
                }
                if (P2Input.GetMouseButtonDown(0))
                {
                    Dial.Cancel();
                    dialCancelFrame = Time.frameCount; // 같은 프레임에 PlayerCombat이 나중에 돌아도 평타가 나가지 않게
                    return false;
                }
                Dial.Update(mouse.x, mouse.y, wheel, Time.unscaledTime);
                if (P2Input.GetMouseButton(1)) return false;
                if (!Dial.Release(out int e, out int s)) return false;
                MouseSpellIndex = e;
                SpellSlotOf[e] = s;
                element = (Element)e;
                slot = s;
                return true;
            }

            if (wheel != 0f) // 휠 위로 = 다음 속성. 한 프레임에 한 칸만
                MouseSpellIndex = SpellDialRules.Step(MouseSpellIndex, MouseSpellCount, wheel);
            if (P2Input.GetMouseButtonDown(1))
            {
                for (int i = 0; i < MouseSpellCount; i++) DialCounts[i] = SpellBook.LearnedCount((Element)i);
                dialRemote = P2Input.Remote;
                Dial.Open(mouse.x, mouse.y, MouseSpellIndex, DialCounts, SpellSlotOf, Time.unscaledTime);
                return false;
            }
            if (!GetIndexedKeyDownB(TopRowFirstFive, out element)) return false;
            if ((int)element < MouseSpellCount)
            {
                MouseSpellIndex = (int)element; // 숫자줄로 고른 속성도 휠·다이얼과 같은 상태로
                slot = SpellSlotOf[(int)element];
            }
            return true;
        }

        // ---- 마법진 그리기 (SpellCaster) ----
        // 마우스 자리 (GUI 좌표: 왼쪽 위가 0). 온라인이면 친구 마우스 (P2Input)
        public static Vector2 CircleMouseGui()
        {
            var m = P2Input.mousePosition;
            return new Vector2(m.x, Screen.height - m.y);
        }

        public static bool CircleDrawDown() => !BBlocked && P2Input.GetMouseButtonDown(0);
        public static bool CircleDrawHeld() => !BBlocked && P2Input.GetMouseButton(0);
        // 마법진을 Esc로 취소한 프레임: 같은 프레임에 SkillHUD가 Esc를 보고 일시정지를 열지 않게 (컴포넌트 실행 순서와 무관, 2026-11-22)
        private static int circleCancelFrame = -1;
        public static void MarkCircleCancel() => circleCancelFrame = Time.frameCount;
        public static bool CircleCancelledThisFrame => Time.frameCount == circleCancelFrame;
        public static bool CircleCancelDown() => P2Input.GetKeyDown(KeyCode.Escape) || (!BBlocked && P2Input.GetMouseButtonDown(1));

        // B(P2)의 숫자줄: 온라인이면 친구 키보드
        private static bool GetIndexedKeyDownB(KeyCode[] keys, out Element element)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (P2Input.GetKeyDown(keys[i]))
                {
                    element = (Element)i;
                    return true;
                }
            }

            element = Element.Neutral;
            return false;
        }

        private static bool GetIndexedKeyDown(KeyCode[] keys, out Element element)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKeyDown(keys[i]))
                {
                    element = (Element)i;
                    return true;
                }
            }

            element = Element.Neutral;
            return false;
        }
    }
}
