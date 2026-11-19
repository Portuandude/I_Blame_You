using UnityEngine;

namespace IBlameYou.Net
{
    // P2(공격 담당)의 마우스·키 입력을 읽는 한 곳. 한 컴퓨터로 할 때는 Unity Input 그대로,
    // 온라인 호스트일 때는 친구 컴퓨터가 보낸 입력 (NetSession.Tracker)을 같은 모양으로 돌려준다.
    // 그래서 호스트 사람(P1)이 마우스를 건드려도 P2 공격이 나가지 않는다.
    // 마우스 좌표는 Input.mousePosition과 같이 왼쪽 아래가 0인 화면 픽셀. 친구 화면 비율(0~1)을 호스트 창 크기에 맞춰 바꾼다.
    public static class P2Input
    {
        public static bool Remote => NetSession.RemoteP2Active;

        public static Vector3 mousePosition
        {
            get
            {
                if (!Remote) return Input.mousePosition;
                var s = NetSession.Tracker.Latest;
                return new Vector3(s.MouseX * Screen.width, s.MouseY * Screen.height, 0f);
            }
        }

        public static Vector2 mouseScrollDelta => Remote ? new Vector2(0f, NetSession.Tracker.WheelDelta) : Input.mouseScrollDelta;

        public static bool GetMouseButton(int button) =>
            Remote ? ValidButton(button) && NetSession.Tracker.Held(button) : Input.GetMouseButton(button);

        public static bool GetMouseButtonDown(int button) =>
            Remote ? ValidButton(button) && NetSession.Tracker.ButtonDown[button] : Input.GetMouseButtonDown(button);

        public static bool GetMouseButtonUp(int button) =>
            Remote ? ValidButton(button) && NetSession.Tracker.ButtonUp[button] : Input.GetMouseButtonUp(button);

        public static bool GetKeyDown(KeyCode key)
        {
            if (!Remote) return Input.GetKeyDown(key);
            return TryMap(key, out var k) && NetSession.Tracker.KeyDown[(int)k];
        }

        public static bool GetKey(KeyCode key)
        {
            if (!Remote) return Input.GetKey(key);
            return TryMap(key, out var k) && NetSession.Tracker.KeyHeld(k);
        }

        private static bool ValidButton(int button) => button >= 0 && button < P2InputState.ButtonCount;

        // 친구 컴퓨터에서 보내는 키 (P2Key)와 Unity 키 이름을 잇는다. 클라이언트 쪽 보내기(NetClientView)도 같은 표를 쓴다.
        public static readonly KeyCode[] KeyCodes =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
            KeyCode.Space, KeyCode.Escape, KeyCode.Return,
        };

        public static bool TryMap(KeyCode key, out P2Key result)
        {
            for (int i = 0; i < KeyCodes.Length; i++)
            {
                if (KeyCodes[i] != key) continue;
                result = (P2Key)i;
                return true;
            }
            if (key == KeyCode.KeypadEnter)
            {
                result = P2Key.Return;
                return true;
            }
            result = default(P2Key);
            return false;
        }
    }
}
