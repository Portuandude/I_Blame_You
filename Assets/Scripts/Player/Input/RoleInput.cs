using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Player
{
    // 플레이어 A/B 역할별 입력. 지금은 로컬 키보드/마우스에서 읽지만, 데디케이티드 서버가 붙으면
    // 각 클라이언트가 자기 역할의 입력만 서버로 보내도록 이 클래스만 바꾸면 된다.
    //
    // 플레이어 A (이동·방어)                  플레이어 B (공격)
    //   A/D 이동, Space 점프                     마우스 조준, 좌클릭 평타
    //   Shift 달리기, Left Ctrl 대쉬             상단 1~5: 공격 스킬 선택 → 영창 타이핑 → Enter
    //   W/A/S/D + Enter: 순간이동 (2026-11-12 방향키에서 바꿈)
    //   W(또는 ↑): 마을 건물 앞에서 들어가기
    //   넘패드 1~5: 속성 방어막 → W/A/S/D 순서 입력
    //
    // 개발자 모드: 상단 1~5 = A 방어막 즉시, 6~9·0 = B 공격 스킬 즉시 (미니게임 생략).
    // 기능 테스트의 A/B 전환 (2026-11-14 사람 결정 ④ 두 사람 플레이 첫 단계): 한 사람이 한 역할만 조작해 보며 각자 무엇을 하는지 시험한다.
    public enum ControlFocus { Both, A, B }

    public static class RoleInput
    {
        // Both = 평소 (한 키보드로 둘 다). A = A 입력만 받고 B(마우스 공격·공격 스킬)는 막는다. B = 그 반대.
        // 기능 테스트에서 ` 키로 바꾼다 (FeatureTestTools). 시험 맵을 나가면 Both로 돌아간다.
        public static ControlFocus Focus { get; set; } = ControlFocus.Both;

        private static bool ABlocked(PlayerControlState state) => state.AKeyboardBlocked || Focus == ControlFocus.B;
        private static bool BBlocked => Focus == ControlFocus.A;

        private static readonly KeyCode[] NumpadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5 };
        private static readonly KeyCode[] TopRowFirstFive = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
        private static readonly KeyCode[] TopRowLastFive = { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };

        // ---- 플레이어 A ----

        public static float MoveAxis(PlayerControlState state)
        {
            if (ABlocked(state) || state.MovementFrozen) return 0f;
            float axis = 0f;
            if (Input.GetKey(KeyCode.D)) axis += 1f;
            if (Input.GetKey(KeyCode.A)) axis -= 1f;
            return axis;
        }

        public static bool JumpDown(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen && Input.GetKeyDown(KeyCode.Space);

        // 점프 키를 누르고 있는지 (짧게 누르면 낮은 점프). 막혀 있으면 뗀 것으로 본다.
        public static bool JumpHeld(PlayerControlState state) =>
            !ABlocked(state) && Input.GetKey(KeyCode.Space);

        public static bool RunHeld(PlayerControlState state) =>
            !ABlocked(state) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        public static bool DashDown(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen && Input.GetKeyDown(KeyCode.LeftControl);

        public static bool TeleportDown(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter));

        // 마을 건물 등 앞에서 상호작용: ↑를 누르거나 W를 짧게 톡 (떼는 순간).
        // 순간이동은 W를 누른 채 Enter라, W를 누르는 동안 Enter가 눌렸으면 상호작용으로 치지 않는다. 매 프레임 불러야 W 기록이 맞다.
        private const float InteractTapSeconds = 0.35f;
        private static float interactWDownAt = -1f;
        private static bool interactEnterDuringW;

        public static bool InteractDown(PlayerControlState state)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                interactWDownAt = ABlocked(state) || state.MovementFrozen ? -1f : Time.unscaledTime; // 잠긴 동안(창을 W로 닫은 프레임 등) 누른 W는 톡으로 치지 않는다
                interactEnterDuringW = false;
            }
            if (Input.GetKey(KeyCode.W) && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) interactEnterDuringW = true;
            bool tap = Input.GetKeyUp(KeyCode.W) && interactWDownAt >= 0f && !interactEnterDuringW
                && Time.unscaledTime - interactWDownAt <= InteractTapSeconds;
            if (Input.GetKeyUp(KeyCode.W)) interactWDownAt = -1f;
            return !ABlocked(state) && !state.MovementFrozen && (tap || Input.GetKeyDown(KeyCode.UpArrow));
        }

        // 바람 구멍 기둥 안에서 S를 누르고 있으면 천천히 내려간다 (WindVent). 순간이동은 Enter를 눌러야 하므로 겹치지 않는다.
        public static bool DescendHeld(PlayerControlState state) =>
            !ABlocked(state) && !state.MovementFrozen && Input.GetKey(KeyCode.S);

        // 순간이동 방향: 누르고 있는 W/A/S/D (대각선 가능, 네 방향 고르기는 TeleportRules.Cardinal). 아무것도 안 누르면 zero.
        public static Vector2 TeleportDirection()
        {
            var dir = Vector2.zero;
            if (Input.GetKey(KeyCode.D)) dir.x += 1f;
            if (Input.GetKey(KeyCode.A)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.W)) dir.y += 1f;
            if (Input.GetKey(KeyCode.S)) dir.y -= 1f;
            return dir.normalized;
        }

        public static bool ShieldDown(PlayerControlState state, out Element element)
        {
            element = Element.Neutral;
            if (ABlocked(state) || state.ShieldMinigameActive) return false;
            return GetIndexedKeyDown(GameSession.DevMode ? TopRowFirstFive : NumpadKeys, out element);
        }

        // 방어막 미니게임 입력 (W/A/S/D 중 이번 프레임에 눌린 것). 없으면 false.
        public static bool MinigameArrowDown(out char key)
        {
            key = '\0';
            if (Focus == ControlFocus.B) return false; // B만 조작 중이면 A의 방패 미니게임 입력도 막는다
            if (Input.GetKeyDown(KeyCode.W)) key = 'W';
            else if (Input.GetKeyDown(KeyCode.A)) key = 'A';
            else if (Input.GetKeyDown(KeyCode.S)) key = 'S';
            else if (Input.GetKeyDown(KeyCode.D)) key = 'D';
            return key != '\0';
        }

        // ---- 플레이어 B ----

        public static bool AttackDown(PlayerControlState state) => !BBlocked && !state.Locked && !state.ChantActive && Input.GetMouseButtonDown(0);

        public static Vector2 AimWorldPoint(Vector2 fallback)
        {
            var cam = Camera.main;
            if (cam == null) return fallback;
            var mouse = Input.mousePosition;
            mouse.z = -cam.transform.position.z;
            return cam.ScreenToWorldPoint(mouse);
        }

        public static bool SpellDown(PlayerControlState state, out Element element)
        {
            element = Element.Neutral;
            if (BBlocked || state.Locked || state.ChantActive) return false;
            if (GameSession.DevMode) return GetIndexedKeyDown(TopRowLastFive, out element);
            if (state.ShieldMinigameActive && GameSession.SharedKeyboard) return false;
            return GetIndexedKeyDown(TopRowFirstFive, out element);
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
