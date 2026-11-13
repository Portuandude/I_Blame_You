using System;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 유물 고르기 창: 정예 구역의 유물 상자(RelicChest)에 닿으면 열린다. 후보 3개(네잎 클로버면 4개) 중 하나를 숫자 키나 클릭으로 고른다.
    // 열려 있는 동안 게임이 멈추고 조작이 잠긴다 (특성 창과 같은 방식). 유물은 A/B 공용이라 누가 고르든 둘 다 갖는다.
    // 영창·방어막 미니게임·특성 창·일시정지 중에는 열지 않고, 상자가 다시 물어 올 때까지 기다린다.
    public partial class SkillHUD
    {
        private bool relicWindowOpen;
        private RelicId[] relicOffer;
        private Action<RelicId> relicChosen;
        private Action relicCancelled;
        private int relicOpenedFrame;
        private long relicCodexAtOpen; // 창을 연 때의 유물 도감 (처음 보는 유물 카드에 NEW)
        private int relicUnlockFrame = -1; // 고른 키가 같은 프레임에 스킬 입력으로 새지 않게, 이 프레임부터 조작 잠금을 푼다
        private int relicSelection;        // 키보드로 고른 카드 (←→·A/D, Enter·Space)
        private bool relicKeyboard;        // 화살표를 한 번이라도 눌렀으면 금색 테두리를 그린다
        private int relicColumns = 1;      // 그린 카드 열 수 (2×2일 때 ↑↓가 한 줄씩)

        private PlayerRelics relics;

        private int RelicCount => relics != null ? relics.Owned.Count : 0;

        private void InitializeRelics()
        {
            relics = health != null ? health.GetComponent<PlayerRelics>() : null;
            RelicChest.OpenRequested = OpenRelicWindow;
            RelicChest.CanOpen = CanOpenRelicWindow;
        }

        private void ShutdownRelics()
        {
            if (RelicChest.OpenRequested == (Func<RelicId[], Action<RelicId>, Action, bool>)OpenRelicWindow) RelicChest.OpenRequested = null;
            if (RelicChest.CanOpen == (Func<bool>)CanOpenRelicWindow) RelicChest.CanOpen = null;
        }

        private bool CanOpenRelicWindow()
        {
            if (relicWindowOpen || relicUnlockFrame >= 0 || equipCardOpen || equipCardUnlockFrame >= 0 || villageOpen || villageUnlockFrame >= 0) return false;
            if (paused || traitWindowOpen || deathHandled || stageCleared || health == null || health.IsDead) return false;
            return !control.ChantActive && !control.ShieldMinigameActive && !control.Locked;
        }

        private bool OpenRelicWindow(RelicId[] offer, Action<RelicId> onChosen, Action onCancelled)
        {
            if (offer == null || offer.Length == 0 || !CanOpenRelicWindow()) return false;

            relicWindowOpen = true;
            relicOffer = offer;
            relicChosen = onChosen;
            relicCancelled = onCancelled;
            relicOpenedFrame = Time.frameCount;
            relicCodexAtOpen = IBlameYou.Core.SaveData.RelicCodexMask(IBlameYou.Core.GameSession.DevMode);
            relicSelection = 0;
            relicKeyboard = false;
            relicColumns = offer.Length;
            relicWindowOpenedAt = Time.unscaledTime; // 카드 팝 (SkillHUD.Cards.cs), 패널 팝·배경 페이드 (RelicPanelScale)
            GameAudio.PlaySfx("ChestCreak", 0.6f); // 뚜껑 "끼-익" + 안에서 "샤랑" (Tools/AudioGen)
            control.Locked = true;
            Time.timeScale = 0f;
            return true;
        }

        private void ChooseRelic(int index)
        {
            if (!relicWindowOpen || relicOffer == null || index < 0 || index >= relicOffer.Length) return;
            var chosen = relicOffer[index];
            var callback = relicChosen;
            relicCancelled = null; // 골랐으니 취소 콜백은 부르지 않는다
            CloseRelicWindow(true);
            callback?.Invoke(chosen);
            if (OwnsRelic(chosen)) StartRelicFly(chosen, index); // 아이콘이 HUD 유물 칸으로 날아간다 (SkillHUD.RelicFly.cs)
            AddFeed($"유물: {RelicBook.Name(chosen)} · {RelicBook.Description(chosen)}", new Color(1f, 0.85f, 0.4f));
        }

        // 고르지 않고 닫히면(쓰러짐, 클리어) 상자에 알려서 다음에 다시 열 수 있게 한다.
        // keepLockThisFrame: 키로 고른 경우 그 키가 같은 프레임의 스킬 입력(RoleInput)으로 새지 않게 잠금은 다음 프레임에 푼다.
        private void CloseRelicWindow(bool keepLockThisFrame = false)
        {
            if (!relicWindowOpen) return;
            relicWindowOpen = false;
            var cancelled = relicCancelled;
            relicOffer = null;
            relicChosen = null;
            relicCancelled = null;
            Time.timeScale = 1f;
            if (keepLockThisFrame) relicUnlockFrame = Time.frameCount + 1;
            else if (!deathHandled && !stageCleared) control.Locked = false;
            cancelled?.Invoke();
        }

        private void UpdateRelicWindowInput()
        {
            if (relicUnlockFrame >= 0 && Time.frameCount >= relicUnlockFrame)
            {
                relicUnlockFrame = -1;
                if (!deathHandled && !stageCleared && !paused && !traitWindowOpen) control.Locked = false;
            }

            // 상자에 닿은 프레임에 누르고 있던 스킬 키로 후보를 보지도 않고 고르지 않게, 연 프레임의 입력은 버린다.
            if (!relicWindowOpen || Time.frameCount <= relicOpenedFrame) return;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ChooseRelic(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ChooseRelic(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ChooseRelic(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ChooseRelic(3); // 네잎 클로버
            else UpdateRelicKeys();
        }

        // ←→·A/D로 카드 옮기기 (2×2면 ↑↓·W/S로 줄 옮기기), Enter·Space로 고르기. 첫 화살표는 지금 칸(첫 카드)을 보여 주기만 한다.
        // 고른 키는 같은 프레임 스킬·순간이동 입력으로 새지 않게 ChooseRelic → CloseRelicWindow(true)가 잠금을 다음 프레임에 푼다.
        // A/D·Space·Enter는 이동·점프·순간이동 키이기도 해서, 카드가 다 나타나기 전(0.4초)엔 받지 않고 Enter·Space는 화살표로 칸을 보인 뒤에만 받는다
        // (상자에 뛰어들며 점프를 연타하다 후보를 보지도 않고 고르지 않게).
        private const float RelicKeysDelay = 0.4f;

        private void UpdateRelicKeys()
        {
            if (Time.unscaledTime - relicWindowOpenedAt < RelicKeysDelay) return;
            int count = relicOffer.Length;
            int step = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) step = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) step = 1;
            else if (relicColumns < count && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))) step = -relicColumns;
            else if (relicColumns < count && (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))) step = relicColumns;
            if (step != 0)
            {
                if (relicKeyboard) relicSelection = ((relicSelection + step) % count + count) % count;
                relicKeyboard = true;
                GameAudio.PlaySfx("UiClick", 0.35f, 0.04f);
                return;
            }

            if (relicKeyboard && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                GameAudio.PlaySfx("UiClick", 0.7f, 0.04f); // 마우스로 누를 때와 같은 소리
                ChooseRelic(Mathf.Clamp(relicSelection, 0, count - 1));
            }
        }

        // 창 열림 연출: 배경 어둠이 RelicFadeTime 동안 깔리고, 패널이 RelicPopTime 동안 0.85 → 1.04 → 1 (일시정지 창 팝과 같은 곡선).
        private const float RelicFadeTime = 0.15f;
        private const float RelicPopTime = 0.18f;

        private float RelicPanelScale()
        {
            float t = (Time.unscaledTime - relicWindowOpenedAt) / RelicPopTime;
            if (t >= 1f || t < 0f) return 1f;
            return t < 0.6f
                ? Mathf.Lerp(0.85f, 1.04f, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }

        private bool OwnsRelic(RelicId id)
        {
            if (relics == null) return false;
            for (int i = 0; i < relics.Owned.Count; i++)
                if (relics.Owned[i] == id) return true;
            return false;
        }

        // 유물 한 줄에 하나: "이름 — 효과".
        private static string RelicLine(RelicId id) => $"{RelicBook.Name(id)} — {RelicBook.Description(id)}";

        // 결과 창용: 이름만 쉼표로.
        private string RelicNames()
        {
            if (RelicCount == 0) return "유물 없음";
            var names = new System.Text.StringBuilder("유물: ");
            for (int i = 0; i < RelicCount; i++)
            {
                if (i > 0) names.Append(", ");
                names.Append(RelicBook.Name(relics.Owned[i]));
            }

            return names.ToString();
        }

        // DrawRelicList: 레벨 막대 아래 유물 목록 (SkillHUD.RelicIcons.cs)

        private void DrawRelicWindow()
        {
            if (!relicWindowOpen || relicOffer == null) return;

            const float fullCardWidth = 230f;
            const float minCardWidth = 180f; // 이보다 좁으면 설명이 너무 접혀서 2×2로 바꾼다
            const float cardHeight = 200f;
            const float gap = 14f;
            const float edge = 16f;          // 화면 가장자리 최소 여백
            int count = relicOffer.Length;
            // 한 줄로 화면에 들어가면 그대로, 모자라면 카드 폭을 줄이고, 그래도 180px 밑이면 2열로 (네잎 클로버 4장, 좁은 화면).
            float available = Screen.width - edge * 2f - 40f;
            int columns = count;
            float cardWidth = Mathf.Min(fullCardWidth, (available - (columns - 1) * gap) / columns);
            if (cardWidth < minCardWidth && count >= 4)
            {
                columns = 2;
                cardWidth = Mathf.Min(fullCardWidth, (available - gap) / columns);
            }
            int rows = (count + columns - 1) / columns;
            relicColumns = columns;
            float width = columns * cardWidth + (columns - 1) * gap + 40f;
            const float ownedRowHeight = 30f; // 아래 "가진 유물 N개" 아이콘 줄 (DrawRelicOwnedRow)
            float height = 80f + rows * cardHeight + (rows - 1) * gap + 20f + ownedRowHeight;
            float top = Mathf.Max(edge, Mathf.Min(Screen.height * 0.22f, (Screen.height - height) / 2f));
            var rect = new Rect((Screen.width - width) / 2f, top, width, height);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((Time.unscaledTime - relicWindowOpenedAt) / RelicFadeTime));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            var windowMatrix = GUI.matrix; // 패널 팝: 카드 팝(BeginCardPop)은 이 위에 겹친다
            float panelScale = RelicPanelScale();
            if (panelScale != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * panelScale, rect.center);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "유물 상자", big);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 22f),
                $"하나만 고를 수 있다 · A와 B가 함께 갖는다 · {(count >= 4 ? "1/2/3/4" : count == 3 ? "1/2/3" : count == 2 ? "1/2" : "1")} 키, ←→ + Enter 또는 클릭",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            for (int i = 0; i < count; i++)
            {
                var id = relicOffer[i];
                int col = i % columns, row = i / columns;
                var card = new Rect(rect.x + 20f + col * (cardWidth + gap), rect.y + 80f + row * (cardHeight + gap), cardWidth, cardHeight);
                var previousMatrix = BeginCardPop(card, relicWindowOpenedAt, i);
                string owner = RelicBook.Owner(id);
                DrawCard(card, owner);

                string ownerColor = owner == "A" ? "#73d9ff" : owner == "B" ? "#ffa659" : "#dddddd";
                var iconRect = new Rect(card.xMax - 46f, card.y + 8f, 36f, 36f);
                if (i < relicCardIcons.Length) relicCardIcons[i] = iconRect; // 고르면 여기서 날아간다 (SkillHUD.RelicFly.cs)
                DrawRelicIcon(id, iconRect); // 오른쪽 위 큰 아이콘 (SkillHUD.RelicIcons.cs)
                GUI.Label(new Rect(card.x + 10f, card.y + 10f, card.width - 60f, 26f), $"<b>{i + 1}. {RelicBook.Name(id)}</b>", label);
                GUI.Label(new Rect(card.x + 10f, card.y + 40f, card.width - 20f, 22f), $"<color={ownerColor}>{owner}</color> 유물", small);
                if (!RelicCodex.Has(relicCodexAtOpen, (int)id)) // 도감에 없는 유물: 아이콘 아래 NEW
                    GUI.Label(new Rect(card.xMax - 56f, card.y + 46f, 46f, 18f), "<b><color=#7dff7a>NEW</color></b>",
                        new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true });
                GUI.Label(new Rect(card.x + 10f, card.y + 70f, card.width - 20f, 60f), $"<color=#ffd84d>{RelicBook.Description(id)}</color>", label);

                var buttonRect = new Rect(card.x + 10f, card.yMax - 40f, card.width - 20f, 30f);
                if (relicKeyboard && i == relicSelection) CuteButton.DrawSelection(card); // 키보드로 고른 카드 둘레 금색 테두리
                bool chosen = CuteButton.Button(buttonRect, "고르기", cardButton);
                GUI.matrix = previousMatrix;
                if (chosen)
                {
                    GUI.matrix = windowMatrix;
                    ChooseRelic(i);
                    return;
                }
            }

            DrawRelicOwnedRow(new Rect(rect.x + 20f, rect.yMax - 20f - ownedRowHeight + 6f, rect.width - 40f, 22f));
            GUI.matrix = windowMatrix;
        }

        // 창 아래 "가진 유물 N개" + 아이콘 줄: 지금 가진 유물을 보며 고르게. 줄에 다 안 들어가면 앞쪽만 그리고 "+k".
        // 마우스를 올린 아이콘은 이름을 줄 오른쪽에 적는다.
        private void DrawRelicOwnedRow(Rect row)
        {
            var style = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = true };
            int owned = RelicCount;
            var header = new GUIContent(owned == 0 ? "<color=#bbbbbb>가진 유물 없음</color>" : $"<color=#ffd84d>가진 유물 {owned}개</color>");
            float headerWidth = style.CalcSize(header).x;
            GUI.Label(new Rect(row.x, row.y, headerWidth + 2f, row.height), header, style);
            if (owned == 0) return;

            const float icon = 18f;
            const float step = icon + 4f;
            float x = row.x + headerWidth + 8f;
            const float nameRoom = 120f; // 오른쪽에 마우스 올린 유물 이름 칸
            float maxX = row.xMax - nameRoom;
            int fit = Mathf.Max(0, Mathf.FloorToInt((maxX - x) / step));
            int shown = owned <= fit ? owned : Mathf.Max(0, fit - 1); // 넘치면 마지막 칸은 "+k"
            var mouse = Event.current.mousePosition;
            int hovered = -1;
            for (int i = 0; i < shown; i++)
            {
                var r = new Rect(x, row.y + (row.height - icon) * 0.5f, icon, icon);
                if (RelicIcon(relics.Owned[i]) != null) DrawRelicIcon(relics.Owned[i], r);
                else GUI.Label(new Rect(r.x - 2f, row.y, icon + 4f, row.height), "•", style);
                if (r.Contains(mouse)) hovered = i;
                x += step;
            }
            if (shown < owned) GUI.Label(new Rect(x, row.y, 40f, row.height), $"+{owned - shown}", style);
            if (hovered >= 0)
                GUI.Label(new Rect(row.xMax - nameRoom, row.y, nameRoom, row.height), RelicBook.Name(relics.Owned[hovered]),
                    new GUIStyle(style) { alignment = TextAnchor.MiddleRight });
        }
    }
}
