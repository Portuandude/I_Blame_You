using UnityEngine;

namespace IBlameYou.Player
{
    // P1(플레이어 A) 조작 키. 규칙(기본값·맞바꾸기·금지 키·저장 문자열)은 순수 KeyBindingRules, 여기는 키 읽기·PlayerPrefs 저장만.
    // 처음 쓸 때 PlayerPrefs에서 읽는다 (저장값이 없으면 지금까지와 똑같은 기본 키). 바꾸는 창은 Core/KeyBindingsWindow.
    // 온라인에서는 호스트 컴퓨터 키보드가 A라 호스트 쪽 설정만 쓴다 (친구 B 입력은 P2Input, 바꾸지 않는다).
    public static class KeyBindings
    {
        private const string PrefsKey = "Settings.P1Keys";
        private static int[,] current;

        // 바인딩이 바뀔 때마다 1씩 오른다 (안내 문구 캐시를 다시 만들 때 비교용)
        public static int Revision { get; private set; }

        private static int[,] Current
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        public static void Load()
        {
            string text = PlayerPrefs.GetString(PrefsKey, "");
            current = KeyBindingRules.Parse(text, out bool repaired);
            if (repaired) Debug.LogWarning($"[KeyBindings] 저장된 조작 키가 깨져 일부(또는 전부)를 기본값으로 되돌림: \"{text}\"");
        }

        private static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, KeyBindingRules.Serialize(Current));
            PlayerPrefs.Save();
            Revision++;
        }

        public static bool Get(P1Action action)
        {
            var b = Current;
            int a = (int)action;
            for (int s = 0; s < KeyBindingRules.Slots; s++)
                if (b[a, s] != KeyBindingRules.None && Input.GetKey((KeyCode)b[a, s])) return true;
            return false;
        }

        public static bool GetDown(P1Action action)
        {
            var b = Current;
            int a = (int)action;
            for (int s = 0; s < KeyBindingRules.Slots; s++)
                if (b[a, s] != KeyBindingRules.None && Input.GetKeyDown((KeyCode)b[a, s])) return true;
            return false;
        }

        public static bool GetUp(P1Action action)
        {
            var b = Current;
            int a = (int)action;
            for (int s = 0; s < KeyBindingRules.Slots; s++)
                if (b[a, s] != KeyBindingRules.None && Input.GetKeyUp((KeyCode)b[a, s])) return true;
            return false;
        }

        // 이 키가 P1 동작 어딘가에 쓰이는지 (↑ 상호작용 대체 키처럼 고정 키가 바꾼 키와 겹칠 때 고정 키 쪽을 끈다)
        public static bool Uses(KeyCode key)
        {
            var b = Current;
            for (int a = 0; a < KeyBindingRules.ActionCount; a++)
                for (int s = 0; s < KeyBindingRules.Slots; s++)
                    if (b[a, s] == (int)key) return true;
            return false;
        }

        // 안내 문구용 이름 (기본값: "A", "Space", "Shift", "L.Ctrl", "Enter")
        public static string Name(P1Action action) => KeyBindingRules.Label(Current, action, code => ((KeyCode)code).ToString());

        public static bool IsDefault => KeyBindingRules.SameAs(Current, KeyBindingRules.Defaults());

        // 결과: 금지 키·그대로·바꿈·맞바꿈 (swappedWith = 키를 넘겨받은 동작)
        public static KeyBindingRules.AssignResult Set(P1Action action, KeyCode key, out P1Action swappedWith)
        {
            var result = KeyBindingRules.Assign(Current, action, (int)key, out swappedWith);
            if (result == KeyBindingRules.AssignResult.Set || result == KeyBindingRules.AssignResult.Swapped) Save();
            return result;
        }

        public static void ResetDefaults()
        {
            KeyBindingRules.CopyFrom(Current, KeyBindingRules.Defaults());
            Save();
        }

        // 도움말·시작 화면용 묶음 이름
        public static string MoveKeys => $"{Name(P1Action.Left)}/{Name(P1Action.Right)}";
        public static string TeleportKeys =>
            $"{Name(P1Action.Up)}/{Name(P1Action.Left)}/{Name(P1Action.Down)}/{Name(P1Action.Right)}+{Name(P1Action.Teleport)}";
    }
}
