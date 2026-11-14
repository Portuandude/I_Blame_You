using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.UI
{
    // 방 격자 모드의 미니맵 (Docs/LevelDesign.md). 화면 오른쪽 아래에 지금 방 주변을, ` 키(백쿼트)로 방 전체 지도를 크게 펼친다.
    // 던그리드 방식 (MinimapRules, 사람 결정 2026-11-10): 가 본 방만 칸으로 그리고, 안 가 본 방은 칸 없이 가 본 방에서
    // 뻗은 통로 실선만 보여 "그쪽에 방이 더 있다"는 것만 알린다. 방 종류 아이콘·룬 점·자물쇠도 가 본 방에만. 지금 방은 깜빡이는 흰 테두리.
    // 탐험가의 나침반 유물이 있으면 안 가 본 방도 흐린 칸 + 흐린 종류 아이콘으로 보이고 통로 실선이 지도 전체를 잇는다 (MinimapRules.Look).
    // 보물 지도 유물이 있으면 안 가 본 마을 방만 흐린 칸 + 흐린 집 아이콘으로 보인다.
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
        private static readonly Color CoinColor = new Color(1f, 0.82f, 0.25f);      // 동전 무더기 점 (금색)
        private static readonly Color CurseColor = new Color(0.66f, 0.4f, 0.98f);   // 저주받은 상자 점 (보라, CursedChest와 같은 색)
        private static readonly Color GoblinColor = new Color(1f, 0.93f, 0.55f);    // 보물 고블린 점 (밝은 금색, 깜빡임)
        private static readonly Color VillageColor = new Color(1f, 0.63f, 0.48f);   // 살구색 (NPC 마을, #ffa07a)

        private ChunkStreamer streamer;
        public ChunkStreamer Streamer => streamer; // 일시정지 창 탐험 줄 (SkillHUD.Pause.cs)
        private PlayerControlState control;
        private GameObject playerObject;
        private PlayerRelics relics; // 탐험가의 나침반 확인용 (늦게 붙을 수 있어 처음 쓸 때 찾는다)
        private bool expanded;

        // 큰 지도 펼침·접힘 연출 (2026-11-13): 작은 지도 쪽(오른쪽 아래)에서 톡 커지며 펼쳐지고, 접을 땐 그쪽으로 쏙 줄어든다.
        // 뒤 게임 화면은 살짝 어두워진다. 펼친 동안 지금 방 칸에서 하늘색 고리가 주기적으로 퍼져 내 위치를 알린다.
        private const float ExpandTime = 0.2f;
        private const float CollapseTime = 0.14f;
        private const float ExpandFrom = 0.35f;     // 펼치기 시작 크기
        private const float PingPeriod = 1.4f;      // 지금 방 고리 주기
        private const float PingTime = 0.8f;        // 고리 하나가 퍼지는 시간
        private float expandedAt = -10f;
        private float collapsedAt = -10f;
        public bool Expanded => expanded;
        private GUIStyle legend;
        private GUIStyle legendLeft;

        // 새 방 발견 연출: 처음 들어간 방의 칸이 RevealTime 동안 작게 시작해 살짝 커졌다 제자리로 "톡" 나타나고,
        // 다 나타나면 칸 위에 흰 반짝임이 한 번 지나간다. 시작할 때 이미 가 본 방(시작 방·이어하기)은 연출 없이.
        private const float RevealTime = 0.3f;
        private const float RevealShine = 0.25f;
        private readonly System.Collections.Generic.HashSet<int> known = new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.Dictionary<int, float> revealStart = new System.Collections.Generic.Dictionary<int, float>();

        // 다시 들어간 방 (2026-11-13 방 입장 연출): 이미 아는 방이면 지금 방 칸이 PulseTime 동안 1 → 1.18 → 1로 톡 커졌다 돌아온다.
        private const float PulseTime = 0.25f;
        private const float PulseGrow = 0.18f;
        private int pulseRoom = -1;
        private float pulseStart;

        // 작은 지도 미끄러짐 (2026-11-13 작은 지도 다듬기): 방을 옮기면 칸들이 뚝 바뀌지 않고 SlideTime 동안 새 방 쪽으로 스르륵 밀려 온다.
        // 한 칸 넘게 떨어진 곳으로 옮겼으면(이어 하기 등) 미끄러지지 않고 바로 바뀐다.
        private const float SlideTime = 0.25f;
        private int slideFromRoom = -1;
        private int lastRoom = -1;
        private float slideStart = -10f;

        // 나침반 획득 연출: 나침반을 얻은 뒤 게임이 다시 흐르는 순간 큰 지도가 CompassShowTime 동안 저절로 펼쳐지고,
        // 안 가 본 방의 흐린 칸이 지금 방에서 가까운 순서로 하나씩 "톡" 나타난다 (발견 연출 RevealScale 재사용).
        private const float CompassShowTime = 1.5f;
        private const float CompassFirstDelay = 0.15f;
        private const float CompassSpread = 0.9f;   // 첫 칸부터 마지막 칸이 나타나기 시작할 때까지
        private bool compassChecked;                // 처음 확인 때 이미 있던 나침반은 연출하지 않는다
        private bool hadCompass;
        private bool compassPending;                // 얻었지만 아직 일시정지(유물 창) 중
        private float compassShowStart = -1f;

        public void Initialize(ChunkStreamer stageStreamer, GameObject player)
        {
            streamer = stageStreamer;
            control = player != null ? player.GetComponent<PlayerControlState>() : null;
            playerObject = player;
            if (streamer == null || !streamer.IsRoomMode) return;
            foreach (var room in streamer.Rooms.Rooms) if (streamer.HasVisited(room.Index)) known.Add(room.Index);
            lastRoom = streamer.CurrentRoom;
            streamer.RoomEntered += OnRoomEntered;
        }

        private void OnDestroy()
        {
            if (streamer != null) streamer.RoomEntered -= OnRoomEntered;
        }

        private void OnRoomEntered(int index)
        {
            if (lastRoom >= 0 && lastRoom != index)
            {
                slideFromRoom = lastRoom;
                slideStart = Time.unscaledTime;
            }
            lastRoom = index;
            if (known.Add(index)) revealStart[index] = Time.unscaledTime;
            else
            {
                pulseRoom = index;
                pulseStart = Time.unscaledTime;
            }
        }

        // 다시 들어간 방 칸 크기 배율 (연출 중이 아니면 1)
        private float PulseScale(int index)
        {
            if (index != pulseRoom) return 1f;
            float t = (Time.unscaledTime - pulseStart) / PulseTime;
            if (t >= 1f || t < 0f) return 1f;
            return 1f + PulseGrow * Mathf.Sin(t * Mathf.PI);
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
            UpdateCompassShow();
            // B가 영창을 치는 중에는 글자 입력과 겹치지 않게 무시한다
            if (control != null && control.AKeyboardBlocked) return;
            // 일시정지 중에는 새로 펼치지 않는다 (펼친 지도는 닫을 수 있게 둔다)
            if (Time.timeScale == 0f && !expanded) return;
            if (Input.GetKeyDown(KeyCode.BackQuote)) SetExpanded(!expanded);
        }

        // 큰 지도 펼치기·접기 (소리 포함). 일시정지 메뉴 Esc는 펼친 지도를 먼저 접는다 (SkillHUD.Pause.cs).
        public void SetExpanded(bool value)
        {
            if (expanded == value) return;
            expanded = value;
            if (value)
            {
                expandedAt = Time.unscaledTime;
                GameAudio.PlaySfx("BookOpen", 0.45f, 0.05f);
            }
            else
            {
                collapsedAt = Time.unscaledTime;
                GameAudio.PlaySfx("PauseClose", 0.45f, 0.05f);
            }
        }

        // 큰 지도 크기 배율: 펼칠 때 ExpandFrom → 1 (살짝 넘침), 접을 때 1 → ExpandFrom. 나침반 자동 펼침은 연출 없이 1.
        private float ExpandScale(bool closing)
        {
            float now = Time.unscaledTime;
            if (closing)
            {
                float c = Mathf.Clamp01((now - collapsedAt) / CollapseTime);
                return Mathf.Lerp(1f, ExpandFrom, c * c);
            }
            if (!expanded) return 1f;
            float t = (now - expandedAt) / ExpandTime;
            if (t >= 1f) return 1f;
            float k = Mathf.Clamp01(t) - 1f;
            const float s = 1.70158f;
            float back = 1f + k * k * ((s + 1f) * k + s); // ease-out-back
            return ExpandFrom + (1f - ExpandFrom) * back;
        }

        // 펼침 정도 0~1 (뒤 화면 어둠에 쓴다)
        private float ExpandAmount(bool closing)
        {
            float now = Time.unscaledTime;
            if (closing) return 1f - Mathf.Clamp01((now - collapsedAt) / CollapseTime);
            if (!expanded) return 1f;
            return Mathf.Clamp01((now - expandedAt) / ExpandTime);
        }

        // 작은 지도 가운데 (펼침·접힘 연출의 기준점)
        private static Vector2 SmallMapCenter =>
            new Vector2(Screen.width - Margin - SmallCols * SmallCell / 2f, Screen.height - Margin - SmallRows * SmallCell / 2f);

        private void UpdateCompassShow()
        {
            bool has = HasCompass();
            if (compassChecked && has && !hadCompass) compassPending = true;
            compassChecked = playerObject != null;
            hadCompass = has;
            if (!compassPending || Time.timeScale == 0f) return;
            compassPending = false;
            compassShowStart = Time.unscaledTime;

            // 안 가 본 방을 지금 방에서 가까운(격자 거리) 순서로 줄 세워 하나씩 나타나게 한다
            var layout = streamer.Rooms;
            var current = layout.Rooms[streamer.CurrentRoom];
            var faint = new System.Collections.Generic.List<RoomData>();
            foreach (var room in layout.Rooms) if (!streamer.HasVisited(room.Index) && room.Index != current.Index && !IsCharted(room.Index)) faint.Add(room); // 보물 지도로 이미 보이던 마을은 다시 숨겼다 띄우지 않는다
            faint.Sort((a, b) => (Mathf.Abs(a.X - current.X) + Mathf.Abs(a.Y - current.Y))
                .CompareTo(Mathf.Abs(b.X - current.X) + Mathf.Abs(b.Y - current.Y)));
            float step = faint.Count > 1 ? Mathf.Min(0.08f, CompassSpread / (faint.Count - 1)) : 0f;
            for (int i = 0; i < faint.Count; i++) revealStart[faint[i].Index] = compassShowStart + CompassFirstDelay + i * step;
        }

        private bool CompassShowing => compassShowStart >= 0f && Time.unscaledTime - compassShowStart < CompassShowTime;

        // 나침반 연출 중 아직 차례가 오지 않은 흐린 칸 (칸·실선 모두 숨긴다)
        private bool WaitingReveal(int index) =>
            revealStart.TryGetValue(index, out float start) && Time.unscaledTime < start;

        private void OnGUI()
        {
            // 일시정지 창 위에 지도를 덮지 않는다
            if (streamer == null || !streamer.IsRoomMode || Time.timeScale == 0f) return;
            var layout = streamer.Rooms;
            var current = layout.Rooms[streamer.CurrentRoom];

            bool closing = !expanded && Time.unscaledTime - collapsedAt < CollapseTime;
            if (expanded || CompassShowing || closing)
            {
                bool animate = expanded || (closing && !CompassShowing);
                float amount = animate ? ExpandAmount(closing) : 1f;
                if (animate && amount > 0f) Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.3f * amount)); // 뒤 게임 화면을 살짝 어둡게
                var screenMatrix = GUI.matrix;
                float scale = animate ? ExpandScale(closing) : 1f;
                if (scale != 1f)
                {
                    // 화면 가운데와 작은 지도 사이 기준점: 작을수록 작은 지도 쪽에서 나오는 것처럼 보인다
                    var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                    GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.Lerp(screenCenter, SmallMapCenter, 0.55f));
                }
                int cols = layout.MaxX - layout.MinX + 1;
                int rows = layout.MaxY - layout.MinY + 1;
                float cell = Mathf.Min(Screen.width * 0.86f / cols, Screen.height * 0.6f / rows, 40f);
                float width = cols * cell, height = rows * cell;
                var area = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
                Background(new Rect(area.x - 16f, area.y - 36f, area.width + 32f, area.height + 72f), new Color(0.05f, 0.04f, 0.1f, 0.85f));
                DrawRooms(layout, current, area, cell, layout.MinX, layout.MaxY);
                if (expanded) DrawPing(CellRect(current.X, current.Y, layout.MinX, layout.MaxY, area, cell));
                EnsureStyle();
                // 나침반이 있으면 제목 줄에 흐린 칸 뜻을 덧붙인다 (탐험가의 나침반)
                string title = CompassShowing && !expanded
                    ? "탐험가의 나침반! 안 가 본 방이 흐리게 보인다"
                    : (HasCompass() ? "지도 (` 키로 닫기) · 흐린 칸 = 안 가 본 방 (나침반)" : "지도 (` 키로 닫기)") + " · " + FoundText(layout);
                GUI.Label(new Rect(area.x - 120f, area.y - 30f, area.width + 240f, 24f), title, legend);
                DrawLegend(new Rect(area.x - 16f, area.yMax + 8f, area.width + 32f, 24f));
                GUI.matrix = screenMatrix;
            }
            else
            {
                float width = SmallCols * SmallCell, height = SmallRows * SmallCell;
                var area = new Rect(Screen.width - width - Margin, Screen.height - height - Margin, width, height);
                Background(new Rect(area.x - 6f, area.y - 6f, width + 12f, height + 12f), new Color(0.05f, 0.04f, 0.1f, 0.6f));
                GUI.BeginGroup(area);
                // 한 칸씩 더 넓게 그리고 그룹이 잘라 낸다: 미끄러지는 동안 가장자리 칸이 뚝 나타나지 않게
                var slide = SmallSlideOffset(layout, current);
                DrawRooms(layout, current, new Rect(-SmallCell + slide.x, -SmallCell + slide.y, width + SmallCell * 2f, height + SmallCell * 2f), SmallCell,
                    current.X - SmallCols / 2 - 1, current.Y + SmallRows / 2 + 1);
                GUI.EndGroup();
                DrawOffMapMarks(layout, current, area);
            }
        }

        // 작은 지도 밖 길잡이 (2026-11-13 작은 지도 다듬기): 지도에 보이는(가 봤거나 나침반·보물 지도로 아는) 보스 방·마을이
        // 작은 지도 범위 밖에 있으면 그쪽 테두리에 그 방 색 작은 세모가 붙어 천천히 깜빡인다. 같은 방향 여럿이어도 방마다 하나.
        private const float OffMarkSize = 7f;

        private void DrawOffMapMarks(RoomLayoutData layout, RoomData current, Rect area)
        {
            if (Event.current.type != EventType.Repaint) return;
            int halfCols = SmallCols / 2, halfRows = SmallRows / 2;
            float blink = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var room in layout.Rooms)
            {
                if (room.Kind != RoomKind.Boss && room.Kind != RoomKind.Village) continue;
                if (!IsShown(room.Index, current)) continue;
                int dx = room.X - current.X, dy = room.Y - current.Y;
                if (Mathf.Abs(dx) <= halfCols && Mathf.Abs(dy) <= halfRows) continue; // 작은 지도 안에 보인다

                // 지도 가운데에서 그 방 쪽으로 그은 선이 테두리와 만나는 곳 (GUI는 y가 아래로)
                var dir = new Vector2(dx, -dy).normalized;
                float sx = (area.width / 2f - OffMarkSize) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
                float sy = (area.height / 2f - OffMarkSize) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                var at = area.center + dir * Mathf.Min(sx, sy);
                var color = room.Kind == RoomKind.Boss ? BossColor : VillageColor;
                DrawArrowMark(at, dir, new Color(color.r, color.g, color.b, blink));
            }
        }

        // dir 쪽을 가리키는 작은 세모 (검은 테두리 + 색). 회전 행렬로 오른쪽(→) 세모를 돌린다.
        private static void DrawArrowMark(Vector2 at, Vector2 dir, Color color)
        {
            var previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, at);
            float s = OffMarkSize;
            // 세모를 가로줄로 쌓는다: 왼쪽(뒤) 넓고 오른쪽(앞) 끝이 뾰족
            for (int i = 0; i < 2; i++)
            {
                float grow = i == 0 ? 1.5f : 0f;
                var c = i == 0 ? Frame : color;
                int steps = 6;
                for (int k = 0; k < steps; k++)
                {
                    float x0 = at.x - s - grow + k * (2f * s + grow * 2f) / steps;
                    float half = (s + grow) * (1f - (float)k / steps);
                    Fill(new Rect(x0, at.y - half, (2f * s + grow * 2f) / steps, half * 2f), c);
                }
            }
            GUI.matrix = previous;
        }

        // 작은 지도를 이전 방 가운데에서 지금 방 가운데로 옮겨 가는 픽셀 차이 (0이면 제자리).
        private Vector2 SmallSlideOffset(RoomLayoutData layout, RoomData current)
        {
            if (slideFromRoom < 0 || slideFromRoom >= layout.Rooms.Count) return Vector2.zero;
            float t = (Time.unscaledTime - slideStart) / SlideTime;
            if (t >= 1f || t < 0f) return Vector2.zero;
            var from = layout.Rooms[slideFromRoom];
            int dx = current.X - from.X, dy = current.Y - from.Y;
            if (Mathf.Abs(dx) > 1 || Mathf.Abs(dy) > 1) return Vector2.zero; // 한 칸 넓게만 그리므로 더 멀면 빈 줄이 보인다
            float left = (1f - t) * (1f - t); // ease-out: 남은 거리
            // 처음엔 이전 방이 가운데에 있다: x는 오른쪽으로 dx칸, y는 GUI가 아래로라 위로 dy칸 밀려 있던 셈
            return new Vector2(dx * SmallCell, -dy * SmallCell) * left;
        }

        // 큰 지도 제목 줄 끝: "찾은 방 N/M" (다 찾았으면 금색). 일시정지 창 탐험 줄과 같은 셈.
        private string FoundText(RoomLayoutData layout)
        {
            int seen = 0;
            foreach (var room in layout.Rooms) if (streamer.HasVisited(room.Index)) seen++;
            int total = layout.Rooms.Count;
            return seen >= total ? $"<color=#ffd84d>찾은 방 {seen}/{total}</color>" : $"찾은 방 {seen}/{total}";
        }

        // 지금 방 칸에서 퍼지는 하늘색 고리 (PingPeriod마다 하나, PingTime 동안 커지며 흐려진다).
        private static void DrawPing(Rect cellRect)
        {
            if (Event.current.type != EventType.Repaint) return;
            float t = (Time.unscaledTime % PingPeriod) / PingTime;
            if (t >= 1f) return;
            float grow = cellRect.width * 1.1f * (1f - (1f - t) * (1f - t)); // ease-out
            var r = new Rect(cellRect.x - grow / 2f, cellRect.y - grow / 2f, cellRect.width + grow, cellRect.height + grow);
            var color = new Color(RestColor.r, RestColor.g, RestColor.b, 0.85f * (1f - t));
            const float w = 2f;
            Fill(new Rect(r.x, r.y, r.width, w), color);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), color);
            Fill(new Rect(r.x, r.y, w, r.height), color);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), color);
        }

        // 일시정지 창 왼쪽에 그리는 전체 지도 (SkillHUD.Pause.cs가 자기 OnGUI 안에서 부른다 — 이 컴포넌트의 OnGUI는 일시정지 중 쉰다).
        // 큰 지도와 같은 규칙(가 본 방·지금 방 깜빡임·나침반 흐린 칸·체크·자물쇠·동전 점)으로, rect 안에 맞춰 칸 크기를 정한다.
        public void DrawPauseMap(Rect rect)
        {
            if (streamer == null || !streamer.IsRoomMode) return;
            var layout = streamer.Rooms;
            var current = layout.Rooms[streamer.CurrentRoom];
            EnsureStyle();
            Background(rect, new Color(0.05f, 0.04f, 0.1f, 0.85f));
            GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 24f), "지도", legend);

            const float top = 42f, side = 16f, bottom = 16f;
            int cols = layout.MaxX - layout.MinX + 1;
            int rows = layout.MaxY - layout.MinY + 1;
            float cell = Mathf.Min((rect.width - side * 2f) / cols, (rect.height - top - bottom) / rows, 40f);
            float width = cols * cell, height = rows * cell;
            var area = new Rect(rect.center.x - width / 2f, rect.y + top + (rect.height - top - bottom - height) / 2f, width, height);
            DrawRooms(layout, current, area, cell, layout.MinX, layout.MaxY);
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
                    Mathf.Max(c.x, end.x) + link / 2f, Mathf.Max(c.y, end.y) + link / 2f), LinkColor(layout, line));
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
                    float faintReveal = RevealScale(room.Index); // 나침반 획득 연출 (차례 전 칸은 LookOf가 숨긴다)
                    if (faintReveal != 1f)
                    {
                        var faintCenter = body.center;
                        body.size *= faintReveal;
                        body.center = faintCenter;
                    }
                    DrawFaintRoom(room, body);
                    continue;
                }
                float reveal = RevealScale(room.Index) * PulseScale(room.Index);
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

                // 돌파한 전투·정예·중간보스 방: 방 칸 오른쪽 아래에 작은 초록 체크 (보스 방은 클리어 화면이 대신한다)
                if (art.Check != null && room.Kind != RoomKind.Boss && streamer.HasClearedArena(room.Index))
                {
                    float s = body.width * 0.55f;
                    var previous = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(body.xMax - s * 0.75f, body.yMax - s * 0.75f, s, s), art.Check, ScaleMode.ScaleToFit, true);
                    GUI.color = previous;
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

                // 안 주운 동전 무더기가 남은 방: 방 칸 오른쪽 위에 작은 금색 점 (주우면 사라짐)
                if (streamer.HasCoinsLeft(room.Index))
                {
                    float d = Mathf.Max(4f, body.width * 0.26f);
                    float o = Mathf.Max(1f, d * 0.2f);
                    var dot = new Rect(body.xMax - 1f - d, body.y + 1f, d, d);
                    if (art.Tile != null)
                    {
                        DrawTile(new Rect(dot.x - o, dot.y - o, dot.width + o * 2f, dot.height + o * 2f), Frame);
                        DrawTile(dot, CoinColor);
                    }
                    else
                    {
                        Fill(new Rect(dot.x - 1f, dot.y - 1f, dot.width + 2f, dot.height + 2f), Frame);
                        Fill(dot, CoinColor);
                    }
                }

                // 방 이벤트 점 (2026-11-14): 방 칸 왼쪽 아래 (오른쪽 아래는 체크·자물쇠, 위는 룬·동전). 들어가 본 방만.
                // 저주받은 상자(유물을 아직 안 골랐으면 보라) · 보물 고블린(살아 있는 동안 밝은 금색으로 깜빡임)
                if (streamer.HasVisited(room.Index))
                {
                    Color? eventColor = null;
                    if (streamer.HasTreasureGoblin(room.Index))
                        eventColor = new Color(GoblinColor.r, GoblinColor.g, GoblinColor.b, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)));
                    else if (streamer.HasCursedChestLeft(room.Index)) eventColor = CurseColor;
                    if (eventColor.HasValue)
                    {
                        float d = Mathf.Max(4f, body.width * 0.26f);
                        float o = Mathf.Max(1f, d * 0.2f);
                        var dot = new Rect(body.x + 1f, body.yMax - 1f - d, d, d);
                        if (art.Tile != null)
                        {
                            DrawTile(new Rect(dot.x - o, dot.y - o, dot.width + o * 2f, dot.height + o * 2f), Frame);
                            DrawTile(dot, eventColor.Value);
                        }
                        else
                        {
                            Fill(new Rect(dot.x - 1f, dot.y - 1f, dot.width + 2f, dot.height + 2f), Frame);
                            Fill(dot, eventColor.Value);
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
            public Texture2D Boss, MidBoss, Elite, Treasure, Rest, Start, Lock, Check, Village;
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
            art.Check = Resources.Load<Texture2D>("UI/HUD/MapCheck");
            art.Village = Resources.Load<Texture2D>("UI/HUD/MapHouse"); // NPC 마을 (Tools/ArtGen/generate_hud.py)
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
                case RoomKind.Village: return art.Village;
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
                    "<color=#ffd84d>■</color> 보물  <color=#7ff6ff>■</color> 샘  <color=#99ff73>■</color> 시작  <color=#ffa07a>■</color> 마을  <color=#ffd140>●</color> 동전  <color=#a866fa>●</color> 저주 상자", legend);
                return;
            }

            var items = new[]
            {
                (art.Boss, "보스"), (art.MidBoss, "중간보스"), (art.Elite, "정예"),
                (art.Treasure, "보물"), (art.Rest, "샘"), (art.Start, "시작"), (art.Village, "마을"), (art.Check, "돌파")
            };
            int count = items.Length + 2; // 마지막 두 칸은 동전 점·저주 상자 점 (그림이 아니라 색 점)
            float itemWidth = Mathf.Min(92f, rect.width / count); // 좁은 지도에서도 패널 안에
            float x = rect.center.x - itemWidth * count / 2f;
            foreach (var (icon, name) in items)
            {
                if (icon != null) GUI.DrawTexture(new Rect(x, rect.y + 2f, 20f, 20f), icon, ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(x + 22f, rect.y, itemWidth - 22f, rect.height), name, legendLeft);
                x += itemWidth;
            }
            var coinDot = new Rect(x + 5f, rect.y + 7f, 10f, 10f);
            if (art.Tile != null)
            {
                DrawTile(new Rect(coinDot.x - 2f, coinDot.y - 2f, 14f, 14f), Frame);
                DrawTile(coinDot, CoinColor);
            }
            else Fill(coinDot, CoinColor);
            GUI.Label(new Rect(x + 22f, rect.y, itemWidth - 22f, rect.height), "동전", legendLeft);
            x += itemWidth;
            var curseDot = new Rect(x + 5f, rect.y + 7f, 10f, 10f);
            if (art.Tile != null)
            {
                DrawTile(new Rect(curseDot.x - 2f, curseDot.y - 2f, 14f, 14f), Frame);
                DrawTile(curseDot, CurseColor);
            }
            else Fill(curseDot, CurseColor);
            GUI.Label(new Rect(x + 22f, rect.y, itemWidth - 22f, rect.height), "저주", legendLeft);
        }

        private bool IsShown(int index, RoomData current) => LookOf(index, current) != RoomLook.Hidden;

        private RoomLook LookOf(int index, RoomData current)
        {
            var look = MinimapRules.Look(streamer.HasVisited(index), index == current.Index, HasCompass(), IsCharted(index));
            return look == RoomLook.Faint && WaitingReveal(index) ? RoomLook.Hidden : look;
        }

        // 보물 지도: 마을 방은 안 가 봤어도 흐린 칸 + 흐린 집 아이콘 (나침반 획득 연출은 없음)
        private bool IsCharted(int index)
        {
            if (relics == null && playerObject != null) relics = playerObject.GetComponent<PlayerRelics>();
            if (relics == null || !relics.Has(RelicId.TreasureMap)) return false;
            var rooms = streamer.Rooms.Rooms;
            return index >= 0 && index < rooms.Count && rooms[index].Kind == RoomKind.Village;
        }

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
                case RoomKind.Village: return VillageColor;
                default: return null;
            }
        }

        // 보스·중간보스 방으로 들어가는 통로는 그 방 색으로 (안 가 봤어도 "저기가 보스 입구"를 알 수 있게, 2026-11-12 플레이테스트)
        private static Color LinkColor(RoomLayoutData layout, MapLinkLine line)
        {
            var a = line.From;
            var b = layout.Neighbor(line.From, line.Dir);
            if ((a != null && a.Kind == RoomKind.Boss) || (b != null && b.Kind == RoomKind.Boss)) return BossColor;
            if ((a != null && a.Kind == RoomKind.MidBoss) || (b != null && b.Kind == RoomKind.MidBoss)) return MidBossColor;
            return Link;
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
