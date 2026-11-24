using System;
using System.Text;

namespace IBlameYou.Player
{
    // P1(플레이어 A, 키보드) 동작. 순서는 저장 문자열 순서이므로 바꾸지 않는다 (새 동작은 끝에 더하고 버전을 올린다).
    public enum P1Action { Left, Right, Up, Down, Jump, Run, Dash, Teleport }

    // P1 조작 키 바꾸기 규칙 (순수 C#, EditMode 테스트 대상). 스팀 출시 준비 (2026-11-23 자동 개발).
    // 키는 UnityEngine.KeyCode의 정수 값으로 다룬다 (이 어셈블리는 UnityEngine을 모른다. 값 일치는 Unity 쪽 KeyBindings가 쓴다).
    // 동작마다 칸 두 개: 기본값은 지금과 똑같다 — 달리기는 왼·오른 Shift, 순간이동은 Enter·키패드 Enter. 0 = 비어 있음.
    public static class KeyBindingRules
    {
        public const int ActionCount = 8;
        public const int Slots = 2;
        public const int None = 0;
        public const string Version = "v1";

        // UnityEngine.KeyCode 값
        public const int Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32;
        public const int Alpha0 = 48, Alpha9 = 57;
        public const int BackQuote = 96;
        public const int A = 97, C = 99, D = 100, M = 109, R = 114, S = 115, W = 119, Z = 122;
        public const int Keypad0 = 256, Keypad9 = 265, KeypadEnter = 271;
        public const int UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276;
        public const int F1 = 282, F15 = 296;
        public const int RightShift = 303, LeftShift = 304, RightControl = 305, LeftControl = 306, RightAlt = 307, LeftAlt = 308;
        public const int FirstMouseOrJoystick = 323; // Mouse0부터 마우스·패드 버튼
        public const int MinKey = 8;                  // Backspace보다 작은 값은 키가 아니다

        public static int[,] Defaults()
        {
            var b = new int[ActionCount, Slots];
            Put(b, P1Action.Left, A);
            Put(b, P1Action.Right, D);
            Put(b, P1Action.Up, W);
            Put(b, P1Action.Down, S);
            Put(b, P1Action.Jump, Space);
            Put(b, P1Action.Run, LeftShift, RightShift);
            Put(b, P1Action.Dash, LeftControl);
            Put(b, P1Action.Teleport, Return, KeypadEnter);
            return b;
        }

        private static void Put(int[,] b, P1Action action, int first, int second = None)
        {
            b[(int)action, 0] = first;
            b[(int)action, 1] = second;
        }

        // P1이 쓸 수 없는 키: Esc(일시정지·창 닫기), 숫자줄 1~0(P2 공격 스킬), 백틱(기능 테스트 A/B 전환), F1~F15(개발자·시험 키),
        // Tab(특성 창), C(도감), R·M(결과 창 다시 시작), 마우스·패드 버튼, 키가 아닌 값.
        public static bool IsForbidden(int code)
        {
            if (code < MinKey || code >= FirstMouseOrJoystick) return true;
            if (code >= Alpha0 && code <= Alpha9) return true;
            if (code >= F1 && code <= F15) return true;
            switch (code)
            {
                case Escape: case BackQuote: case Tab: case C: case R: case M:
                    return true;
            }
            return false;
        }

        public enum AssignResult { Forbidden, Unchanged, Set, Swapped }

        // action의 첫 칸을 code로 바꾸고 둘째 칸은 비운다 (기본 Shift 두 개처럼 둘째 칸이 남아 엉뚱한 키가 같이 되지 않게).
        // 다른 동작이 이미 code를 쓰고 있으면 그 동작에 action의 원래 첫 키를 넘겨준다 (맞바꾸기). swappedWith = 그 동작.
        public static AssignResult Assign(int[,] b, P1Action action, int code, out P1Action swappedWith)
        {
            swappedWith = action;
            if (b == null || IsForbidden(code)) return AssignResult.Forbidden;
            int a = (int)action;
            if (b[a, 0] == code || b[a, 1] == code) return AssignResult.Unchanged; // 이미 이 동작의 키 (기본 Shift 두 개를 하나로 줄이지 않는다)

            int old = b[a, 0];
            int owner = -1, ownerSlot = -1;
            for (int i = 0; i < ActionCount && owner < 0; i++)
            {
                if (i == a) continue;
                for (int s = 0; s < Slots; s++)
                    if (b[i, s] == code) { owner = i; ownerSlot = s; break; }
            }

            b[a, 0] = code;
            b[a, 1] = None;
            if (owner < 0) return AssignResult.Set;

            b[owner, ownerSlot] = old;
            Compact(b, owner);
            if (b[owner, 0] == None) // 넘겨줄 키가 없었다 (있을 수 없지만): 그 동작은 기본값으로
            {
                var d = Defaults();
                b[owner, 0] = d[owner, 0];
                b[owner, 1] = None;
                if (HasDuplicates(b)) { CopyFrom(b, d); return AssignResult.Swapped; }
            }
            swappedWith = (P1Action)owner;
            return AssignResult.Swapped;
        }

        // 빈 첫 칸 앞당기기, 두 칸이 같으면 둘째 비우기
        private static void Compact(int[,] b, int a)
        {
            if (b[a, 0] == None && b[a, 1] != None) { b[a, 0] = b[a, 1]; b[a, 1] = None; }
            if (b[a, 1] == b[a, 0]) b[a, 1] = None;
        }

