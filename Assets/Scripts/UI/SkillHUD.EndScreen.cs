using IBlameYou.Core;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 게임오버·클리어 화면 연출 (귀엽지만 화려하게).
    // - 패널이 0.6배에서 튀어 올라 1배로 (처음 그려진 순간부터 0.35초)
    // - 제목은 OutlinedLabel 테두리 글자. 게임오버 "쓰러졌다!"는 좌우로 갸우뚱 (±4도)
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
        private int defeatUsualRooms = -1; // 이번 판 전 최근 쓰러진 판들이 간 가장 먼 방 거리 평균 (없으면 -1)
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

        // ── 결과 창 본문 (게임오버·클리어 공용) ──
        // 줄 목록을 위에서 아래로 쌓는다: 있는 줄만 높이를 차지한다 (예전처럼 고정 좌표에 끼워 넣지 않아 줄을 더해도 겹치지 않는다).
        // 줄마다 EndRowGap 간격으로 차례로 나타난다 (살짝 아래에서 떠오르며 밝아짐, RowReveal). 제목·패널은 팝 등장과 함께 바로 보인다.
        private const float EndRowStart = 0.15f; // 창이 처음 그려지고 첫 줄이 나타나기 시작할 때까지
        private const float EndRowGap = 0.09f;   // 줄 사이 간격
        private const float EndRowFade = 0.22f;  // 한 줄이 다 나타나는 데 걸리는 시간
        private const float EndRowLift = 8f;     // 나타나기 시작할 때 아래로 내려가 있는 픽셀
        private const float EndButtonHeight = 32f;
        private const float EndButtonWidth = 170f;
        private int endButtonsRow = int.MaxValue; // 버튼 줄의 순번 (그려 봐야 안다, 그 전에는 키를 안 받는다)
        private int endSelection;                 // 키보드로 고른 버튼: 0 다시 시작, 1 시작 화면
        private bool restartRequested;            // 다시 시작은 한 번만 (같은 프레임에 R과 버튼이 겹쳐도)
        private GUIStyle endButton;

        // index번째 줄이 얼마나 나타났는지 0 ~ 1.
        private float RowReveal(int index)
        {
            if (endScreenShownAt < 0f) return 0f;
            return Mathf.Clamp01((Time.unscaledTime - endScreenShownAt - EndRowStart - index * EndRowGap) / EndRowFade);
        }

        // 줄을 그리기 전에: GUI.color 알파를 나타난 만큼 줄이고, 덜 나타난 만큼 아래로 내릴 픽셀을 돌려준다. 그린 뒤 previous를 되돌린다.
        private float BeginRow(int index, out Color previous)
        {
            previous = GUI.color;
            float k = RowReveal(index);
            var color = previous;
            color.a *= k;
            GUI.color = color;
            return (1f - Mathf.SmoothStep(0f, 1f, k)) * EndRowLift;
        }

        private void DrawEndPanel(bool clear, float width)
        {
            bool compare = !clear && HasDefeatCompare;
            string explore = ExploreText(true);           // 방 N/M 탐험 · 동전 무더기 a/b · 마을 (SkillHUD.Pause.cs)
            bool favorite = !string.IsNullOrEmpty(FavoriteLine());
            const float top = 60f, line = 26f, summary = 28f, small22 = 22f, relics = 30f;
            float height = top + line * 2f + (compare ? 24f : 0f) + summary + (explore.Length > 0 ? small22 : 0f)
                           + (favorite ? small22 : 0f) + relics + EndButtonHeight + 16f;
            float y0 = Mathf.Max(8f, Mathf.Min(Screen.height * 0.35f, Screen.height - height - 12f));
            var rect = new Rect((Screen.width - width) / 2f, y0, width, height);

            var previousMatrix = BeginEndScreenPop(rect); // 팝 등장
            GUI.Box(rect, GUIContent.none, panel);
            DrawEndTitle(new Rect(rect.x, rect.y + 14f, rect.width, 40f), clear ? $"{StageEvents.StageName} 클리어!" : "쓰러졌다!", !clear);

            if (clear && newBestTime) DrawBestStamp(new Rect(rect.x + 14f, rect.y + 16f, 132f, 34f)); // 최고 기록! 도장

            var center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            float y = rect.y + top;
            int row = 0;
            Color previous;

            // 골드(오른쪽 위)는 첫 줄과 같이 나타난다
            float lift = BeginRow(row, out previous);
            float count = UpdateEndCount();
            DrawEndGold(rect, Mathf.RoundToInt(RunStats.Gold * count), EndGoldPop()); // 이번 판 골드, 올라가며 세기 (SkillHUD.Gold.cs)
            string manaText = $"A가 날린 마나 {mana.WastedBy(ManaUser.PlayerA) * count:0} · B가 날린 마나 {mana.WastedBy(ManaUser.PlayerB) * count:0}";
            if (clear)
            {
                // 최고 기록이면 괄호 대신 제목 왼쪽 금색 도장 (DrawBestStamp)
                string best = newBestTime ? "" : $" (최고 {SaveData.FormatTime(SaveData.BestClearTime(GameSession.DevMode))})";
                manaText = $"걸린 시간 {SaveData.FormatTime(clearTime)}{best} · " + manaText;
            }
            GUI.Label(new Rect(rect.x, y + lift, rect.width, line), manaText, center);
            GUI.color = previous;
            y += line; row++;

            lift = BeginRow(row, out previous);
            if (clear) DrawClearPace(new Rect(rect.x, y + lift, rect.width, line)); // 목표 시간 비교
            else DrawDefeatPlace(new Rect(rect.x, y + lift, rect.width, line));   // 쓰러진 곳
            GUI.color = previous;
            y += line; row++;

            if (compare)
            {
                lift = BeginRow(row, out previous);
                DrawDefeatCompare(new Rect(rect.x, y + lift, rect.width, 24f)); // 평소보다 더/덜 갔다
                GUI.color = previous;
                y += 24f; row++;
            }

            lift = BeginRow(row, out previous);
            GUI.Label(new Rect(rect.x, y + lift, rect.width, summary), RunStats.Summary(RunLevel, RelicCount), center);
            GUI.color = previous;
            y += summary; row++;

            if (explore.Length > 0)
            {
                lift = BeginRow(row, out previous);
                var exploreStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = false, clipping = TextClipping.Overflow };
                exploreStyle.normal.textColor = OutlinedLabel.Cream;
                GUI.Label(new Rect(rect.x, y + lift, rect.width, small22), explore, exploreStyle);
                GUI.color = previous;
                y += small22; row++;
            }

            if (favorite)
            {
                lift = BeginRow(row, out previous);
                DrawFavoriteLine(rect.x, y + lift, rect.width); // B 최애 마법 · A 최애 방어막 · 새 유물
                GUI.color = previous;
                y += small22; row++;
            }

            lift = BeginRow(row, out previous);
            DrawEndRelicsAndEquipment(new Rect(rect.x + 12f, y + 2f + lift, rect.width - 24f, 26f)); // 유물 아이콘 줄 + 장비 3칸
            GUI.color = previous;
            y += relics; row++;

            endButtonsRow = row;
            lift = BeginRow(row, out previous);
            DrawEndButtons(new Rect(rect.x, y + 4f + lift, rect.width, EndButtonHeight), clear);
            GUI.color = previous;

            GUI.matrix = previousMatrix;
        }

        // 숫자 세기: 골드와 A/B가 날린 마나가 첫 줄이 나타날 때부터 EndCountTime 동안 0에서 올라간다 (끝으로 갈수록 느려짐).
        // 다 센 순간 골드가 톡 커졌다 돌아오고 (EndGoldPopTime) 골드가 있으면 동전 소리. 그 뒤로는 실제 값을 그대로 보여 준다
        // (클리어 뒤 날아오는 보스 골드도 들어오는 대로 오른다).
        private const float EndCountTime = 0.7f;
        private const float EndGoldPopTime = 0.25f;
        private float endCountDoneAt = -1f;

        // 지금 세기 비율 0 ~ 1. 다 센 첫 순간에 소리를 내고 시각을 적는다.
        private float UpdateEndCount()
        {
            if (endScreenShownAt < 0f) return 0f;
            float t = Mathf.Clamp01((Time.unscaledTime - endScreenShownAt - EndRowStart) / EndCountTime);
            if (t >= 1f && endCountDoneAt < 0f)
            {
                endCountDoneAt = Time.unscaledTime;
                if (RunStats.Gold > 0) GameAudio.PlaySfx("Coin", 0.8f, 0f);
            }
            return 1f - (1f - t) * (1f - t); // 빨리 오르다 천천히 멈춘다
        }

        // 다 센 순간 골드 줄 배율 1 → 1.25 → 1.
        private float EndGoldPop()
        {
            if (endCountDoneAt < 0f) return 1f;
            float k = (Time.unscaledTime - endCountDoneAt) / EndGoldPopTime;
            if (k >= 1f) return 1f;
            return 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
        }

        // 버튼 줄이 다 나타났고 (게임오버면 페이드도 끝났고) 키·클릭을 받을 수 있는지.
        private bool EndButtonsReady =>
            endScreenShownAt >= 0f && RowReveal(endButtonsRow) >= 1f && (stageCleared || GameOverReady);

        // 결과 창 버튼 2개: 다시 시작 (R) · 시작 화면 (M). 키보드로 고른 쪽은 금색 테두리 (일시정지 창·시작 화면과 같은 CuteButton.DrawSelection).
        private void DrawEndButtons(Rect row, bool clear)
        {
            if (endButton == null) endButton = CuteButton.Create(16);
            const float gap = 24f;
            float x = row.center.x - EndButtonWidth - gap / 2f;
            var left = new Rect(x, row.y, EndButtonWidth, row.height);
            var right = new Rect(x + EndButtonWidth + gap, row.y, EndButtonWidth, row.height);

            bool ready = EndButtonsReady;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = ready;
            if (CuteButton.Button(left, clear ? "새 맵으로 다시 (R)" : "다시 시작 (R)", endButton)) RequestRestart(false);
            if (CuteButton.Button(right, "시작 화면 (M)", endButton)) RequestRestart(true);
            GUI.enabled = wasEnabled;
            if (ready) CuteButton.DrawSelection(endSelection == 0 ? left : right);
        }

        // Update에서 R·M 다음에 부른다: ←→·A/D로 고르고 Enter·Space로 누른다. 버튼 줄이 다 나타나기 전에는 받지 않는다
        // (쓰러지거나 클리어하는 순간 누르고 있던 Space·Enter가 바로 다시 시작으로 새지 않게 — GetKeyDown이라 새로 누른 것만).
        private void UpdateEndButtonsInput()
        {
            if (!EndButtonsReady || restartRequested) return;
            int pick = -1;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) pick = 0;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) pick = 1;
            if (pick >= 0)
            {
                if (pick != endSelection) GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                endSelection = pick;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                GameAudio.PlaySfx("UiClick", 0.7f, 0.04f); // 마우스로 누를 때와 같은 소리
                RequestRestart(endSelection == 1);
            }
        }

        private void RequestRestart(bool backToMenu)
        {
            if (restartRequested) return;
            restartRequested = true;
            restart?.Invoke(backToMenu);
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

        // 결과 창 도장 연출 시간: 크게 떨어져 내려오는 시간 (1.8배 → 1배).
        private const float StampDrop = 0.18f;

        // 클리어 최고 기록 도장: 창이 뜨고 조금 뒤 제목 왼쪽에 금색 "최고 기록!"이 비스듬히(-10도) 쾅.
        // 1.8배 → 1배로 떨어지고, 착지 때 RelicNew 반짝 소리, 흰색에서 금색으로.
        private const float BestStampDelay = 0.8f;
        private bool bestStamped;
        private GUIStyle bestStampStyle;

        private void DrawBestStamp(Rect rect)
        {
            if (endScreenShownAt < 0f) return;
            float t = Time.unscaledTime - endScreenShownAt - BestStampDelay;
            if (t < 0f) return;
            if (bestStampStyle == null)
            {
                bestStampStyle = new GUIStyle(big) { fontSize = 22, richText = false };
                bestStampStyle.normal.textColor = Color.white;
            }

            var previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(-10f, rect.center);
            var color = OutlinedLabel.Gold;
            if (t < StampDrop)
            {
                float k = t / StampDrop;
                GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Lerp(1.8f, 1f, k * k), rect.center);
                color = Color.white;
            }
            else
            {
                if (!bestStamped)
                {
                    bestStamped = true;
                    GameAudio.PlaySfx("RelicNew", 0.7f, 0f);
                }
                color = Color.Lerp(Color.white, OutlinedLabel.Gold, Mathf.Clamp01((t - StampDrop) / 0.35f));
            }
            OutlinedLabel.Draw(rect, "최고 기록!", bestStampStyle, color, OutlinedLabel.Outline, 0f, 2f); // GUI.color 알파는 Draw가 곱한다
            GUI.matrix = previous;
        }

        // 게임오버 결과 창: 쓰러진 곳(시작 방에서 몇 칸·곁방·가장 멀리 간 거리)과 버틴 시간. 크림색 (Chunks/ClearPace.DescribeDefeat).
        private void DrawDefeatPlace(Rect rect)
        {
            string text = ClearPace.DescribeDefeat(StageEvents.RoomDistance, StageEvents.FarthestDistance, StageEvents.InSideRoom, defeatTime);
            var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = OutlinedLabel.Cream;
            GUI.Label(rect, text, style);
        }

        // 게임오버 결과 창: 이번 판 가장 멀리 간 방 거리를 최근 쓰러진 판 평균과 비교. 더 갔으면 초록, 덜 갔으면 주황, 같으면 크림색 (Chunks/ClearPace.CompareDefeat).
        private bool HasDefeatCompare => defeatUsualRooms >= 0 && StageEvents.FarthestDistance >= 0;

        private void DrawDefeatCompare(Rect rect)
        {
            if (!HasDefeatCompare) return;
            int gap = ClearPace.DefeatGap(StageEvents.FarthestDistance, defeatUsualRooms);
            var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = gap > 0 ? new Color(0.45f, 0.95f, 0.5f)   // 초록: 평소보다 더 갔다
                : gap < 0 ? new Color(1f, 0.62f, 0.25f)                          // 주황: 덜 갔다
                : OutlinedLabel.Cream;
            GUI.Label(rect, ClearPace.CompareDefeat(StageEvents.FarthestDistance, defeatUsualRooms), style);
        }

        // 결과 창 "최애" 한 줄: B 공격 마법·A 방어막 중 가장 많이 쓴 속성 (Chunks/RunTally) + 이번 판 새 도감 유물 수. 없으면 빈 문자열.
        private static string[] elementNames;
        private static string[] elementColors;   // 속성 이름만 그 속성 색으로 (rich text)

        private static string FavoriteLine()
        {
            if (elementNames == null)
            {
                elementNames = new string[ElementInfo.Count];
                elementColors = new string[ElementInfo.Count];
                for (int i = 0; i < elementNames.Length; i++)
                {
                    elementNames[i] = ElementInfo.KoreanName((Element)i);
                    elementColors[i] = ColorUtility.ToHtmlStringRGB(ElementInfo.Color((Element)i));
                }
            }
            string line = RunTally.Line(IBlameYou.Core.RunStats.SpellCasts, IBlameYou.Core.RunStats.ShieldCasts, elementNames, elementColors);
            int fresh = IBlameYou.Core.RunStats.NewRelics; // 이번 판에 처음 도감에 오른 유물
            if (fresh <= 0) return line;
            // 이번 판에 도감을 다 채웠으면 "새 유물 N개" 대신 금색 완성 문구 (한 줄 폭을 아낀다)
            string news = IBlameYou.Core.RunStats.CodexCompleted
                ? "<color=#ffd94d>★ 유물 도감 완성! ★</color>"
                : $"<color=#7dff7a>새 유물 {fresh}개 도감 등록!</color>";
            return line.Length == 0 ? news : line + " · " + news;
        }

        // 최애 줄을 그리고, 그린 높이(없으면 0)를 돌려준다. 아래 줄은 그만큼 내린다.
        private float DrawFavoriteLine(float x, float y, float width)
        {
            string text = FavoriteLine();
            if (string.IsNullOrEmpty(text)) return 0f;
            // 한 줄 높이(22)라 접히면 아랫줄이 잘린다: 줄바꿈 없이 그린다 (리뷰 LOW)
            var style = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = false, clipping = TextClipping.Overflow };
            style.normal.textColor = OutlinedLabel.Cream;
            GUI.Label(new Rect(x, y, width, 22f), text, style);
            return 22f;
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
