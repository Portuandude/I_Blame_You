using System;
using System.Collections.Generic;
using IBlameYou.Player;
using IBlameYou.Systems;
using UnityEngine;

namespace IBlameYou.Core
{
    // 기능 테스트 모드 도구 (2026-11-12 플레이테스트 피드백: 7·8·11·19번이 일반 판에서는 "테스트 불가").
    // 맵은 적 없는 시험용 외길(FeatureTestStage). F키로 몬스터를 골라 부르고, 유물을 골라 장착·해제하고,
    // 협동 기능(B 영창·A 방어막 순서 입력, 협동 봉인문, 스위치 발판, 결과 창)을 바로 시험한다.
    // F1~F4 치트(DevCheats)는 이 모드에서 개발자 모드가 꺼져 있어도 쓴다.
    public class FeatureTestTools : MonoBehaviour
    {
        private static readonly StageEnemy[] Monsters = (StageEnemy[])Enum.GetValues(typeof(StageEnemy));

        // F7 목록에서 몬스터 뒤에 오는 방 이벤트 시험 (2026-11-14): 시험 맵은 방 격자가 아니라서 매복·저주받은 상자·궁수 탑이 저절로 나오지 않는다.
        private enum TestEvent { Ambush, CursedChest, ArcherTower }
        private static readonly TestEvent[] Events = (TestEvent[])Enum.GetValues(typeof(TestEvent));
        private static int PickCount => Monsters.Length + Events.Length;
        private ArenaZone ambushZone;
        private int ambushCount;
        private int cursedCount;
        private int towerCount;

        // 유물 해제는 효과를 되돌리는 대신 시험 맵을 다시 만들고 남은 유물만 다시 단다 (유물 효과는 얻을 때 한 번 적용되므로).
        // 다시 만드는 동안 고른 유물·몬스터·미니게임 설정을 들고 있으려고 static으로 둔다.
        private static readonly List<RelicId> keptRelics = new List<RelicId>();
        private static readonly List<EquipmentItem> keptEquipment = new List<EquipmentItem>(); // F12 장비 (맵을 다시 만들어도 다시 낀다)
        private static int equipGrade; // F12 창에서 새로 낄 장비의 수식어 등급
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
        private bool equipWindowOpen;
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
                keptEquipment.Clear();
                menuDevMode = null;
            }
            rebuilding = false;

