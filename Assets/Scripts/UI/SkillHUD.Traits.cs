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
        private int traitSelection;   // 키보드로 고른 칸 (←→·A/D, Enter·Space로 찍기)
        private bool traitKeyboard;   // 화살표를 한 번이라도 눌렀으면 금색 테두리를 그린다

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
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
                {
                    GameAudio.PlaySfx("PauseClose", 0.6f); // 일시정지 창 닫기와 같은 "통" (쓰러짐·클리어로 닫힐 땐 안 냄)
                    CloseTraitWindow();
                }
                else UpdateTraitKeys();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Tab) || relicWindowOpen || equipCardOpen || villageOpen || deathHandled || stageCleared || health.IsDead) return;
            if (control.ChantActive || control.ShieldMinigameActive || control.Locked) return; // 영창/미니게임 중에는 열지 않는다

            traitWindowOpen = true;
            traitWindowOpenedAt = Time.unscaledTime; // 카드 팝 (SkillHUD.Cards.cs), 패널 팝·배경 페이드
            traitSelection = 0;
            traitKeyboard = false;
            GameAudio.PlaySfx("BookOpen", 0.6f); // 책 펼치는 "파라락" (Tools/AudioGen)
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

        // ←→·A/D로 칸 옮기기, Enter·Space로 그 칸 찍기. 첫 화살표는 지금 칸(첫 칸)을 보여 주기만 한다.
        // 찍어도 창은 열린 채라 키가 게임으로 새지 않는다 (닫는 건 Tab·Esc).
        private void UpdateTraitKeys()
        {
            int count = TraitTree.All.Length;
            if (count == 0) return;
            int step = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) step = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) step = 1;
            if (step != 0)
            {
                if (traitKeyboard) traitSelection = ((traitSelection + step) % count + count) % count;
                traitKeyboard = true;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                return;
            }

            if (!traitKeyboard || !(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))) return;
            int i = Mathf.Clamp(traitSelection, 0, count - 1);
            var trait = TraitTree.All[i];
            if (progression.Raise(trait)) OnTraitRaisedInWindow(i, progression.Rank(trait));
            else GameAudio.PlaySfx("Fizzle", 0.5f); // 포인트가 없거나 최대 랭크
        }

        // 창 열림 연출: 배경 어둠이 RelicFadeTime 동안 깔리고 패널이 유물 창과 같은 곡선으로 톡 (SkillHUD.Relics.cs의 상수 공유).
        private float TraitPanelScale()
        {
            float t = (Time.unscaledTime - traitWindowOpenedAt) / RelicPopTime;
            if (t >= 1f || t < 0f) return 1f;
            return t < 0.6f
                ? Mathf.Lerp(0.85f, 1.04f, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }

        // 다음 마일스톤까지 남은 포인트. 다 모았으면 -1.
        private static int PointsToNextStar(int rank)
        {
            foreach (int milestone in TraitTree.Milestones)
                if (rank < milestone) return milestone - rank;
            return -1;
        }

        private const float LevelFlashTime = 0.6f;
        private static readonly Color LevelFillColor = new Color(0.75f, 0.62f, 1f);   // lilac
        private static readonly Color LevelGlowColor = new Color(0.85f, 0.75f, 1f);
        private float levelFlashStart = -10f;

        // 경험치 막대가 따라 차오르는 값. 레벨이 바뀌면 0부터 다시 (처음 그릴 때·이어하기는 바로 그 값).
        private const float XpFillMinSpeed = 0.6f;  // 막대 비율/초 (작은 차이도 끝까지 가게)
        private const float XpFillCatchUp = 6f;     // 남은 차이 × 이 값 /초 (큰 차이는 빠르게)
        private float xpShown;
        private int xpShownLevel = -1;
        private float xpShownTime;

        private float UpdateXpShown(float ratio)
        {
            if (xpShownLevel < 0) { xpShown = ratio; xpShownLevel = progression.Level; xpShownTime = Time.unscaledTime; return xpShown; }
            if (progression.Level != xpShownLevel) { xpShown = 0f; xpShownLevel = progression.Level; }
            if (Event.current.type != EventType.Repaint) return xpShown; // OnGUI는 한 프레임에 여러 번 불린다
            float dt = Mathf.Min(0.1f, Time.unscaledTime - xpShownTime);
            xpShownTime = Time.unscaledTime;
            if (ratio <= xpShown) xpShown = ratio;
            else xpShown = Mathf.MoveTowards(xpShown, ratio, Mathf.Max(XpFillMinSpeed, (ratio - xpShown) * XpFillCatchUp) * dt);
            return xpShown;
        }

        private void DrawLevelBar()
        {
            if (progression == null) return;

            var rect = new Rect(12f, 204f, 360f, 30f);
            GUI.Box(rect, GUIContent.none, panel);
            float ratio = Mathf.Clamp01(progression.Experience / progression.XpToNext);
            float shown = UpdateXpShown(ratio);
            // 경험치 캡슐 바 (lilac). 얻은 만큼 하얀 끝이 먼저 가고 lilac이 따라 차오른다 (2026-11-13).
            // 레벨업 순간 gold로 반짝 빛나며 하얗게 찼다가 돌아온다.
            float flash = 1f - Mathf.Clamp01((Time.unscaledTime - levelFlashStart) / LevelFlashTime);
            var fill = Color.Lerp(LevelFillColor, Color.white, flash * 0.8f);
            var glow = Color.Lerp(LevelGlowColor, new Color(1f, 0.85f, 0.3f), flash);
            DrawCapsuleBar(new Rect(rect.x + 150f, rect.y + 6f, rect.width - 160f - BarGlow, BarHeight),
                Mathf.Max(shown, flash), ratio, fill, glow, null);

            string points = progression.Points > 0 ? $"  <color=#ffd84d>+{progression.Points}</color>" : "";
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, 140f, 24f), $"레벨 {progression.Level}{points}", small);
            DrawTraitPointsPill(new Vector2(rect.x, rect.y + 31f)); // gold 알약 + 숨쉬기 + 팝 (SkillHUD.TraitPoints.cs)
        }

        // 특성 창에서 찍은 순간: 소리 + 그 카드 막대 반짝 + 마일스톤이면 별 팝 (unscaledTime: 창이 열린 동안 게임이 멈춰 있다)
        private const float TraitRaiseFlashTime = 0.35f;
        private const float TraitStarPopTime = 0.5f;
        private float[] traitRaisedAt;
        private bool[] traitRaisedMilestone;

        private void OnTraitRaisedInWindow(int column, int rank)
        {
            if (traitRaisedAt == null) { traitRaisedAt = new float[TraitTree.All.Length]; traitRaisedMilestone = new bool[TraitTree.All.Length]; }
            bool milestone = System.Array.IndexOf(TraitTree.Milestones, rank) >= 0;
            traitRaisedAt[column] = Time.unscaledTime;
            traitRaisedMilestone[column] = milestone;
            if (milestone) GameAudio.PlaySfx("TraitMilestone", 0.7f);
            else GameAudio.PlaySfx("TraitUp", 0.6f);
        }

        // 1(막 찍음) → 0. 찍은 적 없으면 0.
        private float TraitRaiseFlash(int column)
        {
            if (traitRaisedAt == null || traitRaisedAt[column] <= 0f) return 0f;
            return 1f - Mathf.Clamp01((Time.unscaledTime - traitRaisedAt[column]) / TraitRaiseFlashTime);
        }

        // 마일스톤을 막 달성한 카드의 별: 1 → 1.9배 → 1배 (TraitStarPopTime). 그 밖에는 1배.
        private float TraitStarPopScale(int column)
        {
            if (traitRaisedAt == null || !traitRaisedMilestone[column] || traitRaisedAt[column] <= 0f) return 1f;
            float t = (Time.unscaledTime - traitRaisedAt[column]) / TraitStarPopTime;
            if (t < 0f || t >= 1f) return 1f;
            return 1f + 0.9f * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.3f);
        }

        private void DrawTraitWindow()
        {
            if (!traitWindowOpen || progression == null) return;

            const float columnWidth = 200f;
            const float gap = 10f;
            float width = TraitTree.All.Length * columnWidth + (TraitTree.All.Length - 1) * gap + 40f;
            var rect = new Rect((Screen.width - width) / 2f, Screen.height * 0.18f, width, 380f);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((Time.unscaledTime - traitWindowOpenedAt) / RelicFadeTime));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            var windowMatrix = GUI.matrix; // 패널 팝: 카드 팝(BeginCardPop)은 이 위에 겹친다
            float panelScale = TraitPanelScale();
            if (panelScale != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * panelScale, rect.center);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "특성", big);
            // 포인트가 없으면 다음 레벨까지 남은 경험치를 보여 준다
            string pointsText = progression.Points > 0
                ? $"남은 포인트 <color=#ffd84d>{progression.Points}</color>"
                : $"남은 포인트 0 · 다음 레벨까지 경험치 <color=#c8b5ff>{Mathf.Max(1, Mathf.CeilToInt(progression.XpToNext - progression.Experience))}</color>";
            GUI.Label(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 22f),
                $"레벨 {progression.Level} · {pointsText} · A와 B가 함께 쓰는 포인트 · ←→ + Enter 찍기 · Tab/Esc 닫기",
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
                int toStar = PointsToNextStar(rank); // 오른쪽: 다음 별까지 남은 포인트
                GUI.Label(new Rect(column.x + 10f, column.y + 34f, column.width - 20f, 22f),
                    toStar > 0 ? $"<color=#ffd84d>다음 별까지 {toStar}</color>" : "<color=#ffd84d>★ 완성</color>",
                    new GUIStyle(small) { alignment = TextAnchor.MiddleRight, richText = true });

                // 랭크 막대: gold 캡슐 바 (SkillHUD.Bars.cs, 빛 테두리 여백 BarGlow만큼 안쪽으로)
                var bar = new Rect(column.x + 10f + BarGlow, column.y + 58f, column.width - 20f - 2f * BarGlow, BarHeight);
                // 방금 찍은 카드는 막대가 하얗게 반짝였다가 gold로 돌아온다 (2026-11-13 특성 연출)
                float raised = TraitRaiseFlash(i);
                DrawCapsuleBar(bar, (float)rank / TraitTree.MaxRank, 0f, Color.Lerp(new Color(1f, 0.82f, 0.3f), Color.white, raised * 0.85f),
                    Color.Lerp(new Color(1f, 0.9f, 0.55f), Color.white, raised), null);

                GUI.Label(new Rect(column.x + 10f, column.y + 80f, column.width - 20f, 22f), TraitTree.PerPoint(trait), small);

                float y = column.y + 104f;
                foreach (int milestone in TraitTree.Milestones)
                {
                    bool unlocked = rank >= milestone;
                    string color = unlocked ? "#ffd84d" : "#8a8a8a";
                    var starRect = new Rect(column.x + 8f, y, 14f, 14f);
                    var starMatrix = GUI.matrix;
                    if (unlocked && rank == milestone) // 막 닿은 단계의 별은 크게 톡 튀었다 돌아온다
                        GUIUtility.ScaleAroundPivot(Vector2.one * TraitStarPopScale(i), new Vector2(starRect.x + 7f, starRect.y + 8f));
                    DrawMilestoneStar(starRect, unlocked);
                    GUI.matrix = starMatrix;
                    GUI.Label(new Rect(column.x + 24f, y, column.width - 32f, 44f),
                        $"<color={color}>{milestone}: {TraitTree.MilestoneText(trait, milestone)}</color>", small);
                    y += 44f;
                }

                if (traitKeyboard && i == traitSelection) CuteButton.DrawSelection(column); // 키보드로 고른 칸 둘레 금색 테두리
                GUI.enabled = progression.CanRaise(trait);
                string raiseText = progression.Points <= 0 ? "포인트 없음" : rank >= TraitTree.MaxRank ? "최대" : "+ 찍기";
                if (CuteButton.Button(new Rect(column.x + 10f, column.yMax - 36f, column.width - 20f, 28f), raiseText, cardButton))
                {
                    if (progression.Raise(trait)) OnTraitRaisedInWindow(i, progression.Rank(trait));
                }
                GUI.enabled = true;
                GUI.matrix = previousMatrix;
            }

            GUI.matrix = windowMatrix;
        }
    }
}