        public static bool HasDuplicates(int[,] b)
        {
            for (int i = 0; i < ActionCount; i++)
                for (int s = 0; s < Slots; s++)
                {
                    int code = b[i, s];
                    if (code == None) continue;
                    for (int j = i; j < ActionCount; j++)
                        for (int t = j == i ? s + 1 : 0; t < Slots; t++)
                            if (b[j, t] == code) return true;
                }
            return false;
        }

        public static bool SameAs(int[,] x, int[,] y)
        {
            for (int i = 0; i < ActionCount; i++)
                for (int s = 0; s < Slots; s++)
                    if (x[i, s] != y[i, s]) return false;
            return true;
        }

        public static void CopyFrom(int[,] target, int[,] source)
        {
            for (int i = 0; i < ActionCount; i++)
                for (int s = 0; s < Slots; s++)
                    target[i, s] = source[i, s];
        }

        // "v1:97,0;100,0;...". 저장용
        public static string Serialize(int[,] b)
        {
            var sb = new StringBuilder(Version).Append(':');
            for (int i = 0; i < ActionCount; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(b[i, 0]).Append(',').Append(b[i, 1]);
            }
            return sb.ToString();
        }

        // 저장 문자열 → 바인딩. 형식이 깨졌으면 전부 기본값, 한 동작만 이상하면(금지 키·빈 칸) 그 동작만 기본값,
        // 그래도 키가 겹치면 전부 기본값. repaired = 저장값을 고쳐서 읽었는지 (빈 값은 고친 것으로 치지 않는다).
        public static int[,] Parse(string text, out bool repaired)
        {
            repaired = false;
            var defaults = Defaults();
            if (string.IsNullOrEmpty(text)) return defaults;

            int colon = text.IndexOf(':');
            string[] parts = colon > 0 && text.Substring(0, colon) == Version ? text.Substring(colon + 1).Split(';') : null;
            if (parts == null || parts.Length != ActionCount) { repaired = true; return defaults; }

            var b = new int[ActionCount, Slots];
            for (int i = 0; i < ActionCount; i++)
            {
                string[] pair = parts[i].Split(',');
                if (pair.Length != Slots) { repaired = true; return defaults; }
                bool bad = false;
                for (int s = 0; s < Slots; s++)
                {
                    if (!int.TryParse(pair[s], out int code)) { repaired = true; return defaults; }
                    if (code != None && IsForbidden(code)) bad = true;
                    b[i, s] = code;
                }
                Compact(b, i);
                if (bad || b[i, 0] == None)
                {
                    repaired = true;
                    b[i, 0] = defaults[i, 0];
                    b[i, 1] = defaults[i, 1];
                }
            }
            if (HasDuplicates(b)) { repaired = true; return defaults; }
            return b;
        }

        // 안내 문구용 키 이름. '+'·'/'는 쓰지 않는다 (도움말이 "W/A/S/D+Enter"를 '+'로, 묶음을 '/'로 나눈다). 모르는 키는 null (Unity 쪽이 KeyCode 이름으로 채운다).
        public static string DisplayName(int code)
        {
            if (code >= A && code <= Z) return ((char)('A' + (code - A))).ToString();
            if (code >= Keypad0 && code <= Keypad9) return "Num" + (code - Keypad0);
            switch (code)
            {
                case Space: return "Space";
                case Return: return "Enter";
                case KeypadEnter: return "Num Enter";
                case Backspace: return "Backspace";
                case LeftShift: return "L.Shift";
                case RightShift: return "R.Shift";
                case LeftControl: return "L.Ctrl";
                case RightControl: return "R.Ctrl";
                case LeftAlt: return "L.Alt";
                case RightAlt: return "R.Alt";
                case UpArrow: return "↑";
                case DownArrow: return "↓";
                case LeftArrow: return "←";
                case RightArrow: return "→";
                case 266: return "Num.";
                case 267: return "Num Slash"; // 도움말이 "/"로 키를 나눈다
                case 268: return "Num*";
                case 269: return "Num-";
                case 270: return "Num Plus";
                case 277: return "Insert";
                case 278: return "Home";
                case 279: return "End";
                case 280: return "PageUp";
                case 281: return "PageDown";
                case 301: return "CapsLock";
                case 127: return "Delete";
                case 39: return "'";
                case 44: return ",";
                case 45: return "-";
                case 46: return ".";
                case 47: return "Slash";
                case 59: return ";";
                case 61: return "=";
                case 91: return "[";
                case 92: return "\\";
                case 93: return "]";
            }
            return null;
        }

        // 동작 하나의 안내 이름. 기본 두 칸 묶음은 짧게: 왼·오른 Shift → "Shift", Enter·키패드 Enter → "Enter".
        // unknownName: DisplayName이 모르는 키 이름 (Unity 쪽은 KeyCode.ToString). null이면 "#코드".
        public static string Label(int[,] b, P1Action action, Func<int, string> unknownName = null)
        {
            int a = (int)action;
            int first = b[a, 0], second = b[a, 1];
            if ((first == LeftShift && second == RightShift) || (first == RightShift && second == LeftShift)) return "Shift";
            if (first == Return && second == KeypadEnter) return "Enter";
            if (first == None) return "-";
            return DisplayName(first) ?? unknownName?.Invoke(first) ?? "#" + first;
        }

        public static string ActionName(P1Action action)
        {
            switch (action)
            {
                case P1Action.Left: return "왼쪽 이동";
                case P1Action.Right: return "오른쪽 이동";
                case P1Action.Up: return "위 (순간이동·들어가기)";
                case P1Action.Down: return "아래 (순간이동·내려가기)";
                case P1Action.Jump: return "점프";
                case P1Action.Run: return "달리기";
                case P1Action.Dash: return "대쉬";
                case P1Action.Teleport: return "순간이동";
            }
            return action.ToString();
        }
    }
}
