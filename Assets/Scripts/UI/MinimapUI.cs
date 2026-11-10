using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 방 격자 모드의 미니맵 (Docs/LevelDesign.md). 화면 오른쪽 아래에 지금 방 주변을, ` 키(백쿼트)로 방 전체 지도를 크게 펼친다.
    // 던그리드 방식 (MinimapRules, 사람 결정 2026-11-10): 가 본 방만 칸으로 그리고, 안 가 본 방은 칸 없이 가 본 방에서
    // 뻗은 통로 실선만 보여 "그쪽에 방이 더 있다"는 것만 알린다. 방 종류 아이콘·룬 점·자물쇠도 가 본 방에만. 지금 방은 깜빡이는 흰 테두리.
    // 탐험가의 나침반 유물이 있으면 안 가 본 방도 흐린 칸 + 흐린 종류 아이콘으로 보이고 통로 실선이 지도 전체를 잇는다 (MinimapRules.Look).
    // 방 종류 색: 보스 빨강, 중간보스 주황, 정예 보라, 보물 금색, 샘 하늘색, 시작 초록.
    // 귀엽게 (그림이 있으면): 둥근 보라 패널 위에 둥근 방 칸, 방 종류는 아이콘 (보스 왕관, 중간보스 투구, 정예 보라 별,
    // 보물 상자, 샘 하트, 시작 깃발 — Tools/ArtGen/generate_hud.py → Resources/UI/HUD). 그림이 없으면 예전 색 네모.
    public class MinimapUI : MonoBehaviour
    {
        private const int SmallCols = 9;   // 작은 지도에 보이는 칸 수 (지금 방이 가운데)
        private const int SmallRows = 5;
        private const float SmallCell = 22f;
        private const float Margin = 12f;

        private static readonly Color Visited = new Color(0.78f, 0.74f, 0.9f, 0.95f);
        private static readonly Color Faint = new Color(0.78f, 0.74f, 0.9f, 0.3f);  // 나침반으로 보이는 안 가 본 방
        private const float FaintIconAlpha = 0.45f;
        private static readonly Color Link = new Color(0.95f, 0.92f, 1f, 0.85f);
        private static readonly Color Frame = new Color(0.094f, 0.078f, 0.102f, 0.7f); // ArtStyle outline
        private static readonly Color BossColor = new Color(1f, 0.32f, 0.3f);
        private static readonly Color MidBossColor = new Color(1f, 0.6f, 0.2f);
        private static readonly Color EliteColor = new Color(0.75f, 0.45f, 1f);
        private static readonly Color TreasureColor = new Color(1f, 0.85f, 0.3f);   // gold
        private static readonly Color RestColor = new Color(0.5f, 0.97f, 1f);       // aqua
        private static readonly Color StartColor = new Color(0.6f, 1f, 0.45f);

        private ChunkStreamer streamer;
        private PlayerControlState control;
        private GameObject playerObject;
        private PlayerRelics relics; // 탐험가의 나침반 확인용 (늦게 붙을 수 있어 처음 쓸 때 찾는다)
        private bool expanded;
        private GUIStyle legend;
        private GUIStyle legendLeft;

        // 새 방 발견 연출: 처음 들어간 방의 칸이 RevealTime 동안 작게 시작해 살짝 커졌다 제자리로 "톡" 나타나고,
        // 다 나타나면 칸 위에 흰 반짝임이 한 번 지나간다. 시작할 때 이미 가 본 방(시작 방·이어하기)은 연출 없이.
        private const float RevealTime = 0.3f;
        private const float RevealShine = 0.25f;
        private readonly System.Collections.Generic.HashSet<int> known = new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.Dictionary<int, float> revealStart = new System.Collections.Generic.Dictionary<int, float>();

        public void Initialize(ChunkStreamer stageStreamer, GameObject player)
        {
            streamer = stageStreamer;
            control = player != null ? player.GetComponent<PlayerControlState>() : null;
            playerObject = player;
            if (streamer == null || !streamer.IsRoomMode) return;
            foreach (var room in streamer.Rooms.Rooms) if (streamer.HasVisited(room.Index)) known.Add(room.Index);
            streamer.RoomEntered += OnRoomEntered;
        }

        private void OnDestroy()
        {
            if (streamer != null) streamer.RoomEntered -= OnRoomEntered;
        }

        private void OnRoomEntered(int index)
        {
            if (known.Add(index)) revealStart[index] = Time.unscaledTime;
        }

        // 발견 연출 크기 (0.2 → 1.2 → 1). 연출이 끝났거나 없으면 1.
        private float RevealScale(int index)
        {
            if (!revealStart.TryGetValue(index, out float start)) return 1f;
            float t = (Time.unscaledTime - start) / RevealTime;
            if (t >= 1f) return 1f;
            return t < 0.6f ? Mathf.Lerp(0.2f, 1.2f, t / 0.6f) : Mathf.Lerp(1.2f, 1f, (t - 0.6f) / 0.4f);
        }

        // 다 나타난 직후 흰 반짝임 알파 (0.55 → 0). 없으면 0.
        private float RevealShineAlpha(int index)
        {
            if (!revealStart.TryGetValue(index, out float start)) return 0f;
            float t = (Time.unscaledTime - start - RevealTime) / RevealShine;
            if (t >= 1f)
            {
                revealStart.Remove(index); // 연출 끝 (OnGUI 안이지만 이 방 하나만 지운다)
                return 0f;
            }
            return t < 0f ? 0f : 0.55f * (1f - t);
        }

        private void Update()
        {
            if (streamer == null || !streamer.IsRoomMode) return;
            // B가 영창을 치는 중에는 글자 입력과 겹치지 않게 무시한다
            if (control != null && control.AKeyboardBlocked) return;
            // 일시정지 중에는 새로 펼치지 않는다 (펼친 지도는 닫을 수 있게 둔다)
            if (Time.timeScale == 0f && !expanded) return;
            if (Input.GetKeyDown(KeyCode.BackQuote)) expanded = !expanded;
        }

        private void OnGUI()
        {
            // 일시정지 창 위에 지도를 덮지 않는다
            if (streamer == null || !streamer.IsRoomMode || Time.timeScale == 0f) return;
            var layout = streamer.Rooms;
            var current = layout.Rooms[streamer.CurrentRoom];

            if (expanded)
            {
                int cols = layout.MaxX - layout.MinX + 1;
                int rows = layout.MaxY - layout.MinY + 1;
                float cell = Mathf.Min(Screen.width * 0.86f / cols, Screen.height * 0.6f / rows, 40f);
                float width = cols * cell, height = rows * cell;
                var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
                Background(new Rect(area.x - 16f, area.y - 36f, area.width + 32f, area.height + 72f), new Color(0.05f, 0.04f, 0.1f, 0.85f));
                DrawRooms(layout, current, area, cell, layout.MinX, layout.MaxY);
                EnsureStyle();
                GUI.Label(new Rect(area.x, area.y - 30f, area.width, 24f), "지도 (` 키로 닫기)", legend);
                DrawLegend(new Rect(area.x - 16f, area.yMax + 8f, area.width + 32f, 24f));
            }
            else
            {
                float width = SmallCols * SmallCell, height = SmallRows * SmallCell;
                var area = new Rect(Screen.width - width - Margin, Screen.height - height - Margin, width, height);
                Background(new Rect(area.x - 6f, area.y - 6f, width + 12f, height + 12f), new Color(0.05f, 0.04f, 0.1f, 0.6f));
                GUI.BeginGroup(area);
                DrawRooms(layout, current, new Rect(0f, 0f, width, height), SmallCell,
                    current.X - SmallCols / 2, current.Y + SmallRows / 2);
                GUI.EndGroup();
            }
        }

        // left: 화면 왼쪽 끝 칸의 격자 x, top: 화면 맨 위 칸의 격자 y. 영역 밖 칸은 그리지 않는다.
        private void DrawRooms(RoomLayoutData layout, RoomData current, Rect area, float cell, int left, int top)
        {
            float link = Mathf.Max(3f, cell * 0.22f);
            float pad = Mathf.Max(2f, cell * 0.14f);

            // 통로 실선 (던그리드 방식, MinimapRules): 가 본 두 방은 칸 가운데끼리 잇고, 안 가 본 쪽으로는 반 칸쯤만 뻗는다.
            // 칸이 실선 위에 덮이므로 칸 사이에만 선이 보인다. 작은 지도는 GUI 그룹이 영역 밖을 잘라 준다.
            foreach (var line in MinimapRules.Links(layout, i => IsShown(i, current)))
            {
                if (!Near(line.From.X, line.From.Y, left, top, area, cell)) continue;
                var c = CellRect(line.From.X, line.From.Y, left, top, area, cell).center;
                var (dx, dy) = RoomLayout.Offset(line.Dir);
                float length = line.Kind == MapLink.Full ? cell : cell * 0.85f;
                var end = new Vector2(c.x + dx * length, c.y - dy * length);
                Fill(Rect.MinMaxRect(Mathf.Min(c.x, end.x) - link / 2f, Mathf.Min(c.y, end.y) - link / 2f,
                    Mathf.Max(c.x, end.x) + link / 2f, Mathf.Max(c.y, end.y) + link / 2f), Link);
            }

            foreach (var room in layout.Rooms)
            {
                if (!Inside(room.X, room.Y, left, top, area, cell)) continue;
                // 안 가 본 방은 칸·아이콘·점을 그리지 않는다 (통로 실선만 위에서 그렸다)
                var look = LookOf(room.Index, current);
                if (look == RoomLook.Hidden) continue;
                var r = CellRect(room.X, room.Y, left, top, area, cell);
                var body = new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - pad * 2f);
                if (look == RoomLook.Faint)
                {
                    DrawFaintRoom(room, body);
                    continue;
                }
                float reveal = RevealScale(room.Index);
                if (reveal != 1f)
                {
                    var center = body.center;
                    body.size *= reveal;
                    body.center = center;
                }
                bool blink = room == current && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.55f;
                if (art.Tile != null)
                {
                    // 지금 방: 흰 둥근 칸을 한 겹 크게 깔아 빛나는 테두리처럼
                    if (blink)
                    {
                        float g = Mathf.Max(2f, cell * 0.12f);
                        DrawTile(new Rect(body.x - g, body.y - g, body.width + g * 2f, body.height + g * 2f), Color.white);
                    }
                    DrawTile(body, Visited);
                }
                else
                {
                    Fill(new Rect(body.x - 1f, body.y - 1f, body.width + 2f, body.height + 2f), Frame);
                    Fill(body, Visited);
                }

                var mark = KindColor(room.Kind);
                var icon = KindIcon(room.Kind);
                if (icon != null)
                {
                    float s = body.width * 0.82f;
                    var previous = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(body.center.x - s / 2f, body.center.y - s / 2f, s, s), icon, ScaleMode.ScaleToFit, true);
                    GUI.color = previous;
                }
                else if (mark.HasValue)
                {
                    var c = mark.Value;
                    float s = body.width * 0.5f;
                    Fill(new Rect(body.center.x - s / 2f, body.center.y - s / 2f, s, s), c);
                }

                // 안 연 협동 봉인문: 방 칸 오른쪽 아래에 작은 자물쇠 (열면 사라짐)
                if (art.Lock != null && streamer.HasClosedPairGate(room.Index))
                {
                    float s = body.width * 0.55f;
                    var previous = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(body.xMax - s * 0.75f, body.yMax - s * 0.75f, s, s), art.Lock, ScaleMode.ScaleToFit, true);
                    GUI.color = previous;
                }

                // 마법 스위치 퍼즐 방: 방 칸 왼쪽 위에 룬 속성 색 점 (룬마다 하나, 어떤 속성 마법이 필요한지 미리 보이게)
                var switches = streamer.SwitchElements(room.Index);
                if (switches.Count > 0)
                {
                    float d = Mathf.Max(4f, body.width * 0.26f);
                    float o = Mathf.Max(1f, d * 0.2f);
                    for (int i = 0; i < switches.Count; i++)
                    {
                        var c = ElementInfo.Color(switches[i]);
                        var dot = new Rect(body.x + 1f + i * (d * 0.8f), body.y + 1f, d, d);
                        if (art.Tile != null)
                        {
                            DrawTile(new Rect(dot.x - o, dot.y - o, dot.width + o * 2f, dot.height + o * 2f), new Color(Frame.r, Frame.g, Frame.b, c.a));
                            DrawTile(dot, c);
                        }
                        else
                        {
                            Fill(new Rect(dot.x - 1f, dot.y - 1f, dot.width + 2f, dot.height + 2f), Frame);
                            Fill(dot, c);
                        }
                    }
                }

                // 새 방 발견 직후 흰 반짝임 한 번
                float shine = RevealShineAlpha(room.Index);
                if (shine > 0f)
                {
                    if (art.Tile != null) DrawTile(body, new Color(1f, 1f, 1f, shine));
                    else Fill(body, new Color(1f, 1f, 1f, shine));
                }

                if (blink && art.Tile == null)
                {
                    float t = Mathf.Max(2f, cell * 0.1f);
                    Outline(new Rect(body.x - t, body.y - t, body.width + t * 2f, body.height + t * 2f), t, Color.white);
                }
            }
        }

        // 지도 그림 (한 번만 읽는다). 없는 그림은 null → 예전 색 네모로 그린다.
        private struct MapArt
        {
            public bool Loaded;
            public GUIStyle Tile;
            public GUIStyle Panel;
            public Texture2D Boss, MidBoss, Elite, Treasure, Rest, Start, Lock;
        }

        private static MapArt art;

        private static void LoadArt()
        {
            if (art.Loaded) return;
            art.Loaded = true;
            art.Tile = NineSlice("UI/HUD/RoomTile", 5);
            art.Panel = NineSlice("UI/HUD/Panel", 10);
            art.Boss = Resources.Load<Texture2D>("UI/HUD/Crown");
            art.MidBoss = Resources.Load<Texture2D>("UI/HUD/MapHelmet");
            art.Elite = Resources.Load<Texture2D>("UI/HUD/MapElite");
            art.Treasure = Resources.Load<Texture2D>("UI/HUD/MapChest");
            art.Rest = Resources.Load<Texture2D>("UI/HUD/Heart");
            art.Start = Resources.Load<Texture2D>("UI/HUD/Flag");
            art.Lock = Resources.Load<Texture2D>("UI/HUD/MapLock");
        }

        private static GUIStyle NineSlice(string path, int border)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            var style = new GUIStyle { border = new RectOffset(border, border, border, border) };
            style.normal.background = texture;
            return style;
        }

        private static Texture2D KindIcon(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Boss: return art.Boss;
                case RoomKind.MidBoss: return art.MidBoss;
                case RoomKind.Elite: return art.Elite;
                case RoomKind.Treasure: return art.Treasure;
                case RoomKind.Spring: return art.Rest; // 숨 고르기 방(Rest)은 체크포인트 제거(2026-11-10)로 표시 없음
                case RoomKind.Start: return art.Start;
                default: return null;
            }
        }

        private static void DrawTile(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = color;
            art.Tile.Draw(rect, false, false, false, false);
            GUI.color = previous;
        }

        private static void Background(Rect rect, Color fallback)
        {
            LoadArt();
            if (art.Panel == null)
            {
                Fill(rect, fallback);
                return;
            }

            if (Event.current.type == EventType.Repaint) art.Panel.Draw(rect, false, false, false, false);
        }

        // 범례: 아이콘이 있으면 아이콘 + 이름, 없으면 예전 색 네모 글자.
        private void DrawLegend(Rect rect)
        {
            if (art.Boss == null)
            {
                GUI.Label(rect,
                    "<color=#ff524d>■</color> 보스  <color=#ff9933>■</color> 중간보스  <color=#bf73ff>■</color> 정예  " +
                    "<color=#ffd84d>■</color> 보물  <color=#7ff6ff>■</color> 샘  <color=#99ff73>■</color> 시작", legend);
                return;
            }

            var items = new[]
            {
                (art.Boss, "보스"), (art.MidBoss, "중간보스"), (art.Elite, "정예"),
                (art.Treasure, "보물"), (art.Rest, "샘"), (art.Start, "시작")
            };
            float itemWidth = Mathf.Min(92f, rect.width / items.Length); // 좁은 지도에서도 패널 안에
            float x = rect.center.x - itemWidth * items.Length / 2f;
            foreach (var (icon, name) in items)
            {
                if (icon != null) GUI.DrawTexture(new Rect(x, rect.y + 2f, 20f, 20f), icon, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(x + 22f, rect.y, itemWidth - 22f, rect.height), name, legendLeft);
                x += itemWidth;
            }
        }

        private bool IsShown(int index, RoomData current) => LookOf(index, current) != RoomLook.Hidden;

        private RoomLook LookOf(int index, RoomData current) =>
            MinimapRules.Look(streamer.HasVisited(index), index == current.Index, HasCompass());

        private bool HasCompass()
        {
            if (relics == null && playerObject != null) relics = playerObject.GetComponent<PlayerRelics>();
            return relics != null && relics.Has(RelicId.Compass);
        }

        // 나침반으로 보이는 안 가 본 방: 흐린 칸 + 흐린 방 종류 아이콘(색 네모). 자물쇠·룬 점·발견 연출은 가 본 뒤에.
        private void DrawFaintRoom(RoomData room, Rect body)
        {
            if (art.Tile != null) DrawTile(body, Faint);
            else Fill(body, Faint);

            var icon = KindIcon(room.Kind);
            var mark = KindColor(room.Kind);
            var previous = GUI.color;
            if (icon != null)
            {
                float s = body.width * 0.82f;
                GUI.color = new Color(1f, 1f, 1f, FaintIconAlpha);
                GUI.DrawTexture(new Rect(body.center.x - s / 2f, body.center.y - s / 2f, s, s), icon, ScaleMode.ScaleToFit, true);
            }
            else if (mark.HasValue)
            {
                var c = mark.Value;
                c.a *= FaintIconAlpha;
                float s = body.width * 0.5f;
                Fill(new Rect(body.center.x - s / 2f, body.center.y - s / 2f, s, s), c);
            }
            GUI.color = previous;
        }

        // 칸이 영역 안이거나 바로 옆(한 칸 밖)이면 실선을 그린다 (영역 밖에서 들어오는 실선도 보이게)
        private static bool Near(int x, int y, int left, int top, Rect area, float cell)
        {
            float px = (x - left) * cell, py = (top - y) * cell;
            return px >= -cell && py >= -cell && px <= area.width + 0.5f && py <= area.height + 0.5f;
        }

        private static bool Inside(int x, int y, int left, int top, Rect area, float cell)
        {
            float px = (x - left) * cell, py = (top - y) * cell;
            return px >= 0f && py >= 0f && px + cell <= area.width + 0.5f && py + cell <= area.height + 0.5f;
        }

        private static Rect CellRect(int x, int y, int left, int top, Rect area, float cell) =>
            new Rect(area.x + (x - left) * cell, area.y + (top - y) * cell, cell, cell);

        private static Color? KindColor(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Boss: return BossColor;
                case RoomKind.MidBoss: return MidBossColor;
                case RoomKind.Elite: return EliteColor;
                case RoomKind.Treasure: return TreasureColor;
                case RoomKind.Spring: return RestColor; // 숨 고르기 방(Rest)은 표시 없음
                case RoomKind.Start: return StartColor;
                default: return null;
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Outline(Rect rect, float thickness, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void EnsureStyle()
        {
            if (legend != null) return;
            legend = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, alignment = TextAnchor.MiddleCenter };
            legend.normal.textColor = Color.white;
            legendLeft = new GUIStyle(legend) { alignment = TextAnchor.MiddleLeft };
        }
    }
}
