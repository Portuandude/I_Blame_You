using System.Collections.Generic;
using IBlameYou.Systems;
using IBlameYou.UI;
using UnityEngine;

namespace IBlameYou.Core
{
    // 유물 도감 창 (IMGUI): 모든 유물을 격자로. 한 번이라도 고른 유물은 아이콘·이름, 아직이면 흐린 아이콘 + "???".
    // 시작 화면(StartMenu)과 게임 중(SkillHUD.Codex, C키)이 같은 창을 쓴다. 여닫기 키는 부르는 쪽이 처리하고,
    // 창 안의 칸 고르기(방향키/WASD·마우스)·Tab 모드 바꾸기·닫기 버튼은 여기서 처리한다.
    public class RelicCodexWindow
    {
        private int codexCursor = -1;   // 고른 칸 (방향키/WASD 또는 마우스). -1이면 없음
        private Vector2 codexLastMouse; // 마우스가 움직였는지 보려고 (움직이면 마우스 쪽이 우선)
        private bool codexDevMode;      // 보여 주는 모드 (열 때 정하고, 창 안에서 Tab으로 바꾼다)
        private readonly Dictionary<IBlameYou.Player.RelicId, Texture2D> codexIcons = new Dictionary<IBlameYou.Player.RelicId, Texture2D>();

        private IReadOnlyList<IBlameYou.Player.RelicId> runOwned; // 게임 중이면 이번 판에 가진 유물 (시작 화면은 null)
        private bool runDevMode;        // 이번 판의 모드: 보유 점은 이 모드 화면에서만 (Tab으로 다른 모드를 보면 숨긴다)
        private static readonly Color OwnedMint = new Color(0.45f, 1f, 0.8f);
        private int poppedCursor = -1;  // 아이콘 톡을 마지막으로 시작한 칸 (칸이 바뀌면 새로 톡)
        private float cursorPoppedAt = -10f;
        private const float IconPopTime = 0.2f;
        private float openedAt = -10f;  // 연 시각 (unscaledTime: 게임 중에는 시간이 멈춰 있다). 배경 페이드·패널 팝

        // 창 열림 연출: 배경 어둠이 FadeTime 동안 깔리고 패널이 PopTime 동안 0.85 → 1.04 → 1 (유물·특성 창과 같은 곡선)
        private const float FadeTime = 0.15f;
        private const float PopTime = 0.18f;

        public bool IsOpen { get; private set; }

        // dev: 처음 보여 줄 모드 (시작 화면은 메뉴 토글 값, 게임 중에는 지금 판의 모드). 고른 칸 없이 연다.
        // owned: 게임 중이면 이번 판에 가진 유물 — 그 칸 오른쪽 위에 민트색 "보유" 점을 찍는다. 시작 화면은 null.
        public void Show(bool dev, IReadOnlyList<IBlameYou.Player.RelicId> owned = null)
        {
            runOwned = owned;
            runDevMode = dev;
            if (!IsOpen) GameAudio.PlaySfx("BookOpen", 0.6f); // 책 펼치는 "파라락" (특성 창과 같은 소리)
            IsOpen = true;
            codexCursor = -1;
            poppedCursor = -1;
            codexDevMode = dev;
            openedAt = Time.unscaledTime;
        }

        // playSound: 사람이 닫을 때(C·Esc·닫기 버튼)만 "통". 쓰러짐·클리어로 닫힐 때는 조용히.
        public void Close(bool playSound = false)
        {
            if (IsOpen && playSound) GameAudio.PlaySfx("PauseClose", 0.6f);
            IsOpen = false;
        }

        // 고른 칸 아이콘: 1 → 1.18 → 1배 (IconPopTime). 그 밖에는 1배.
        private float IconPopScale()
        {
            float t = (Time.unscaledTime - cursorPoppedAt) / IconPopTime;
            if (t >= 1f || t < 0f) return 1f;
            return t < 0.4f
                ? Mathf.Lerp(1f, 1.18f, Mathf.SmoothStep(0f, 1f, t / 0.4f))
                : Mathf.Lerp(1.18f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.6f));
        }

