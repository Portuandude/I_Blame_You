using UnityEngine;

namespace IBlameYou.UI
{
    // 오른쪽 위 알림 피드: 줄마다 반투명 둥근 알약 배경 + 왼쪽 색 띠(실패 berry, 성공은 플레이어/항목 색).
    // 새 줄은 오른쪽 화면 밖에서 살짝 튕기며 미끄러져 들어오고, 사라질 때는 흐려진다.
    // 같은 글자의 줄이 아직 떠 있으면 새 줄을 쌓지 않고 그 줄 끝의 "×N"을 올린다 (마나 부족 연타가 피드를 덮지 않게).
    // 위 줄이 사라지면 아래 줄은 부드럽게 올라가고, 실패 줄은 나오거나 수가 오를 때 좌우로 짧게 흔들린다.
    public partial class SkillHUD
    {
        private const float FeedMaxWidth = 540f;
        private const float FeedLineHeight = 26f;
        private const float FeedSlideTime = 0.3f;
        private const float FeedCountPopTime = 0.25f;
        private const float FeedFollowSpeed = 14f;
        private const float FeedShakeTime = 0.3f;
        private static readonly Color FeedBadStripe = new Color(1f, 0.44f, 0.68f); // berry (palette.py)

        private class FeedEntry
        {
            public float Time;     // 처음 나온 때 (미끄러져 들어오기 기준)
            public float LastTime; // 마지막으로 다시 나온 때 (사라지기·×N 톡 기준)
            public string Text;
            public Color Color;
            public bool Bad;       // 실패 알림이면 왼쪽 띠가 berry
            public int Count = 1;
            public float Y = -1f;  // 그리는 높이. 위 줄이 사라지면 제자리(FeedSlotY)로 부드럽게 따라간다
        }

        private GUIStyle feedText;
        private GUIStyle feedCount;
        private GUIStyle feedPill;

        private void AddFeed(string text, Color color, bool bad = false)
        {
            foreach (var entry in feed)
            {
                if (entry.Text != text || entry.Bad != bad) continue;
                entry.Count++;
                entry.LastTime = Time.time;
                return;
            }

            feed.Add(new FeedEntry { Time = Time.time, LastTime = Time.time, Text = text, Color = color, Bad = bad });
            if (feed.Count > MaxFeed) feed.RemoveAt(0);
        }

        private void UpdateFeed()
        {
            feed.RemoveAll(f => Time.time - f.LastTime > FeedLifetime);

            // 위 줄이 빠지면 아래 줄이 뚝 올라가지 않고 미끄러져 올라간다 (일시정지 중에도 멈추지 않게 실제 시간)
            float follow = 1f - Mathf.Exp(-FeedFollowSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < feed.Count; i++)
            {
                float slot = FeedSlotY(i);
                var entry = feed[i];
                entry.Y = entry.Y < 0f ? slot : Mathf.Lerp(entry.Y, slot, follow);
                if (Mathf.Abs(entry.Y - slot) < 0.1f) entry.Y = slot;
            }
        }

        private static float FeedSlotY(int index) => 12f + index * FeedLineHeight;

        private void DrawFeed()
        {
            if (feedText == null)
            {
                feedText = new GUIStyle(label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Clip };
                feedCount = new GUIStyle(feedText) { fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow, richText = false };
                var pillTexture = Resources.Load<Texture2D>("UI/HUD/Pill");
                feedPill = new GUIStyle();
                feedPill.normal.background = pillTexture != null ? pillTexture : SolidTexture(new Color(0.1f, 0.08f, 0.19f, 0.7f));
                if (pillTexture != null) feedPill.border = new RectOffset(11, 11, 0, 0);
            }

            var previous = GUI.color;
            for (int i = 0; i < feed.Count; i++)
            {
                var entry = feed[i];
                float y = entry.Y < 0f ? FeedSlotY(i) : entry.Y; // Update 전에 그려지는 첫 프레임은 제자리
                float age = Time.time - entry.Time;
                float alpha = Mathf.Clamp01((FeedLifetime - (Time.time - entry.LastTime)) / 1.5f);
                var content = new GUIContent(entry.Text);
                float textWidth = feedText.CalcSize(content).x;
                var countContent = entry.Count > 1 ? new GUIContent($"×{entry.Count}") : null;
                float countWidth = countContent != null ? feedCount.CalcSize(countContent).x + 8f : 0f;
                float width = Mathf.Min(FeedMaxWidth, textWidth + countWidth + 34f);
                float slide = 1f - EaseOutBack(Mathf.Clamp01(age / FeedSlideTime));
                float shake = entry.Bad ? FeedShake(Time.time - entry.LastTime) : 0f;
                var pill = new Rect(Screen.width - width - 12f + slide * (width + 24f) + shake, y, width, FeedLineHeight - 2f);

                GUI.color = new Color(1f, 1f, 1f, alpha);
                DrawStyle(feedPill, pill);

                var stripe = entry.Bad ? FeedBadStripe : entry.Color;
                stripe.a = alpha;
                GUI.color = stripe;
                GUI.DrawTexture(new Rect(pill.x + 7f, pill.y + 5f, 4f, pill.height - 10f), Texture2D.whiteTexture);

                var color = entry.Color;
                color.a = alpha;
                GUI.color = color;
                GUI.Label(new Rect(pill.x + 17f, pill.y, pill.width - 26f - countWidth, pill.height), content, feedText);

                if (countContent != null) DrawFeedCount(new Rect(pill.xMax - 9f - countWidth + 8f, pill.y, countWidth - 8f, pill.height), countContent, Time.time - entry.LastTime, alpha);
            }

            GUI.color = previous;
        }

        // 실패 줄이 나오거나 ×N이 오른 직후 0.3초 동안 좌우로 짧게 흔들린다 (점점 약하게)
        private static float FeedShake(float since)
        {
            if (since < 0f || since >= FeedShakeTime) return 0f;
            float fade = 1f - since / FeedShakeTime;
            return Mathf.Sin(since * 70f) * 5f * fade;
        }

        // 줄 끝 금색 "×N". 수가 오른 순간 1.5배로 톡 커졌다가 돌아온다.
        private void DrawFeedCount(Rect rect, GUIContent content, float sinceBump, float alpha)
        {
            var previousMatrix = GUI.matrix;
            float t = Mathf.Clamp01(sinceBump / FeedCountPopTime);
            float scale = 1f + 0.5f * (1f - t) * (1f - t);
            GUIUtility.ScaleAroundPivot(Vector2.one * scale, rect.center);
            var gold = OutlinedLabel.Gold;
            gold.a = alpha;
            GUI.color = gold;
            GUI.Label(rect, content, feedCount);
            GUI.matrix = previousMatrix;
        }

        // 0→1, 끝에서 살짝 넘쳤다가 돌아온다 (튕기는 느낌)
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
