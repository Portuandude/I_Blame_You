using System.Text.RegularExpressions;
using UnityEngine;

namespace IBlameYou.UI
{
    // 화면 가운데 위 안내 문구 (레벨업·구역 돌파·보스 등장 등, StageEvents.PostNotice).
    // 귀엽지만 화려하게: 진한 외곽선 → gold 테두리 → 크림 글자 3겹 (OutlinedLabel.cs) + 나타날 때 살짝 튀어 오르는(팝) 크기 애니메이션.
    // 크기 애니메이션은 GUI.matrix를 글자 가운데 기준으로 잠깐 바꿔서 그린다 (그린 뒤 원래대로).
    public partial class SkillHUD
    {
        private const float NoticePopTime = 0.35f;

        private GUIStyle noticeStyle;
        private string noticePlainSource; // 태그를 뺀 글자를 다시 만들지 않게 원문을 기억
        private string noticePlain;

        private void DrawNotice()
        {
            if (string.IsNullOrEmpty(notice) || Time.time - noticeTime > NoticeDuration) return;

            if (noticeStyle == null)
            {
                noticeStyle = new GUIStyle(big) { fontSize = 30, richText = false };
                noticeStyle.normal.textColor = Color.white;
            }

            if (noticePlainSource != notice)
            {
                noticePlainSource = notice;
                noticePlain = Regex.Replace(notice, "<[^>]*>", ""); // 색 태그가 있어도 테두리는 한 색으로
            }

            float age = Time.time - noticeTime;
            float alpha = Mathf.Clamp01((NoticeDuration - age) / 0.5f);
            var rect = new Rect(0f, Screen.height * 0.16f, Screen.width, 48f);

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            GUIUtility.ScaleAroundPivot(Vector2.one * NoticePopScale(age), rect.center);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            OutlinedLabel.Draw(rect, noticePlain, noticeStyle, OutlinedLabel.Cream, OutlinedLabel.Gold, 2f, 1.5f);

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        // 0.5배에서 시작해 1.15배까지 커졌다가 1배로 돌아온다 (NoticePopTime 동안). 그 뒤는 1배.
        private static float NoticePopScale(float age)
        {
            if (age >= NoticePopTime) return 1f;
            float t = Mathf.Clamp01(age / NoticePopTime);
            if (t < 0.6f) return Mathf.Lerp(0.5f, 1.15f, Mathf.SmoothStep(0f, 1f, t / 0.6f));
            return Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }
    }
}