        private float PanelScale()
        {
            float t = (Time.unscaledTime - openedAt) / PopTime;
            if (t >= 1f || t < 0f) return 1f;
            return t < 0.6f
                ? Mathf.Lerp(0.85f, 1.04f, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                : Mathf.Lerp(1.04f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
        }

        // 유물 도감 창: 모든 유물을 격자로. 한 번이라도 고른 유물은 아이콘·이름, 아직이면 흐린 아이콘 + "???". C/Esc/닫기로 닫는다.
        public void Draw(GUIStyle panel, GUIStyle body, GUIStyle recordSmall, GUIStyle button)
        {
            var matrix = GUI.matrix; // 패널 팝 동안 키운 행렬을 어느 길로 나가든 되돌린다
            try { DrawWindow(panel, body, recordSmall, button); }
            finally { GUI.matrix = matrix; }
        }

        private void DrawWindow(GUIStyle panel, GUIStyle body, GUIStyle recordSmall, GUIStyle button)
        {
            var all = IBlameYou.Player.RelicBook.All;
            long mask = SaveData.RelicCodexMask(codexDevMode);
            // 낮은 창에서도 닫기 버튼까지 화면 안에 들어오게 칸 높이를 줄이고, 그래도 넘치면 가로 칸을 늘린다 (RelicCodex.Grid*)
            int columns = RelicCodex.GridColumns(all.Length, Screen.width, Screen.height);
            int rows = RelicCodex.GridRows(all.Length, columns);
            const float cell = RelicCodex.GridCellWidth;
            float cellHeight = RelicCodex.GridCellHeight(rows, Screen.height);
            float iconSize = Mathf.Min(44f, cellHeight - 36f);
            float width = columns * cell + 40f;
            float height = 70f + rows * cellHeight + 36f + 56f; // 36: 마우스 올린 유물 설명 (좁은 창에선 두 줄)
            var rect = new Rect((Screen.width - width) / 2f, Mathf.Max(10f, (Screen.height - height) / 2f), width, height);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Clamp01((Time.unscaledTime - openedAt) / FadeTime));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
            float panelScale = PanelScale();
            if (panelScale != 1f) GUIUtility.ScaleAroundPivot(Vector2.one * panelScale, rect.center);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Box(rect, GUIContent.none, panel);

            var head = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(rect.x, rect.y + 12f, rect.width, 30f), $"유물 도감 ({(codexDevMode ? "개발자 모드" : "일반 모드")})", head);
            GUI.Label(new Rect(rect.x, rect.y + 42f, rect.width, 22f), SaveData.RelicCodexSummary(codexDevMode, all.Length),
                new GUIStyle(recordSmall) { alignment = TextAnchor.MiddleCenter });
            DrawProgressBar(new Rect(rect.center.x - 90f, rect.y + 63f, 180f, 4f), RelicCodex.Count(mask, all.Length), all.Length, previous);

            // 칸 고르기: 방향키/WASD로 옮기고, 마우스를 움직이면 마우스가 올라간 칸(칸 밖이면 없음)으로 바뀐다.
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                int step = 0;
                switch (e.keyCode)
                {
                    case KeyCode.LeftArrow: case KeyCode.A: step = -1; break;
                    case KeyCode.RightArrow: case KeyCode.D: step = 1; break;
                    case KeyCode.UpArrow: case KeyCode.W: step = -columns; break;
                    case KeyCode.DownArrow: case KeyCode.S: step = columns; break;
                    case KeyCode.Tab: // 다른 모드 도감 보기 (고른 칸은 그대로 두고 설명만 그 모드 기준으로)
                        codexDevMode = !codexDevMode;
                        e.Use();
                        return; // 이번 이벤트에서는 그리지 않는다 (다음 Repaint에서 새 모드로)
                }
                if (step != 0)
                {
                    int next = codexCursor < 0 ? 0 : codexCursor + step;
                    if (next >= 0 && next < all.Length && next != codexCursor)
                    {
                        codexCursor = next; // 가장자리에서는 멈춘다
                        GameAudio.PlaySfx("UiClick", 0.25f, 0.04f); // 칸을 옮길 때 작은 "톡" (가장자리에서 멈추면 소리 없음)
                    }
                    e.Use();
                }
            }
            bool mouseMoved = e.type != EventType.KeyDown && e.mousePosition != codexLastMouse; // 키 이벤트의 마우스 위치는 믿지 않는다 (리뷰 LOW)
            if (mouseMoved) codexLastMouse = e.mousePosition;
            if (codexCursor >= all.Length) codexCursor = -1;

