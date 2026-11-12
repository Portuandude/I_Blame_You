using System;
using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 기능 테스트 모드 도구 (2026-11-12 플레이테스트 피드백: 7·8·11·19번이 일반 판에서는 "테스트 불가").
    // 맵은 적 없는 시험용 외길(FeatureTestStage). F키로 몬스터를 골라 부르고, 유물을 골라 장착·해제하고,
    // 협동 기능(B 영창·A 방어막 순서 입력, 협동 봉인문, 스위치 발판, "누구 탓?" 도장)을 바로 시험한다.
    // F1~F4 치트(DevCheats)는 이 모드에서 개발자 모드가 꺼져 있어도 쓴다.
    public class FeatureTestTools : MonoBehaviour
    {
        private static readonly StageEnemy[] Monsters = (StageEnemy[])Enum.GetValues(typeof(StageEnemy));

        // 유물 해제는 효과를 되돌리는 대신 시험 맵을 다시 만들고 남은 유물만 다시 단다 (유물 효과는 얻을 때 한 번 적용되므로).
        // 다시 만드는 동안 고른 유물·몬스터·미니게임 설정을 들고 있으려고 static으로 둔다.
        private static readonly List<RelicId> keptRelics = new List<RelicId>();
        private static bool rebuilding;
        private static int monsterIndex;
        private static bool? menuDevMode; // 시험 중 F5로 바꾸기 전 시작 화면 설정 (나갈 때 되돌린다)

        private GameObject player;
        private ChunkStreamer streamer;
        private StageData stage;
        private Func<StageEnemy, Vector3, Transform, HealthSystem> spawner;
        private Action<bool> restart;
        private Transform enemyRoot;
        private bool helpOpen = true;
        private bool relicWindowOpen;
        private Vector2 relicScroll;
        private int area;
        private GUIStyle box;
        private GUIStyle line;
        private GUIStyle cell;

        public void Initialize(GameObject playerObject, ChunkStreamer stageStreamer, StageData stageData,
            Func<StageEnemy, Vector3, Transform, HealthSystem> enemySpawner, Action<bool> restartRun)
        {
            player = playerObject;
            streamer = stageStreamer;
            stage = stageData;
            spawner = enemySpawner;
            restart = restartRun;
            enemyRoot = new GameObject("TestEnemies").transform;
            enemyRoot.SetParent(streamer.transform, false); // 다시 시작할 때 맵과 같이 치워진다

            if (!rebuilding)
            {
                keptRelics.Clear();
                menuDevMode = null;
            }
            rebuilding = false;

            var relics = PlayerRelics.For(player);
            foreach (var id in keptRelics) relics.Add(id);
        }

        private void OnDestroy()
        {
            if (rebuilding) return;
            if (menuDevMode.HasValue) GameSession.DevMode = menuDevMode.Value; // 시작 화면 설정을 시험 중 바꾼 값으로 덮지 않는다
            menuDevMode = null;
        }

        private void Update()
        {
            if (player == null) return;
            if (Input.GetKeyDown(KeyCode.F6)) helpOpen = !helpOpen;
            if (Input.GetKeyDown(KeyCode.F5)) ToggleMinigames();
            if (Input.GetKeyDown(KeyCode.F7)) monsterIndex = (monsterIndex + 1) % Monsters.Length;
            if (Input.GetKeyDown(KeyCode.F8)) SpawnMonster();
            if (Input.GetKeyDown(KeyCode.F9)) relicWindowOpen = !relicWindowOpen;
            if (Input.GetKeyDown(KeyCode.F10)) JumpToArea((area + 1) % FeatureTestStage.Order.Length);
            if (Input.GetKeyDown(KeyCode.F11)) StageEvents.RaiseCleared(); // 결과 창 + "누구 탓?" 도장 (판이 끝남, R로 다시)
            if (relicWindowOpen && Input.GetKeyDown(KeyCode.Escape)) relicWindowOpen = false;
        }

        // 개발자 모드(숫자키 즉시 발동) ↔ 일반 모드(B 영창 타이핑, A 방어막 W/A/S/D 순서 입력). 모두 GameSession.DevMode를 매번 읽는다.
        private void ToggleMinigames()
        {
            if (!menuDevMode.HasValue) menuDevMode = GameSession.DevMode;
            GameSession.DevMode = !GameSession.DevMode;
            StageEvents.PostNotice(GameSession.DevMode ? "개발자 모드: 숫자키 즉시 발동" : "일반 모드: B 영창 타이핑 · A 방어막 W/A/S/D 순서");
        }

        private void SpawnMonster()
        {
            var kind = Monsters[monsterIndex];
            var movement = player.GetComponent<PlayerMovement>();
            float facing = movement != null ? movement.Facing : 1f;
            bool big = kind == StageEnemy.SlimeKing || kind == StageEnemy.SlimeKnightFire || kind == StageEnemy.SlimeKnightWater;
            var position = player.transform.position + new Vector3(facing * (big ? 6f : 4f), big ? 2.5f : 1f, 0f);
            var health = spawner(kind, position, enemyRoot);
            StageEvents.PostNotice(health != null ? $"소환: {MonsterName(kind)}" : $"소환 실패: {MonsterName(kind)}");
        }

        // 구역 i의 왼쪽(입구) 바닥 위로 옮긴다. 떨어지는 속도는 지운다.
        private void JumpToArea(int index)
        {
            area = index;
            const int column = 3;
            float x = index * ChunkData.Width + column + 0.5f; // 외길 모드: 청크 i의 원점 = (i × Width, 0)
            float y = stage.Chunks[index].SurfaceAt(column) + 1f;
            player.transform.position = new Vector3(x, y, player.transform.position.z);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            StageEvents.PostNotice($"구역 {index + 1}: {FeatureTestStage.Names[index]}");
        }

        private void ToggleRelic(RelicId id)
        {
            var relics = PlayerRelics.For(player);
            if (!relics.Has(id))
            {
                relics.Add(id);
                keptRelics.Add(id);
                return;
            }

            keptRelics.Remove(id);
            relicWindowOpen = false;
            rebuilding = true;
            StageEvents.PostNotice($"해제: {RelicBook.Name(id)} — 시험 맵을 다시 만든다");
            restart?.Invoke(false);
        }

        private static string MonsterName(StageEnemy kind)
        {
            switch (kind)
            {
                case StageEnemy.Slime: return "슬라임";
                case StageEnemy.Goblin: return "고블린";
                case StageEnemy.GoblinArcher: return "고블린 궁수";
                case StageEnemy.GoblinSoldier: return "고블린 병사";
                case StageEnemy.MotherSlime: return "엄마 슬라임 (정예)";
                case StageEnemy.GoblinWarrior: return "고블린 전사 (정예)";
                case StageEnemy.SlimeKing: return "슬라임 왕 (보스)";
                case StageEnemy.SlimeKnightFire: return "슬라임 기사 불 (중간보스)";
                case StageEnemy.SlimeKnightWater: return "슬라임 기사 물 (중간보스)";
                default: return kind.ToString();
            }
        }

        private void EnsureStyles()
        {
            if (box != null) return;
            box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            line = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true };
            line.normal.textColor = new Color(1f, 0.96f, 0.86f);
            cell = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        }

        private void OnGUI()
        {
            if (player == null) return;
            EnsureStyles();
            if (helpOpen) DrawHelp();
            if (relicWindowOpen) DrawRelicWindow();
        }

        private void DrawHelp()
        {
            string mode = GameSession.DevMode ? "개발자 모드 (숫자키 즉시)" : "일반 모드 (영창·순서 입력)";
            string text =
                "<b>기능 테스트</b>  (F6 접기)\n" +
                $"F7 몬스터 고르기: <color=#ffd84d>{MonsterName(Monsters[monsterIndex])}</color>   F8 앞에 소환\n" +
                $"F9 유물 장착·해제 ({PlayerRelics.For(player).Owned.Count}개)\n" +
                $"F10 다음 구역: {FeatureTestStage.Names[(area + 1) % FeatureTestStage.Order.Length]}\n" +
                $"F5 미니게임 전환: {mode}\n" +
                "F11 결과 창 (누구 탓? 도장)\n" +
                "F1 무적 · F2 마나 무한 · F3 적 처치 · F4 회복";
            var rect = new Rect(Screen.width - 330f, 64f, 316f, 170f);
            GUI.Box(rect, GUIContent.none, box);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 12f), text, line);
        }

        private void DrawRelicWindow()
        {
            var all = RelicBook.All;
            var relics = PlayerRelics.For(player);
            float width = Mathf.Min(640f, Screen.width - 40f);
            float height = Mathf.Min(520f, Screen.height - 40f);
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(rect, GUIContent.none, box);
            GUI.Box(rect, GUIContent.none, box);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 40f),
                "<b>유물 장착·해제</b> — 누르면 장착, 장착한 것(★)을 누르면 해제 (해제하면 시험 맵을 다시 만든다). F9/Esc 닫기", line);

            const int columns = 2;
            float cellWidth = (rect.width - 36f) / columns;
            const float cellHeight = 34f;
            int rows = (all.Length + columns - 1) / columns;
            var view = new Rect(rect.x + 12f, rect.y + 52f, rect.width - 24f, rect.height - 64f);
            relicScroll = GUI.BeginScrollView(view, relicScroll, new Rect(0f, 0f, view.width - 18f, rows * (cellHeight + 4f)));
            for (int i = 0; i < all.Length; i++)
            {
                var id = all[i];
                bool owned = relics.Has(id);
                var r = new Rect((i % columns) * (cellWidth + 4f), (i / columns) * (cellHeight + 4f), cellWidth, cellHeight);
                if (GUI.Button(r, new GUIContent((owned ? "★ " : "   ") + RelicBook.Name(id), RelicBook.Description(id)), cell))
                {
                    ToggleRelic(id);
                    break; // 해제하면 맵을 다시 만들므로 이번 프레임은 그만 그린다
                }
            }
            GUI.EndScrollView();
            if (!string.IsNullOrEmpty(GUI.tooltip))
                GUI.Label(new Rect(rect.x + 12f, rect.yMax - 30f, rect.width - 24f, 24f), GUI.tooltip, line);
        }
    }
}