            var relics = PlayerRelics.For(player);
            foreach (var id in keptRelics) relics.Add(id);
            var equipment = PlayerEquipment.For(player);
            foreach (var item in keptEquipment) equipment.Equip(new EquipmentItem(item.Id, item.Grade));
        }

        private void OnDestroy()
        {
            if (rebuilding) return;
            if (menuDevMode.HasValue) GameSession.DevMode = menuDevMode.Value; // 시작 화면 설정을 시험 중 바꾼 값으로 덮지 않는다
            RoleInput.Focus = ControlFocus.Both; // A/B 전환은 시험 맵 안에서만
            menuDevMode = null;
        }

        private void Update()
        {
            if (player == null) return;
            if (Input.GetKeyDown(KeyCode.F6)) helpOpen = !helpOpen;
            if (Input.GetKeyDown(KeyCode.F5)) ToggleMinigames();
            var control = player.GetComponent<PlayerControlState>();
            if (Input.GetKeyDown(KeyCode.BackQuote) && (control == null || !control.ChantActive)) CycleFocus(); // 영창 입력 칸에 글자를 치는 중이면 바꾸지 않는다
            if (Input.GetKeyDown(KeyCode.F7)) monsterIndex = (monsterIndex + 1) % PickCount;
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (monsterIndex < Monsters.Length) SpawnMonster();
                else RunEvent(Events[monsterIndex - Monsters.Length]);
            }
            if (Input.GetKeyDown(KeyCode.F9)) relicWindowOpen = !relicWindowOpen;
            if (Input.GetKeyDown(KeyCode.F10)) JumpToArea((area + 1) % FeatureTestStage.Order.Length);
            if (Input.GetKeyDown(KeyCode.F12)) equipWindowOpen = !equipWindowOpen;
            if (Input.GetKeyDown(KeyCode.F11)) StageEvents.RaiseCleared(); // 결과 창 (판이 끝남, R로 다시)
            if (relicWindowOpen && Input.GetKeyDown(KeyCode.Escape)) relicWindowOpen = false;
            if (equipWindowOpen && Input.GetKeyDown(KeyCode.Escape)) equipWindowOpen = false;
        }

        // A/B 전환 (2026-11-14 사람 결정 ④): 둘 다 → A만 → B만 → 둘 다. 한 역할만 조작해 보며 그 사람이 할 일을 시험한다 (RoleInput.Focus).
        private static void CycleFocus()
        {
            RoleInput.Focus = RoleInput.Focus == ControlFocus.Both ? ControlFocus.A
                : RoleInput.Focus == ControlFocus.A ? ControlFocus.B : ControlFocus.Both;
            StageEvents.PostNotice("조작: " + FocusName(RoleInput.Focus));
            GameAudio.PlaySfx("UiClick", 0.6f, 0f);
        }

        private static string FocusName(ControlFocus focus) =>
            focus == ControlFocus.A ? "A만 (이동·점프·대쉬·순간이동·방어막)"
            : focus == ControlFocus.B ? "B만 (마우스 공격·공격 마법)" : "A·B 둘 다";

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

        private void RunEvent(TestEvent kind)
        {
            switch (kind)
            {
                case TestEvent.Ambush: StartAmbush(); break;
                case TestEvent.CursedChest: PlaceCursedChest(); break;
                case TestEvent.ArcherTower: StartArcherTower(); break;
            }
        }

        private static string EventName(TestEvent kind) =>
            kind == TestEvent.Ambush ? "이벤트: 매복 (지금 구역)"
            : kind == TestEvent.CursedChest ? "이벤트: 저주받은 상자 (앞에)" : "이벤트: 궁수 탑 (버섯 탑 방)";

        private string PickName() =>
            monsterIndex < Monsters.Length ? MonsterName(Monsters[monsterIndex]) : EventName(Events[monsterIndex - Monsters.Length]);

        // 외길 모드: 청크 i의 원점 = (i × Width, 0). 월드 x가 있는 청크 번호 (맵 밖이면 -1).
        private int ChunkAt(float x)
        {
            int index = Mathf.FloorToInt(x / ChunkData.Width);
            return index >= 0 && index < stage.Chunks.Count ? index : -1;
        }

        // 월드 x 열의 바닥 높이(서는 칸 y). 맵 밖이면 null.
        private float? GroundAt(float x)
        {
            int index = ChunkAt(x);
            if (index < 0) return null;
            int column = Mathf.Clamp(Mathf.FloorToInt(x) - index * ChunkData.Width, 0, ChunkData.Width - 1);
            return stage.Chunks[index].SurfaceAt(column);
        }

        // 매복 시험: 지금 구역 전체를 전투 구역으로 만들고 매복으로 연다 (ArenaZone.Ambush — 경고음·붉은 가장자리·천장 먼지 뒤 적 셋이 위에서 떨어진다. 두 번째는 땅속, 세 번째는 섞인 매복).
        // 구역 안에 서 있으니 바로 문이 닫힌다. 다 쓰러뜨리면 문이 열리고, 다시 누르면 새로 연다.
        private static readonly StageEnemy[] AmbushEnemies = { StageEnemy.Slime, StageEnemy.Goblin, StageEnemy.GoblinArcher };
        private static readonly int[] AmbushColumns = { 9, 16, 23 };
        private const float AmbushEdge = 3.5f; // ArenaZone 트리거는 양 끝 3칸 안쪽 + 몸 반 폭 — 그보다 끝이면 문이 닫히지 않는다

        private void StartAmbush()
        {
            if (ambushZone != null && ambushZone.IsLocked)
            {
                StageEvents.PostNotice("매복 싸움 중 (F3으로 적 처치)");
                return;
            }

            int index = ChunkAt(player.transform.position.x);
            float left = index * ChunkData.Width;
            float local = player.transform.position.x - left;
            if (index < 0 || local < AmbushEdge || local > ChunkData.Width - AmbushEdge)
            {
                StageEvents.PostNotice("매복 시험: 구역 가운데 쪽에서 눌러 주세요");
                return;
            }

            if (ambushZone != null) Destroy(ambushZone.gameObject);
            var chunk = stage.Chunks[index];
            int ground = ChunkData.Height;
            for (int x = 0; x < ChunkData.Width; x++) ground = Mathf.Min(ground, chunk.SurfaceAt(x));
            var spawns = new List<ArenaZone.Spawn>();
            for (int i = 0; i < AmbushEnemies.Length; i++)
            {
                int column = AmbushColumns[i];
                spawns.Add(new ArenaZone.Spawn
                {
                    Kind = AmbushEnemies[i],
                    Position = new Vector3(left + column + 0.5f, chunk.SurfaceAt(column) + ChunkStreamer.SpawnLift, 0f)
                });
            }

            ambushZone = ArenaZone.Create(enemyRoot, left, left + ChunkData.Width, ground, spawns, false, spawner,
                () => StageEvents.PostNotice("매복 시험 끝 — F8로 다시"));
            ambushZone.Ambush = true;
            ambushZone.Burrow = ambushCount % 3 != 0; // 차례로: 천장에서 떨어짐 → 땅속에서 튀어나옴 → 섞임(땅속·천장 번갈아)
            ambushZone.Mixed = ambushCount % 3 == 2;
            ambushCount++;
        }

        // 저주받은 상자 시험: 바라보는 쪽 4칸 앞 바닥에 상자. 닿으면 정예(엄마 슬라임·고블린 전사 번갈아)가 깨어나고, 쓰러뜨리면 유물 2개 중 고르기 상자.
        private void PlaceCursedChest()
        {
            var movement = player.GetComponent<PlayerMovement>();
            float facing = movement != null ? movement.Facing : 1f;
            float x = Mathf.Floor(player.transform.position.x + facing * 4f) + 0.5f;
            var ground = GroundAt(x);
            if (!ground.HasValue)
            {
                StageEvents.PostNotice("저주받은 상자: 놓을 자리가 없다");
                return;
            }

            var position = new Vector2(x, ground.Value);
            // 세 번째마다 변형 "쫄몹 무리" (엄마 슬라임 → 고블린 전사 → 무리 → ...)
            bool swarm = cursedCount % 3 == 2;
            var elite = cursedCount % 3 == 0 ? StageEnemy.MotherSlime : StageEnemy.GoblinWarrior;
            int offerIndex = 1000 + cursedCount; // 유물 후보가 매번 달라지게
            cursedCount++;
            CursedChest chest = null;
            chest = CursedChest.Create(enemyRoot, position, () =>
            {
                if (swarm) AwakenCursedSwarm(chest, position, offerIndex);
                else AwakenCursedElite(chest, position, elite, offerIndex);
            }, swarm);
            StageEvents.PostNotice(swarm ? "저주받은 상자를 놓았다 (닿으면 쫄몹 무리)" : $"저주받은 상자를 놓았다 (닿으면 {MonsterName(elite)})");
        }

        // 무리 중 아직 살아 있는 것이 있나 (쓰러지지 않고 치워진 것 — 낭떠러지 등 — 은 셈하지 않는다)
        private static bool AnyAlive(List<HealthSystem> group)
        {
            foreach (var g in group)
                if (g != null && !g.IsDead) return true;
            return false;
        }

        private void AwakenCursedSwarm(CursedChest chest, Vector2 chestPosition, int offerIndex)
        {
            if (chest == null) return;
            var kinds = CursedChestRule.SwarmFor(stage.Seed, offerIndex);
            var spawned = new List<HealthSystem>();
            bool broken = false;
            for (int i = 0; i < kinds.Length; i++)
            {
                float x = chestPosition.x + CursedChestRule.SwarmOffsets[i];
                var ground = GroundAt(x) ?? chestPosition.y;
                var position = new Vector3(x, ground + ChunkStreamer.SpawnLift, 0f);
                EffectLibrary.PlayOneShot(position + Vector3.up * 0.4f, EffectLibrary.TeleportFrames, 20f, new Color(0.66f, 0.4f, 0.98f, 0.7f), 1f);
                var health = spawner(kinds[i], position, enemyRoot);
                if (health == null) continue;
                spawned.Add(health);
                bool down = false;
                health.HealthChanged += (current, max) =>
                {
                    if (current > 0f || down) return;
                    down = true;
                    if (AnyAlive(spawned) || broken || chest == null) return;
                    broken = true;
                    int offer = chest.Break();
                    RelicChest.Create(enemyRoot, chestPosition, stage.Seed, offerIndex, true, null, offer);
                };
            }
            if (spawned.Count == 0) return;
            GameAudio.PlaySfx("Summon", 0.7f);
            chest.Watch(spawned.ToArray());
        }

        private void AwakenCursedElite(CursedChest chest, Vector2 chestPosition, StageEnemy elite, int offerIndex)
        {
            if (chest == null) return;
            float side = player.transform.position.x < chestPosition.x ? 1f : -1f;
            float x = chestPosition.x + side * 4f;
            var ground = GroundAt(x) ?? chestPosition.y;
            var position = new Vector3(x, ground + ChunkStreamer.SpawnLift, 0f);
            EffectLibrary.PlayOneShot(position + Vector3.up * 0.6f, EffectLibrary.TeleportFrames, 20f, new Color(0.66f, 0.4f, 0.98f, 0.8f), 1.4f);
            GameAudio.PlaySfx("Summon", 0.7f);
            var health = spawner(elite, position, enemyRoot);
            if (health == null) return;
            chest.Watch(health);
            bool broken = false;
            health.HealthChanged += (current, max) =>
            {
                if (current > 0f || broken || chest == null) return;
                broken = true;
                int offer = chest.Break();
                RelicChest.Create(enemyRoot, chestPosition, stage.Seed, offerIndex, true, null, offer);
            };
        }

        // 궁수 탑 시험: 버섯 탑 방으로 옮겨 P1 선반과 반대쪽 저격 선반에 고블린 궁수를 하나씩 세운다 (세로 방 궁수 탑처럼 위에서 둘이 쏜다. 가시는 없음).
        // 차례로: 궁수 둘 → 궁수 둘 + 바닥 고블린 파수꾼 (깊은 궁수 탑, RoomShaper.ArcherTowerGuardDifficulty 이상).
        private const int TowerGuardColumn = 14; // P1 선반(4~9열)과 저격 선반(19~22열) 사이 바닥 (버섯은 6·8열)
        private void StartArcherTower()
        {
            int index = FeatureTestStage.TowerArea;
            var chunk = stage.Chunks[index];
            int stand1 = chunk.SurfaceAt(RoomShaper.TowerShelf1X0) + RoomShaper.TowerStep;
            if (chunk.Get(RoomShaper.TowerShelf1X0, stand1 - 1) != StageTile.Platform)
            {
                StageEvents.PostNotice("궁수 탑: 버섯 탑 선반이 없다");
                return;
            }

            JumpToArea(index);
            float left = index * ChunkData.Width;
            float sniper = (RoomShaper.TowerSniperX0 + RoomShaper.TowerSniperX1 + 1) / 2f;
            float shelf = (RoomShaper.TowerShelf1X0 + RoomShaper.TowerShelf1X1 + 1) / 2f;
            spawner(StageEnemy.GoblinArcher, new Vector3(left + sniper, stand1 + ChunkStreamer.SpawnLift, 0f), enemyRoot);
            spawner(StageEnemy.GoblinArcher, new Vector3(left + shelf, stand1 + ChunkStreamer.SpawnLift, 0f), enemyRoot);
            bool guard = towerCount % 2 == 1;
            towerCount++;
            if (guard)
            {
                float floor = chunk.SurfaceAt(TowerGuardColumn);
                spawner(StageEnemy.Goblin, new Vector3(left + TowerGuardColumn + 0.5f, floor + ChunkStreamer.SpawnLift, 0f), enemyRoot);
            }
            StageEvents.PostNotice(guard ? "깊은 궁수 탑 시험: 선반 위 궁수 둘 + 바닥 파수꾼" : "궁수 탑 시험: 선반 위 궁수 둘 (다시 누르면 파수꾼도)");
        }

        // 구역 i의 왼쪽(입구) 바닥 위로 옮긴다. 떨어지는 속도는 지운다.
        private void JumpToArea(int index)
        {
            // 매복 싸움 중에 다른 구역으로 가면 그 매복을 치운다 (문·카메라 잠금이 남지 않게 — ArenaZone.OnDestroy가 카메라를 푼다)
            if (ambushZone != null && ambushZone.IsLocked) Destroy(ambushZone.gameObject);
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
                case StageEnemy.TreasureGoblin: return "보물 고블린 (10초)";
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
            if (RoleInput.Focus != ControlFocus.Both) DrawFocusBadge();
            if (relicWindowOpen) DrawRelicWindow();
            if (equipWindowOpen && !relicWindowOpen) DrawEquipWindow();
        }

        private void DrawHelp()
        {
            string mode = GameSession.DevMode ? "개발자 모드 (숫자키 즉시)" : "일반 모드 (영창·순서 입력)";
            string text =
                "<b>기능 테스트</b>  (F6 접기)\n" +
                $"F7 몬스터·이벤트: <color=#ffd84d>{PickName()}</color>   F8 부르기\n" +
                $"F9 유물 장착·해제 ({PlayerRelics.For(player).Owned.Count}개)\n" +
                $"F10 다음 구역: {FeatureTestStage.Names[(area + 1) % FeatureTestStage.Order.Length]}\n" +
                $"F5 미니게임 전환: {mode}\n" +
                $"` A/B 전환: {FocusName(RoleInput.Focus)}\n" +
                "F11 결과 창\n" +
                "F12 장비 끼기·빼기 · 골드 +100 · 식당·상점 창\n" +
                "F1 무적 · F2 마나 무한 · F3 적 처치 · F4 회복";
            var rect = new Rect(Screen.width - 330f, 64f, 316f, 226f);
            GUI.Box(rect, GUIContent.none, box);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 12f), text, line);
        }

        // 한 역할만 조작 중일 때 화면 위 가운데 아래쪽에 "A 조작 중"/"B 조작 중" 알약 (역할 색: A 하늘색, B 주황)
        private void DrawFocusBadge()
        {
            bool a = RoleInput.Focus == ControlFocus.A;
            string text = a ? "<b><color=#73d9ff>A 조작 중</color></b>  (` 전환)" : "<b><color=#ffa659>B 조작 중</color></b>  (` 전환)";
            var rect = new Rect(Screen.width / 2f - 110f, Screen.height - 74f, 220f, 28f);
            GUI.Box(rect, GUIContent.none, box);
            var style = new GUIStyle(line) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(rect, text, style);
        }

        private void DrawRelicWindow()
        {
            var all = RelicBook.All;
            var relics = PlayerRelics.For(player);
            float width = Mathf.Min(640f, Screen.width - 40f);
            float height = Mathf.Min(520f, Screen.height - 40f);
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(rect, GUIContent.none, box);
            GUI.Box(rect, GUIContent.none, box); // 두 겹으로 덜 비치게
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 40f),
                "<b>유물 장착·해제</b> — 누르면 장착, ★을 누르면 해제 (해제하면 시험 맵을 다시 만들어 체력·레벨도 처음으로). F9/Esc 닫기", line);

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
                if (IBlameYou.UI.CuteButton.Button(r, new GUIContent((owned ? "★ " : "   ") + RelicBook.Name(id), RelicBook.Description(id)), cell))
                {
                    ToggleRelic(id);
                    break; // 해제하면 맵을 다시 만들므로 이번 프레임은 그만 그린다
                }
            }
            GUI.EndScrollView();
            if (!string.IsNullOrEmpty(GUI.tooltip))
                GUI.Label(new Rect(rect.x + 12f, rect.yMax - 30f, rect.width - 24f, 24f), GUI.tooltip, line);
        }

        // F12: 장비 끼기·빼기 (Docs/EquipmentDesign.md). 장비는 빼면 능력치가 바로 되돌아가므로 맵을 다시 만들지 않는다.
        private void DrawEquipWindow()
        {
            var equipment = PlayerEquipment.For(player);
            float width = Mathf.Min(720f, Screen.width - 40f);
            float height = Mathf.Min(300f, Screen.height - 40f);
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(rect, GUIContent.none, box);
            GUI.Box(rect, GUIContent.none, box);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 160f, 40f),
                "<b>장비 끼기·빼기</b> — 누르면 그 칸에 끼고, ★을 누르면 뺀다. 오른쪽 버튼으로 새로 낄 수식어. F12/Esc 닫기", line);
            string gradeName = EquipmentModifier.Name(equipGrade);
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.xMax - 144f, rect.y + 10f, 132f, 30f), "수식어: " + (gradeName.Length == 0 ? "보통" : gradeName), cell))
                equipGrade = equipGrade >= EquipmentModifier.MaxGrade ? EquipmentModifier.MinGrade : equipGrade + 1;

            var slots = (EquipSlot[])Enum.GetValues(typeof(EquipSlot));
            float colWidth = (rect.width - 24f - 8f * (slots.Length - 1)) / slots.Length;
            int maxRows = 0;
            for (int c = 0; c < slots.Length; c++)
            {
                var defs = EquipmentBook.OfSlot(slots[c]);
                maxRows = Mathf.Max(maxRows, defs.Count);
                var current = equipment.Get(slots[c]);
                for (int i = 0; i < defs.Count; i++)
                {
                    var def = defs[i];
                    bool worn = current != null && current.Id == def.Id;
                    var shown = worn ? current : new EquipmentItem(def.Id, equipGrade);
                    var tip = new List<string>();
                    foreach (var stat in shown.Stats()) tip.Add(EquipmentBook.Describe(stat));
                    var r = new Rect(rect.x + 12f + c * (colWidth + 8f), rect.y + 52f + i * 40f, colWidth, 36f);
                    if (IBlameYou.UI.CuteButton.Button(r, new GUIContent((worn ? "★ " : "   ") + shown.Name, $"{shown.Name}: {string.Join(", ", tip)} (가격 {shown.Price})"), cell))
                    {
                        if (worn) equipment.Unequip(slots[c]);
                        else equipment.Equip(shown);
                        keptEquipment.Clear();
                        keptEquipment.AddRange(equipment.All);
                    }
                }
            }

            // 마을 시험 (2026-11-12): 시험 맵에는 마을 방이 없어서 골드를 채우고 식당·상점 창을 바로 연다.
            float by = rect.y + 52f + maxRows * 40f + 8f;
            float bw = (rect.width - 24f - 16f) / 3f;
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 12f, by, bw, 30f), $"골드 +{TestGold} (지금 {RunStats.Gold})", cell))
                RunStats.AddGold(TestGold);
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 12f + bw + 8f, by, bw, 30f), "식당 창 열기", cell))
            {
                StageEvents.VillageVisit++; // 열 때마다 새 방문: 음식을 종류별로 다시 살 수 있다
                OpenVillageWindow(VillageService.Restaurant);
            }
            if (IBlameYou.UI.CuteButton.Button(new Rect(rect.x + 12f + 2f * (bw + 8f), by, bw, 30f), "장비 상점 창 열기", cell))
                OpenVillageWindow(VillageService.Shop);

            if (!string.IsNullOrEmpty(GUI.tooltip))
                GUI.Label(new Rect(rect.x + 12f, rect.yMax - 30f, rect.width - 24f, 24f), GUI.tooltip, line);
        }

        private const int TestGold = 100;

        private void OpenVillageWindow(VillageService service)
        {
            equipWindowOpen = false; // 마을 창은 게임을 멈추고 위에 뜬다. 겹치지 않게 이 창은 닫는다
            if (!StageEvents.RaiseVillageInteract(service)) StageEvents.PostNotice("마을 창을 열 수 없다 (HUD 없음)");
        }
    }
}