            var name = new GUIStyle(recordSmall) { alignment = TextAnchor.UpperCenter, fontSize = 12 };
            int mouseCell = -1;
            for (int i = 0; i < all.Length; i++)
            {
                var id = all[i];
                bool known = RelicCodex.Has(mask, (int)id);
                var cellRect = new Rect(rect.x + 20f + (i % columns) * cell, rect.y + 70f + (i / columns) * cellHeight, cell, cellHeight);
                if (cellRect.Contains(e.mousePosition)) mouseCell = i;
                var icon = CodexIcon(id);
                if (icon != null)
                {
                    GUI.color = known ? Color.white : new Color(0.15f, 0.12f, 0.25f, 0.6f); // 모르는 유물은 어두운 실루엣
                    float drawn = iconSize * (i == poppedCursor ? IconPopScale() : 1f); // 고른 칸 아이콘 톡 (발밑 기준으로 위로 커진다)
                    GUI.DrawTexture(new Rect(cellRect.center.x - drawn / 2f, cellRect.y + 4f + iconSize - drawn, drawn, drawn), icon, ScaleMode.ScaleToFit);
                    GUI.color = previous;
                }
                if (known) DrawOwnerBadge(IBlameYou.Player.RelicBook.Owner(id), new Vector2(cellRect.center.x - iconSize / 2f, cellRect.y + 4f + iconSize));
                if (OwnedNow(id)) DrawOwnedDot(new Vector2(cellRect.center.x + iconSize / 2f, cellRect.y + 4f));
                GUI.Label(new Rect(cellRect.x + 2f, cellRect.y + iconSize + 6f, cellRect.width - 4f, 32f),
                    known ? IBlameYou.Player.RelicBook.Name(id) : "???", name);
            }

            if (mouseMoved) codexCursor = mouseCell;
            if (codexCursor != poppedCursor)
            {
                poppedCursor = codexCursor; // 키로든 마우스로든 칸이 바뀌면 그 아이콘이 톡 (다음 프레임부터 보인다)
                cursorPoppedAt = Time.unscaledTime;
            }
            int hovered = codexCursor;
            if (hovered >= 0)
                DrawCellFrame(new Rect(rect.x + 20f + (hovered % columns) * cell, rect.y + 70f + (hovered / columns) * cellHeight, cell, cellHeight), previous);

