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
            GameAudio.PlaySfx("Cast_Wind", 0.5f);
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

            if (!Input.GetKeyDown(KeyCode.Tab) || deathHandled || stageCleared || health.IsDead) return;
            if (control.ChantActive || control.ShieldMinigameActive || control.Locked) return; // 영창/미니게임 중에는 열지 않는다

            traitWindowOpen = true;
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

        private void DrawLevelBar()
        {
            if (progression == null) return;

            var rect = new Rect(12f, 204f, 360f, 30f);
            GUI.Box(rect, GUIContent.none, panel);
            float ratio = Mathf.Clamp01(progression.Experience / progression.XpToNext);
            var back = new Rect(rect.x + 150f, rect.y + 10f, rect.width - 162f, 10f);
            GUI.DrawTexture(back, Texture2D.grayTexture);
            var previous = GUI.color;
            GUI.color = new Color(0.55f, 0.9f, 0.45f);
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * ratio, back.height), Texture2D.whiteTexture);
            GUI.color = previous;

            string points = progression.Points > 0 ? $"  <color=#ffd84d>+{progression.Points}</color>" : "";
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, 140f, 24f), $"레벨 {progression.Level}{points}", small);
            if (progression.Points > 0)
                GUI.Label(new Rect(rect.x, rect.y + 30f, rect.width, 20f), $"<color=#ffd84d>특성 포인트 {progression.Points} (Tab)</color>", small);
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
                GUI.Box(column, GUIContent.none, panel);

                string owner = TraitTree.Owner(trait);
                string ownerColor = owner == "A" ? "#73d9ff" : owner == "B" ? "#ffa659" : "#dddddd";
                GUI.Label(new Rect(column.x + 10f, column.y + 8f, column.width - 20f, 26f),
                    $"<b>{TraitTree.Name(trait)}</b>  <color={ownerColor}>{owner}</color>", label);
                GUI.Label(new Rect(column.x + 10f, column.y + 34f, column.width - 20f, 22f), $"{rank} / {TraitTree.MaxRank}", label);

                var bar = new Rect(column.x + 10f, column.y + 60f, column.width - 20f, 8f);
                GUI.DrawTexture(bar, Texture2D.grayTexture);
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * rank / TraitTree.MaxRank, bar.height), Texture2D.whiteTexture);
                GUI.color = previous;

                GUI.Label(new Rect(column.x + 10f, column.y + 74f, column.width - 20f, 22f), TraitTree.PerPoint(trait), small);

                float y = column.y + 104f;
                foreach (int milestone in TraitTree.Milestones)
                {
                    bool unlocked = rank >= milestone;
                    string color = unlocked ? "#ffd84d" : "#8a8a8a";
                    GUI.Label(new Rect(column.x + 10f, y, column.width - 20f, 44f),
                        $"<color={color}>{milestone}: {TraitTree.MilestoneText(trait, milestone)}</color>", small);
                    y += 44f;
                }

                GUI.enabled = progression.CanRaise(trait);
                if (GUI.Button(new Rect(column.x + 10f, column.yMax - 36f, column.width - 20f, 28f), "+ 찍기"))
                    progression.Raise(trait);
                GUI.enabled = true;
            }
        }
    }
}
