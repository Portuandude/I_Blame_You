using IBlameYou.Core;
using UnityEngine;

namespace IBlameYou.UI
{
    // 골드 표시 (Core/RunGold.cs): 스테이지 막대 오른쪽에 금화 아이콘 + 숫자 알약. 숫자는 아직 날아오는 동전 몫을 빼고 보여 주고,
    // 동전이 닿을 때마다 1.25배로 톡 튀었다가 돌아온다 (unscaledTime). 결과 창 오른쪽 위에도 이번 판 골드를 적는다.
    // 마을에서 골드를 쓰면 알약 아래에 빨간 "-N"이 떠올라 사라지고 숫자가 잠깐 붉어진다 (가게 창이 열려 있어 시간이 멈춰도 보이게 unscaledTime).
    public partial class SkillHUD
    {
        private const float GoldPopTime = 0.25f;
        private static readonly Rect GoldPillRect = new Rect(150f, 168f, 120f, 30f); // 스테이지 이름 꼬리표 오른쪽 (진행도 막대를 뺀 자리, 위쪽 가운데는 보스 막대 자리)

        private Texture2D coinIcon;
        private bool coinIconLoaded;
        private float goldPopStart = -10f;
        private GUIStyle goldText;

        private const float GoldSpendTime = 1.1f;   // "-N"이 떠올라 사라지는 시간
        private const float GoldSpendMerge = 0.5f;  // 이 안에 또 쓰면 "-N"을 합쳐 다시 띄운다 (연달아 사기)
        private static readonly Color GoldSpendRed = new Color(1f, 0.42f, 0.42f);
        private float goldSpendStart = -10f;
        private int goldSpendAmount;
        private GUIStyle goldSpendText;

        // 최고 골드 기록: 판이 끝날 때(클리어·쓰러짐) 이번 판 전 최고를 기억하고, 끝난 뒤에도 보스 골드처럼 늘면 다시 기록한다.
        private int bestGoldBefore = -1;   // 이번 판 전 최고 (-1 = 아직 판이 안 끝남)
        private int goldRecorded = -1;     // 마지막으로 기록한 모은 골드

        private void RecordBestGold()
        {
            if (bestGoldBefore < 0) bestGoldBefore = SaveData.BestGold(GameSession.DevMode);
            if (RunStats.GoldEarned <= goldRecorded) return;
            goldRecorded = RunStats.GoldEarned;
            SaveData.RecordGold(goldRecorded);
        }

        private bool NewBestGold => !GameSession.FeatureTest && bestGoldBefore >= 0 && RunStats.GoldEarned > bestGoldBefore;

        private void OnGoldArrived(int value) => goldPopStart = Time.unscaledTime;

        private void OnGoldSpent(int amount)
        {
            float now = Time.unscaledTime;
            goldSpendAmount = now - goldSpendStart < GoldSpendMerge ? goldSpendAmount + amount : amount;
            goldSpendStart = now;
        }

        private void EnsureGoldArt()
        {
            if (!coinIconLoaded)
            {
                coinIconLoaded = true;
                coinIcon = Resources.Load<Texture2D>("Effects/Coin/00");
            }
            if (goldText == null)
                goldText = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        }

        private void DrawGoldPill()
        {
            EnsureGoldArt();
            var rect = GoldPillRect;
            GUI.Box(rect, GUIContent.none, panel);
            float t = Mathf.Clamp01((Time.unscaledTime - goldPopStart) / GoldPopTime);
            float pop = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
            var icon = new Rect(rect.x + 6f, rect.y + 3f, 24f, 24f);
            if (coinIcon != null) GUI.DrawTexture(ScaleAround(icon, pop), coinIcon, ScaleMode.ScaleToFit, true);
            int shown = Mathf.Max(0, RunStats.Gold - RunGold.InFlight);
            var previous = GUI.matrix;
            var textRect = new Rect(rect.x + 36f, rect.y, rect.width - 40f, rect.height);
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(textRect.x, textRect.center.y));
            float spendAge = Time.unscaledTime - goldSpendStart;
            bool spending = spendAge >= 0f && spendAge < GoldSpendTime;
            // 쓴 직전: 숫자를 붉게 했다가 시간이 지나며 금색으로 돌아온다
            var numberColor = spending ? Color.Lerp(GoldSpendRed, OutlinedLabel.Gold, spendAge / GoldSpendTime) : OutlinedLabel.Gold;
            OutlinedLabel.Draw(textRect, shown.ToString(), goldText, numberColor, OutlinedLabel.Outline, 0f, 1.5f);
            GUI.matrix = previous;
            if (spending) DrawGoldSpend(rect, spendAge / GoldSpendTime);
        }

        // 알약 아래에서 위로 18px 떠오르며 흐려지는 빨간 "-N"
        private void DrawGoldSpend(Rect pill, float t)
        {
            if (goldSpendText == null)
                goldSpendText = new GUIStyle(goldText) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            float rise = 18f * t;
            var textRect = new Rect(pill.x, pill.yMax + 2f - rise, pill.width, 22f);
            var color = GoldSpendRed;
            color.a = 1f - t * t;
            var outline = OutlinedLabel.Outline;
            outline.a *= color.a;
            OutlinedLabel.Draw(textRect, $"-{goldSpendAmount}", goldSpendText, color, outline, 0f, 1.5f);
        }

        // 결과 창 오른쪽 위: 금화 + "골드 N". shown은 올라가며 세는 중인 값, pop은 다 센 순간 톡 커지는 배율 (SkillHUD.EndScreen.cs)
        private void DrawEndGold(Rect panelRect, int shown, float pop)
        {
            EnsureGoldArt();
            var row = new Rect(panelRect.xMax - 150f, panelRect.y + 10f, 140f, 28f);
            var previousMatrix = GUI.matrix;
            if (pop > 1f) GUIUtility.ScaleAroundPivot(Vector2.one * pop, new Vector2(row.x + 40f, row.center.y));
            if (coinIcon != null) GUI.DrawTexture(new Rect(row.x, row.y + 2f, 24f, 24f), coinIcon, ScaleMode.ScaleToFit, true);
            OutlinedLabel.Draw(new Rect(row.x + 28f, row.y, row.width - 28f, row.height), $"골드 {shown}", goldText,
                OutlinedLabel.Gold, OutlinedLabel.Outline, 0f, 1.5f);
            GUI.matrix = previousMatrix;
            RecordBestGold(); // 판이 끝난 뒤 들어온 골드(보스)까지 기록
            // 마을에서 골드를 썼으면 아래에 작게 "모음 N · 씀 M" (안 썼으면 남은 골드 = 모은 골드라 생략), 최고 기록이면 금색 "최고 골드!"
            bool best = NewBestGold;
            string line = RunStats.GoldSpent > 0 ? $"모음 {RunStats.GoldEarned} · 씀 {RunStats.GoldSpent}" : "";
            if (best) line = line.Length > 0 ? line + " · 최고 골드!" : "최고 골드!";
            if (line.Length > 0)
            {
                var style = new GUIStyle(small) { alignment = TextAnchor.MiddleRight, wordWrap = false, clipping = TextClipping.Overflow };
                style.normal.textColor = best ? OutlinedLabel.Gold : OutlinedLabel.Cream;
                GUI.Label(new Rect(row.x - 80f, row.yMax, row.width + 80f, 20f), line, style);
            }
        }

        private static Rect ScaleAround(Rect r, float s) =>
            new Rect(r.center.x - r.width * s / 2f, r.center.y - r.height * s / 2f, r.width * s, r.height * s);
    }
}
