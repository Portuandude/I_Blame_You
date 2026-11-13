using IBlameYou.Core;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 레벨 / 경험치 막대와 Tab으로 여는 특성 창 (던그리드식).
    // 창이 열려 있는 동안은 게임이 멈춘다. 포인트는 A와 B가 함께 쓰므로 누가 찍든 같은 주머니에서 나간다.
    public partial class SkillHUD
    {
        private bool traitWindowOpen;

        private void OnLeveledUp(int level)
        {
            OnStageNotice($"레벨 {level}! 특성 포인트 +1 (Tab)");
            levelFlashStart = Time.unscaledTime;
            SaveData.RecordLevel(level);
            GameAudio.PlaySfx("LevelUp", 0.6f);
        }

        private void OnTraitRaised(Trait trait, int rank)
        {
            string text = $"특성: {TraitTree.Name(trait)} {rank} ({TraitTree.Owner(trait)} 특성)";
            if (System.Array.IndexOf(TraitTree.Milestones, rank) >= 0) text += $" · {TraitTree.MilestoneText(trait, rank)}";
            feed.Add(new FeedEntry { Time = Time.time, Text = text, Color = new Color(1f, 0.85f, 0.4f) });
            if (feed.Count > MaxFeed) feed.RemoveAt(0);
        }

        private void UpdateTraitWindowInput()
        {
            if (progression == null) return;

            if (traitWindowOpen)
            {
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape)) CloseTraitWindow();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Tab) || relicWindowOpen || equipCardOpen || villageOpen || deathHandled || stageCleared || health.IsDead) return;
            if (control.ChantActive || control.ShieldMinigameActive || control.Locked) return; // 영창/미니게임 중에는 열지 않는다

            traitWindowOpen = true;
            traitWindowOpenedAt = Time.unscaledTime; // 카드 팝 (SkillHUD.Cards.cs)
            control.Locked = true;
            Time.timeScale = 0f;
        }

        private void CloseTraitWindow()
        {
            if (!traitWindowOpen) return;
            traitWindowOpen = false;
            Time.timeScale = 1f;
            if (!deathHandled && !stageCleared) control.Locked = false;
        }

        private const float LevelFlashTime = 0.6f;
        private static readonly Color LevelFillColor = new Color(0.75f, 0.62f, 1f);   // lilac
        private static readonly Color LevelGlowColor = new Color(0.85f, 0.75f, 1f);
        private float levelFlashStart = -10f;

        private void DrawLevelBar()
        {
            if (progression == null) return;

            var rect = new Rect(12f, 204f, 360f, 30f);
            GUI.Box(rect, GUIContent.none, panel);
            float ratio = Mathf.Clamp01(progression.Experience / progression.XpToNext);
            // 경험치 캡슐 바 (lilac). 레벨업 순간 gold로 반짝 빛나며 하얗게 찼다가 돌아온다.
            float flash = 1f - Mathf.Clamp01((Time.unscaledTime - levelFlashStart) / LevelFlashTime);
            var fill = Color.Lerp(LevelFillColor, Color.white, flash * 0.8f);
            var glow = Color.Lerp(LevelGlowColor, new Color(1f, 0.85f, 0.3f), flash);
            DrawCapsuleBar(new Rect(rect.x + 150f, rect.y + 6f, rect.width - 160f - BarGlow, BarHeight),
                Mathf.Max(ratio, flash), 0f, fill, glow, null);

            string points = progression.Points > 0 ? $"  <color=#ffd84d>+{progression.Points}</color>" : "";
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, 140f, 24f), $"레벨 {progression.Level}{points}", small);
            DrawTraitPointsPill(new Vector2(rect.x, rect.y + 31f)); // gold 알약 + 숨쉬기 + 팝 (SkillHUD.TraitPoints.cs)
        }

        private void DrawTraitWindow()
        {
            if (!traitWindowOpen || progression == null) return;

            const float columnWidth = 200f;
            const float gap = 10f;
            float width = TraitTree.All.Length * columnWidth + (TraitTree.All.Length - 1) * gap + 40f;
            var rect = new Rect((Screen.width - width) / 2f, Screen.height * 0.18f, width, 380f);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "특성", big);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 22f),
                $"레벨 {progression.Level} · 남은 포인트 <color=#ffd84d>{progression.Points}</color> · A와 B가 함께 쓰는 포인트 · Tab/Esc 닫기",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            for (int i = 0; i < TraitTree.All.Length; i++)
            {
                var trait = TraitTree.All[i];
                int rank = progression.Rank(trait);
                var column = new Rect(rect.x + 20f + i * (columnWidth + gap), rect.y + 80f, columnWidth, rect.height - 100f);
                var previousMatrix = BeginCardPop(column, traitWindowOpenedAt, i);
                string owner = TraitTree.Owner(trait);
                DrawCard(column, owner);

                string ownerColor = owner == "A" ? "#73d9ff" : owner == "B" ? "#ffa659" : "#dddddd";
                GUI.Label(new Rect(column.x + 10f, column.y + 8f, column.width - 20f, 26f),
                    $"<b>{TraitTree.Name(trait)}</b>  <color={ownerColor}>{owner}</color>", label);
                GUI.Label(new Rect(column.x + 10f, column.y + 34f, column.width - 20f, 22f), $"{rank} / {TraitTree.MaxRank}", label);

                // 랭크 막대: gold 캡슐 바 (SkillHUD.Bars.cs, 빛 테두리 여백 BarGlow만큼 안쪽으로)
                var bar = new Rect(column.x + 10f + BarGlow, column.y + 58f, column.width - 20f - 2f * BarGlow, BarHeight);
                DrawCapsuleBar(bar, (float)rank / TraitTree.MaxRank, 0f, new Color(1f, 0.82f, 0.3f), new Color(1f, 0.9f, 0.55f), null);

                GUI.Label(new Rect(column.x + 10f, column.y + 80f, column.width - 20f, 22f), TraitTree.PerPoint(trait), small);

                float y = column.y + 104f;
                foreach (int milestone in TraitTree.Milestones)
                {
                    bool unlocked = rank >= milestone;
                    string color = unlocked ? "#ffd84d" : "#8a8a8a";
                    DrawMilestoneStar(new Rect(column.x + 8f, y, 14f, 14f), unlocked);
                    GUI.Label(new Rect(column.x + 24f, y, column.width - 32f, 44f),
                        $"<color={color}>{milestone}: {TraitTree.MilestoneText(trait, milestone)}</color>", small);
                    y += 44f;
                }

                GUI.enabled = progression.CanRaise(trait);
                if (CuteButton.Button(new Rect(column.x + 10f, column.yMax - 36f, column.width - 20f, 28f), "+ 찍기", cardButton))
                    progression.Raise(trait);
                GUI.enabled = true;
                GUI.matrix = previousMatrix;
            }
        }
    }
}
