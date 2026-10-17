using System;
using UnityEngine;

namespace IBlameYou.Core
{
    // 게임 시작 전 화면 (기획서 2.8/2.9). 개발자 모드를 켜고 끄고, 로컬 테스트를 시작한다.
    // 온라인(데디케이티드 서버)은 아직 없어 버튼만 자리를 잡아 둔다.
    // 테스트 단계라 UI 에셋 없이 IMGUI로 그린다.
    public class StartMenu : MonoBehaviour
    {
        private Action onStart;
        private bool devMode;
        private GUIStyle title;
        private GUIStyle body;
        private GUIStyle button;
        private GUIStyle toggle;

        public void Initialize(Action startCallback)
        {
            onStart = startCallback;
            devMode = GameSession.DevMode;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.85f, 0.4f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            body.normal.textColor = Color.white;
            button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 20 };
        }

        private void OnGUI()
        {
            EnsureStyles();

            const float width = 640f;
            const float height = 520f;
            var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 16f, width - 48f, height - 32f));
            GUILayout.Label("I Blame You!", title, GUILayout.Height(60f));
            GUILayout.Label("한 마법사를 두 사람이 나눠 조작한다. A는 이동·방어, B는 공격. 마나는 하나뿐이다.", body);
            GUILayout.Space(12f);

            devMode = GUILayout.Toggle(devMode, "  개발자 모드 (한 사람이 A/B 동시 조작, 미니게임 생략)", toggle);
            GUILayout.Space(8f);
            GUILayout.Label(devMode ? DevModeHelp : NormalModeHelp, body);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("로컬 테스트 시작", button, GUILayout.Height(48f)))
            {
                GameSession.DevMode = devMode;
                GameSession.Started = true;
                var callback = onStart;
                Destroy(this);
                callback?.Invoke();
            }

            GUI.enabled = false;
            GUILayout.Button("온라인 (데디케이티드 서버) — 준비 중", button, GUILayout.Height(40f));
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private const string DevModeHelp =
            "A  이동 A/D · 점프 Space · 달리기 Shift · 대쉬 Left Ctrl · 순간이동 방향키+Enter\n" +
            "A  방어막 상단 1~5 (불/물/바람/땅/무) 즉시 발동\n" +
            "B  평타 마우스 좌클릭 (마우스 쪽으로)\n" +
            "B  공격 스킬 상단 6~9, 0 (불/물/바람/땅/무) 즉시 발동, 마우스 방향\n" +
            "치트  F1 무적 · F2 마나 무한 · F3 방 안의 적 처치 · F4 체력 회복";

        private const string NormalModeHelp =
            "A  이동 A/D · 점프 Space · 달리기 Shift · 대쉬 Left Ctrl · 순간이동 방향키+Enter\n" +
            "A  방어막 넘패드 1~5 → 화면에 뜬 순서대로 W/A/S/D (입력 중 캐릭터 정지, 틀리면 마나만 소모)\n" +
            "B  평타 마우스 좌클릭\n" +
            "B  공격 스킬 상단 1~5 → 영창문을 똑같이 타이핑 → Enter (오타면 마나만 소모, Esc 취소)\n" +
            "지금은 한 키보드를 같이 써서, B가 타이핑하는 동안 A의 키 입력은 막힌다.";
    }
}
