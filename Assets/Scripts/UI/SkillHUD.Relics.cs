using System;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 유물 고르기 창: 정예 구역의 유물 상자(RelicChest)에 닿으면 열린다. 후보 3개 중 하나를 1/2/3 키나 클릭으로 고른다.
    // 열려 있는 동안 게임이 멈추고 조작이 잠긴다 (특성 창과 같은 방식). 유물은 A/B 공용이라 누가 고르든 둘 다 갖는다.
    // 영창·방어막 미니게임·특성 창·일시정지 중에는 열지 않고, 상자가 다시 물어 올 때까지 기다린다.
    public partial class SkillHUD
    {
        private bool relicWindowOpen;
        private RelicId[] relicOffer;
        private Action<RelicId> relicChosen;
        private Action relicCancelled;
        private int relicOpenedFrame;
        private int relicUnlockFrame = -1; // 고른 키가 같은 프레임에 스킬 입력으로 새지 않게, 이 프레임부터 조작 잠금을 푼다

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
            if (relicWindowOpen || relicUnlockFrame >= 0) return false;
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
            relicWindowOpenedAt = Time.unscaledTime; // 카드 팝 (SkillHUD.Cards.cs)
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
            feed.Add(new FeedEntry { Time = Time.time, Text = $"유물: {RelicBook.Name(chosen)} · {RelicBook.Description(chosen)}", Color = new Color(1f, 0.85f, 0.4f) });
            if (feed.Count > MaxFeed) feed.RemoveAt(0);
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

            const float cardWidth = 230f;
            const float gap = 14f;
            int count = relicOffer.Length;
            float width = count * cardWidth + (count - 1) * gap + 40f;
            var rect = new Rect((Screen.width - width) / 2f, Screen.height * 0.22f, width, 300f);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel); // 두 겹으로 덜 비치게

            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 34f), "유물 상자", big);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 48f, rect.width - 40f, 22f),
                "하나만 고를 수 있다 · A와 B가 함께 갖는다 · 1/2/3 키 또는 클릭",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            for (int i = 0; i < count; i++)
            {
                var id = relicOffer[i];
                var card = new Rect(rect.x + 20f + i * (cardWidth + gap), rect.y + 80f, cardWidth, rect.height - 100f);
                var previousMatrix = BeginCardPop(card, relicWindowOpenedAt, i);
                string owner = RelicBook.Owner(id);
                DrawCard(card, owner);

                string ownerColor = owner == "A" ? "#73d9ff" : owner == "B" ? "#ffa659" : "#dddddd";
                DrawRelicIcon(id, new Rect(card.xMax - 46f, card.y + 8f, 36f, 36f)); // 오른쪽 위 큰 아이콘 (SkillHUD.RelicIcons.cs)
                GUI.Label(new Rect(card.x + 10f, card.y + 10f, card.width - 60f, 26f), $"<b>{i + 1}. {RelicBook.Name(id)}</b>", label);
                GUI.Label(new Rect(card.x + 10f, card.y + 40f, card.width - 20f, 22f), $"<color={ownerColor}>{owner}</color> 유물", small);
                GUI.Label(new Rect(card.x + 10f, card.y + 70f, card.width - 20f, 60f), $"<color=#ffd84d>{RelicBook.Description(id)}</color>", label);

                bool chosen = GUI.Button(new Rect(card.x + 10f, card.yMax - 40f, card.width - 20f, 30f), "고르기", cardButton);
                GUI.matrix = previousMatrix;
                if (chosen)
                {
                    ChooseRelic(i);
                    break;
                }
            }
        }
    }
}
