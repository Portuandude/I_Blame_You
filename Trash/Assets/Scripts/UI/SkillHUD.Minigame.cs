using UnityEngine;

namespace IBlameYou.UI
{
    // 방어막 미니게임·영창 입력 연출 (귀엽지만 화려하게).
    // - 화살표 칸을 맞출 때마다 그 칸이 aqua로 번쩍이며 1.2배로 커졌다 돌아온다 (0.22초)
    // - 지금 눌러야 할 칸은 gold로 숨 쉬듯 깜빡인다
    // - 남은 시간은 캡슐 바 (초록 → berry)
    // - 영창: 지금까지 맞게 친 앞부분은 aqua로 칠해지고, 틀린 글자가 있으면 입력 칸 테두리가 berry
    // 틀린 키·오타는 게임 쪽에서 바로 실패 처리되어 창이 닫히므로 흔들림 연출은 두지 않는다.
    public partial class SkillHUD
    {
        private const float KeyPopTime = 0.22f;
        private static readonly Color KeyAqua = new Color(0.5f, 0.96f, 1f);
        private static readonly Color KeyGold = new Color(1f, 0.85f, 0.3f);
        private static readonly Color KeyBerry = new Color(1f, 0.44f, 0.68f);
        private static readonly Color KeyLeaf = new Color(0.45f, 0.9f, 0.4f);

        private readonly float[] keyMatchedAt = new float[8];
        private int keyLastProgress = -1;

        // 이번 프레임의 Progress를 보고 새로 맞춘 칸의 시각을 기록한다. 미니게임이 새로 시작되면(Progress 감소) 초기화.
        private void TrackShieldKeys(int progress)
        {
            if (keyLastProgress < 0 || progress < keyLastProgress)
            {
                for (int i = 0; i < keyMatchedAt.Length; i++) keyMatchedAt[i] = -10f;
                keyLastProgress = 0;
            }

            for (int i = keyLastProgress; i < progress && i < keyMatchedAt.Length; i++) keyMatchedAt[i] = Time.unscaledTime;
            keyLastProgress = progress;
        }

        // 화살표 칸 하나. state: 맞춤(aqua 팝) / 지금 칸(gold 깜빡) / 아직(흰색)
        private void DrawShieldKey(Rect box, string key, int index, int progress)
        {
            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;

            if (index < progress)
            {
                float t = index < keyMatchedAt.Length ? (Time.unscaledTime - keyMatchedAt[index]) / KeyPopTime : 1f;
                if (t < 1f)
                {
                    float scale = 1f + 0.2f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    GUIUtility.ScaleAroundPivot(Vector2.one * scale, box.center);
                    GUI.color = Color.Lerp(Color.white, KeyAqua, Mathf.Clamp01(t * 2f)); // 처음엔 흰 번쩍 → aqua
                }
                else
                {
                    GUI.color = KeyAqua;
                }
            }
            else if (index == progress)
            {
                GUI.color = Color.Lerp(Color.white, KeyGold, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f));
            }

            GUI.Box(box, key, keyBox);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        // 영창 글자: 맞게 친 앞부분은 aqua. 돌려주는 값은 틀린 글자가 있는지.
        private static string ChantProgressText(string chant, string typed, out bool mistake)
        {
            int match = 0;
            while (match < chant.Length && match < typed.Length && chant[match] == typed[match]) match++;
            mistake = typed.Length > match;
            if (match == 0) return $"<b>{chant}</b>";
            return $"<b><color=#7ff6ff>{chant.Substring(0, match)}</color>{chant.Substring(match)}</b>";
        }
    }
}
