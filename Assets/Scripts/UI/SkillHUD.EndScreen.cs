using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 게임오버·클리어 화면 연출 (귀엽지만 화려하게).
    // - 패널이 0.6배에서 튀어 올라 1배로 (처음 그려진 순간부터 0.35초)
    // - 제목은 OutlinedLabel 테두리 글자. 게임오버 "누구 탓이지?"는 좌우로 갸우뚱 (±4도)
    // - 클리어는 화면 위에서 gold·berry·aqua 별·하트 색종이가 계속 떨어진다 (패널 뒤)
    // 쓰러짐 연출은 Time.timeScale을 낮추므로 시간은 unscaledTime으로 잰다. 재시작하면 HUD가 새로 만들어져 초기화된다.
    public partial class SkillHUD
    {
        private const float EndPopTime = 0.35f;
        private const int ConfettiCount = 36;
        private static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.85f, 0.3f),   // gold
            new Color(1f, 0.44f, 0.68f),  // berry
            new Color(0.5f, 0.96f, 1f),   // aqua
        };

        private float endScreenShownAt = -1f;
        private float defeatTime; // 쓰러진 순간까지 걸린 시간 (Update에서 deathHandled를 세울 때)
        private GUIStyle endTitle;

        // 결과 창이 처음 그려지는 순간: 배경음악을 줄여 끄고 클리어 팡파르 / 게임오버 징글 (Tools/AudioGen Jingle_*).
        private void MarkEndScreenShown()
        {
            if (endScreenShownAt >= 0f) return;
            endScreenShownAt = Time.unscaledTime;
            GameAudio.StopMusic();
            GameAudio.PlaySfx(deathHandled ? "Jingle_Defeat" : "Jingle_Clear", 0.9f, 0f);
        }

        // 패널을 그리기 전에 부른다. 돌려받은 행렬을 다 그린 뒤 GUI.matrix에 되돌린다.
        private Matrix4x4 BeginEndScreenPop(Rect rect)
        {
            MarkEndScreenShown();
            var previous = GUI.matrix;
            float t = (Time.unscaledTime - endScreenShownAt) / EndPopTime;
            if (t >= 1f) return previous;
            float scale = t < 0.6f
                ? Mathf.Lerp(0.6f, 1.1f, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Mathf.Lerp(1.1f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, rect.center);
            return previous;
        }

        // 제목 (테두리 글자). wobble이면 글자 가운데를 기준으로 좌우로 갸우뚱.
        private void DrawEndTitle(Rect rect, string text, bool wobble)
        {
            if (endTitle == null)
            {
                endTitle = new GUIStyle(big) { fontSize = 30, richText = false };
                endTitle.normal.textColor = Color.white;
            }

            var previous = GUI.matrix;
            if (wobble) GUIUtility.RotateAroundPivot(Mathf.Sin(Time.unscaledTime * 2.4f) * 4f, rect.center);
            OutlinedLabel.Draw(rect, text, endTitle, OutlinedLabel.Cream, OutlinedLabel.Gold, 2f, 1.5f);
            GUI.matrix = previous;
        }

        // 클리어 색종이: 조각마다 고정된 무작위 값(인덱스 해시)으로 x·속도·흔들림·크기·색을 정하고 위에서 아래로 반복해서 떨어진다.
        private void DrawConfetti()
        {
            EnsureBarArt();
            if (starIcon == null && heartIcon == null) return;
            MarkEndScreenShown();
            float elapsed = Time.unscaledTime - endScreenShownAt;
            var previous = GUI.color;

            for (int i = 0; i < ConfettiCount; i++)
            {
                float a = Hash01(i * 3 + 1);
                float b = Hash01(i * 3 + 2);
                float c = Hash01(i * 3 + 3);
                float speed = 90f + 110f * b;               // 초당 픽셀
                float size = 12f + 10f * c;
                float fall = Screen.height + 2f * size;
                float travelled = elapsed * speed - a * fall; // 조각마다 출발이 늦어 처음엔 위에서부터 차례로 쏟아진다
                if (travelled < 0f) continue;
                float y = travelled % fall - size;
                float x = a * Screen.width + Mathf.Sin(elapsed * (1.5f + c) + i) * 18f;

                var icon = i % 2 == 0 ? starIcon : heartIcon;
                if (icon == null) icon = starIcon != null ? starIcon : heartIcon;
                var color = ConfettiColors[i % ConfettiColors.Length];
                color.a = 0.9f;
                GUI.color = color;
                DrawIcon(icon, new Rect(x - size / 2f, y, size, size), 1f);
            }

            GUI.color = previous;
        }

        // 클리어 결과 창: 목표 시간(15분) 대비 걸린 시간과 이번 시드의 방 수 한 줄. 빠르면 초록, 느리면 주황 (Chunks/ClearPace).
        private void DrawClearPace(Rect rect)
        {
            string text = ClearPace.Describe(clearTime, StageEvents.TargetSeconds, StageEvents.MainRooms, StageEvents.SideRooms);
            if (string.IsNullOrEmpty(text)) return;
            var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = ClearPace.IsFast(clearTime, StageEvents.TargetSeconds)
                ? new Color(0.45f, 0.95f, 0.5f)   // 초록: 목표보다 빠름
                : new Color(1f, 0.62f, 0.25f);    // 주황: 목표보다 느림
            GUI.Label(rect, text, style);
        }

        // 결과 창 "누구 탓?" 판정: 마나를 더 날린 쪽 이름 색(A/B), 반반·아무도 없음은 크림색 (Chunks/BlameVerdict).
        private void DrawBlameVerdict(Rect rect)
        {
            float a = mana.WastedBy(ManaUser.PlayerA), b = mana.WastedBy(ManaUser.PlayerB);
            var who = BlameVerdict.Judge(a, b);
            var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.normal.textColor = who == BlameVerdict.Who.A ? ColorA : who == BlameVerdict.Who.B ? ColorB : OutlinedLabel.Cream;
            GUI.Label(rect, BlameVerdict.Describe(a, b), style);
        }

        // 게임오버 결과 창: 쓰러진 곳(주 경로 몇 번째 방·곁방·진행도)과 버틴 시간. 크림색 (Chunks/ClearPace.DescribeDefeat).
        private void DrawDefeatPlace(Rect rect)
        {
            string text = ClearPace.DescribeDefeat(StageEvents.PathStep, StageEvents.MainRooms, StageEvents.InSideRoom, StageEvents.Progress, defeatTime);
            var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = OutlinedLabel.Cream;
            GUI.Label(rect, text, style);
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
