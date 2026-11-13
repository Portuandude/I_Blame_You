using System.Text.RegularExpressions;
using UnityEngine;

namespace IBlameYou.UI
{
    // 화면 가운데 위 안내 문구 (레벨업·구역 돌파·보스 등장 등, StageEvents.PostNotice).
    // 귀엽지만 화려하게: 진한 외곽선 → gold 테두리 → 크림 글자 3겹 (OutlinedLabel.cs) + 나타날 때 살짝 튀어 오르는(팝) 크기 애니메이션.
    // 크기 애니메이션은 GUI.matrix를 글자 가운데 기준으로 잠깐 바꿔서 그린다 (그린 뒤 원래대로).
    // 문구 뒤에는 반투명 리본 띠(위아래 금색 선)가 가운데서 양옆으로 펼쳐진다.
    // 문구가 떠 있는 중에 새 문구가 오면 이전 문구는 작아지며 위로 밀려 올라가 흐려진다 (뚝 바뀌지 않게).
    public partial class SkillHUD
    {
        private const float NoticePopTime = 0.35f;
        private const float NoticePushTime = 0.4f;   // 이전 문구가 밀려 올라가 사라지는 시간
        private const float NoticePushRise = 34f;    // 밀려 올라가는 높이
        private const float NoticeRibbonOpen = 0.3f; // 리본 띠가 다 펼쳐지는 시간
        private static readonly Color NoticeRibbonFill = new Color(0.16f, 0.1f, 0.25f, 0.55f); // 진한 자두색 반투명

        private string oldNotice;     // 밀려 올라가는 이전 문구 (태그 뺀 글자)
        private float oldNoticeAlpha; // 밀려나기 시작할 때의 투명도
        private float oldNoticeTime;  // 밀려나기 시작한 때

        private GUIStyle noticeStyle;
        private string noticePlainSource; // 태그를 뺀 글자를 다시 만들지 않게 원문을 기억
        private string noticePlain;

        private void DrawNotice()
        {
            if (string.IsNullOrEmpty(notice) || Time.time - noticeTime > NoticeDuration)
            {
                oldNotice = null;
                return;
            }

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
            var rect = NoticeRect();

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            DrawOldNotice();
            DrawNoticeRibbon(rect, noticeStyle.CalcSize(new GUIContent(noticePlain)).x, age, alpha);
            GUIUtility.ScaleAroundPivot(Vector2.one * NoticePopScale(age), rect.center);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            OutlinedLabel.Draw(rect, noticePlain, noticeStyle, OutlinedLabel.Cream, OutlinedLabel.Gold, 2f, 1.5f);

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private static Rect NoticeRect() => new Rect(0f, Screen.height * 0.16f, Screen.width, 48f);

        // 새 문구가 오기 직전에 부른다. 지금 문구가 아직 보이면 그 글자와 투명도를 기억해 둔다.
        private void PushOldNotice()
        {
            oldNotice = null;
            if (string.IsNullOrEmpty(noticePlain) || noticePlainSource != notice) return; // 한 번도 안 그려진 문구는 밀어낼 것도 없다
            float age = Time.time - noticeTime;
            if (age < 0f || age >= NoticeDuration) return;
            oldNotice = noticePlain;
            oldNoticeAlpha = Mathf.Clamp01((NoticeDuration - age) / 0.5f);
            oldNoticeTime = Time.time;
        }

        // 이전 문구: NoticePushTime 동안 위로 올라가며 0.7배까지 작아지고 흐려진다. 리본 없이 글자만.
        private void DrawOldNotice()
        {
            if (oldNotice == null) return;
            float t = (Time.time - oldNoticeTime) / NoticePushTime;
            if (t < 0f || t >= 1f)
            {
                oldNotice = null;
                return;
            }

            float ease = 1f - (1f - t) * (1f - t);
            var rect = NoticeRect();
            rect.y -= NoticePushRise * ease;
            var previousMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Lerp(1f, 0.7f, ease), rect.center);
            GUI.color = new Color(1f, 1f, 1f, oldNoticeAlpha * (1f - t));
            OutlinedLabel.Draw(rect, oldNotice, noticeStyle, OutlinedLabel.Cream, OutlinedLabel.Gold, 2f, 1.5f);
            GUI.matrix = previousMatrix;
        }

        // 글자 뒤 리본 띠: 가운데서 양옆으로 펼쳐지고(빠르게 시작해 천천히 멈춤), 위아래에 금색 선, 양 끝에 금색 마름모.
        private static void DrawNoticeRibbon(Rect rect, float textWidth, float age, float alpha)
        {
            float open = Mathf.Clamp01(age / NoticeRibbonOpen);
            open = 1f - (1f - open) * (1f - open) * (1f - open);
            float fullWidth = Mathf.Min(Screen.width, textWidth + 120f);
            float width = fullWidth * open;
            if (width < 1f) return;

            const float height = 40f;
            var band = new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
            var fill = NoticeRibbonFill;
            fill.a *= alpha;
            GUI.color = fill;
            GUI.DrawTexture(band, Texture2D.whiteTexture);

            var gold = OutlinedLabel.Gold;
            gold.a = 0.9f * alpha;
            GUI.color = gold;
            GUI.DrawTexture(new Rect(band.x, band.y, band.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(band.x, band.yMax - 2f, band.width, 2f), Texture2D.whiteTexture);

            var previousMatrix = GUI.matrix;
            foreach (float x in new[] { band.x, band.xMax })
            {
                var pivot = new Vector2(x, band.center.y);
                GUIUtility.RotateAroundPivot(45f, pivot);
                GUI.DrawTexture(new Rect(pivot.x - 4f, pivot.y - 4f, 8f, 8f), Texture2D.whiteTexture);
                GUI.matrix = previousMatrix;
            }
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