            // 고른 칸의 설명 한 줄: 모은 유물이면 "[A] 설명", 아직이면 "아직 모르는 유물". 안 골랐으면 안내 문구.
            string info = hovered < 0 ? "칸에 마우스를 올리거나 방향키로 고르면 설명 · Tab: 다른 모드"
                : RelicCodex.Has(mask, (int)all[hovered])
                    ? $"[{IBlameYou.Player.RelicBook.Owner(all[hovered])}] {IBlameYou.Player.RelicBook.Name(all[hovered])} — {IBlameYou.Player.RelicBook.Description(all[hovered])}"
                      + (OwnedNow(all[hovered]) ? " (이번 판 보유)" : "")
                    : "아직 모르는 유물 — 상자에서 골라 보면 도감에 오른다";
            var infoStyle = new GUIStyle(recordSmall) { alignment = TextAnchor.MiddleCenter, wordWrap = true, clipping = TextClipping.Clip };
            GUI.color = hovered >= 0 && RelicCodex.Has(mask, (int)all[hovered]) ? OutlinedLabel.Cream : new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(rect.x + 10f, rect.yMax - 90f, rect.width - 20f, 36f), info, infoStyle);
            GUI.color = previous;

            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.center.x - 150f, rect.yMax - 48f, 160f, 36f), codexDevMode ? "Tab: 일반 보기" : "Tab: 개발자 보기",
                    new GUIStyle(button) { fontSize = 16 })) // 160px 칸에 들어가게 작은 글씨 (리뷰 HIGH)
                codexDevMode = !codexDevMode;
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.center.x + 20f, rect.yMax - 48f, 130f, 36f), "닫기 (C)", button)) Close(true);
        }

        // 모은 유물 아이콘 왼쪽 아래에 주인 배지: A는 하늘색, B는 주황 글자 (공용은 표시 없음). corner = 아이콘 왼쪽 아래.
        private GUIStyle ownerBadge;

        private void DrawOwnerBadge(string owner, Vector2 corner)
        {
            if (owner != "A" && owner != "B") return;
            if (ownerBadge == null)
                ownerBadge = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
            var rect = new Rect(corner.x - 6f, corner.y - 12f, 14f, 14f);
            var previous = GUI.color;
            GUI.color = new Color(0.12f, 0.08f, 0.22f, 0.85f * previous.a); // 진한 바탕 사각
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            ownerBadge.normal.textColor = owner == "A" ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.65f, 0.3f);
            GUI.Label(rect, owner, ownerBadge);
        }

        // 모은 수 진행 막대: 진한 바탕 + 금색 채움. 다 모았으면 밝은 띠가 1.6초마다 왼쪽에서 오른쪽으로 지나간다.
        private static void DrawProgressBar(Rect bar, int count, int total, Color restore)
        {
            if (total <= 0) return;
            float ratio = Mathf.Clamp01((float)count / total);
            GUI.color = new Color(0.12f, 0.08f, 0.22f, 0.85f);
            GUI.DrawTexture(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), Texture2D.whiteTexture);
            GUI.color = OutlinedLabel.Gold;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), Texture2D.whiteTexture);
            if (count >= total && total <= RelicCodex.MaxRelics)
            {
                const float band = 24f;
                float x = Mathf.Repeat(Time.unscaledTime / 1.6f, 1f) * (bar.width + band) - band; // 막대 밖에서 들어와 밖으로 나간다
                float left = Mathf.Max(bar.x, bar.x + x);
                float right = Mathf.Min(bar.xMax, bar.x + x + band);
                if (right > left)
                {
                    GUI.color = new Color(1f, 1f, 0.9f, 0.85f);
                    GUI.DrawTexture(new Rect(left, bar.y, right - left, bar.height), Texture2D.whiteTexture);
                }
            }
            GUI.color = restore;
        }

        // 이번 판에 가진 유물인가 (게임 중, 그 판의 모드 화면일 때만).
        private bool OwnedNow(IBlameYou.Player.RelicId id)
        {
            if (runOwned == null || codexDevMode != runDevMode) return false;
            for (int i = 0; i < runOwned.Count; i++)
                if (runOwned[i] == id) return true;
            return false;
        }

        // "보유" 점: 아이콘 오른쪽 위에 진한 테두리 + 민트색 점 (corner = 아이콘 오른쪽 위).
        private static void DrawOwnedDot(Vector2 corner)
        {
            var previous = GUI.color;
            GUI.color = new Color(0.12f, 0.08f, 0.22f, 0.9f * previous.a);
            GUI.DrawTexture(new Rect(corner.x - 7f, corner.y - 2f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = new Color(OwnedMint.r, OwnedMint.g, OwnedMint.b, previous.a);
            GUI.DrawTexture(new Rect(corner.x - 5f, corner.y, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // 고른 칸 둘레에 금색 테두리 (2px, 칸 안쪽으로 조금 들여서).
        private static void DrawCellFrame(Rect cellRect, Color restore)
        {
            var r = new Rect(cellRect.x + 4f, cellRect.y + 1f, cellRect.width - 8f, cellRect.height - 2f);
            const float t = 2f;
            GUI.color = OutlinedLabel.Gold;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.color = restore;
        }

        private Texture2D CodexIcon(IBlameYou.Player.RelicId id)
        {
            if (!codexIcons.TryGetValue(id, out var icon))
            {
                icon = Resources.Load<Texture2D>("UI/HUD/Relics/" + id);
                codexIcons[id] = icon; // 없어도(null) 기억해서 매 프레임 다시 찾지 않는다
            }
            return icon;
        }
    }
}
