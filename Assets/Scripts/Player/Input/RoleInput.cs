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
    //   방향키 + Enter: 순간이동
    //   넘패드 1~5: 속성 방어막 → W/A/S/D 순서 입력
    //
    // 개발자 모드: 상단 1~5 = A 방어막 즉시, 6~9·0 = B 공격 스킬 즉시 (미니게임 생략).
    public static class RoleInput
    {
        private static readonly KeyCode[] NumpadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5 };
        private static readonly KeyCode[] TopRowFirstFive = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
        private static readonly KeyCode[] TopRowLastFive = { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };

        // ---- 플레이어 A ----

        public static float MoveAxis(PlayerControlState state)
        {
            if (state.AKeyboardBlocked || state.MovementFrozen) return 0f;
            float axis = 0f;
            if (Input.GetKey(KeyCode.D)) axis += 1f;
            if (Input.GetKey(KeyCode.A)) axis -= 1f;
            return axis;
        }

        public static bool JumpDown(PlayerControlState state) =>
            !state.AKeyboardBlocked && !state.MovementFrozen && Input.GetKeyDown(KeyCode.Space);

        public static bool RunHeld(PlayerControlState state) =>
            !state.AKeyboardBlocked && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        public static bool DashDown(PlayerControlState state) =>
            !state.AKeyboardBlocked && !state.MovementFrozen && Input.GetKeyDown(KeyCode.LeftControl);

        public static bool TeleportDown(PlayerControlState state) =>
            !state.AKeyboardBlocked && !state.MovementFrozen &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter));

        // 순간이동 방향: 누르고 있는 방향키 (대각선 가능). 아무것도 안 누르면 zero.
        public static Vector2 TeleportDirection()
        {
            var dir = Vector2.zero;
            if (Input.GetKey(KeyCode.RightArrow)) dir.x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.UpArrow)) dir.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) dir.y -= 1f;
            return dir.normalized;
        }

        public static bool ShieldDown(PlayerControlState state, out Element element)
        {
            element = Element.Neutral;
            if (state.AKeyboardBlocked || state.ShieldMinigameActive) return false;
            return GetIndexedKeyDown(GameSession.DevMode ? TopRowFirstFive : NumpadKeys, out element);
        }

        // 방어막 미니게임 입력 (W/A/S/D 중 이번 프레임에 눌린 것). 없으면 false.
        public static bool MinigameArrowDown(out char key)
        {
            key = '\0';
            if (Input.GetKeyDown(KeyCode.W)) key = 'W';
            else if (Input.GetKeyDown(KeyCode.A)) key = 'A';
            else if (Input.GetKeyDown(KeyCode.S)) key = 'S';
            else if (Input.GetKeyDown(KeyCode.D)) key = 'D';
            return key != '\0';
        }

        // ---- 플레이어 B ----

        public static bool AttackDown(PlayerControlState state) => !state.Locked && !state.ChantActive && Input.GetMouseButtonDown(0);

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
            if (state.Locked || state.ChantActive) return false;
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
