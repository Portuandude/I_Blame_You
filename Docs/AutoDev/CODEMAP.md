# 코드 지도 (탐색 범위 제한용)

작업을 시작할 때 저장소 전체를 훑지 말고 이 표에서 해당 줄만 보고 그 파일로 바로 간다.
새 기능 폴더를 만들거나 큰 구조를 바꾸면 이 표도 한 줄 고친다.

## 읽지 않는 곳

| 경로 | 이유 |
| --- | --- |
| `Trash/` | 치운 파일 보관소. 되살릴 때만 |
| `Library/`, `Temp/`, `Logs/`, `UserSettings/` | Unity 생성물 (저장소에 없음) |
| `Assets/Resources/**.png`, `Assets/Art/**.png` | 생성된 그림. 확인이 필요하면 한두 장만 열거나 크기만 본다 |
| `*.meta`, `*.prefab`, `*.unity`, `*.asset` | Unity 직렬화. GUID·프리팹 확인이 필요할 때만 grep |
| `Docs/AutoDev/BACKLOG_ARCHIVE.md`, `LOG_ARCHIVE.md` | 끝난 기록. 과거 작업을 찾을 때만 grep |
| `.claude/skills/pixelfx/` | 외부 스킬 원본. 그 스킬을 쓸 때만 |

## 기능 → 파일 (`Assets/Scripts/` 기준)

| 기능 | 파일 |
| --- | --- |
| 온라인 2인 (호스트 방식, 컴퓨터 두 대) | `Net/` — 순수 `Net/Core/`(바이트·TCP·P2 입력·압축, 테스트 `Tests/EditMode/NetCoreTests`), 그림·HUD 옮기기 `Net/View/`(`NetWorld`·`HudRecorder`·`NetGUI` 별칭, 테스트 `Tests/EditModeUnity/NetViewTests`), 연결 `NetSession`, 호스트 보내기 `NetHostStreamer`, 친구 화면 `NetClientView`, P2 입력 `P2Input`, 소리 `NetAudio`, 메뉴 창 `Core/OnlineWindow.cs`. OnGUI 파일은 `using GUI = IBlameYou.Net.NetGUI;` 필수 |
| 씬 시작·런 조립·적 스폰 분기 | `Core/PlaytestBootstrap.cs` (`SpawnEnemy` switch, 스테이지 1→5·쉼터 `GoToRest`·`BuildRunData`·`TeardownWorld`), 쉼터 창 `Core/RestMenu.cs`, 저장 `Core/RunSave.cs`(쉼터에서만)·`SaveData.cs`(슬롯 키 `SlotKey`), 규칙 `Chunks/RunFlowRule`·`RestRule`·`SaveSlotRule`, 카메라 `Core/CameraFollow.cs` |
| 스테이지 계획·청크 지형 (순수 C#, 테스트 가능) | `Systems/MapGeneration/Chunks/`: `StagePlanner`(청크 순서·시간), `ChunkGenerator`(청크 지형), `StageValidator`(갈 수 있는지), `ChunkData`(타일·적·기능 데이터) |
| 방 격자 (순수 C#) | `Chunks/RoomLayout.cs`(5×4 방 배치·방 구성: 14~18방 = 전투 5+(좌우 2+)·퍼즐 2+·마을 1·중간보스 1(MidBossFor)·보물·샘 1~2, 이동 방 없음. 주 경로·곁방 개념 없음 — 보스 = 가장 먼 방, 중간보스 = 보스 길목 밖 그다음 먼 방, 특수 방은 문 모양만 봄), `Chunks/RoomShaper.cs`(방 지형·세로 방 모양·중간보스 방 기사) |
| 스테이지를 씬에 만들기 | `Systems/MapGeneration/Stage/ChunkStreamer.cs`(로드·방 전환, partial: `.Queries` 미니맵·결과 창 질문 / `.Puzzles` 등불 퍼즐 / `.Rewards` 보상·런 기록 / `.RoomEvents` 보물 고블린·저주 상자), `ChunkBuilder.cs`(타일), `ArenaZone.cs`(전투 잠금), `Checkpoint`·`RelicChest`·`ArenaReward`·`StageExit`·`FallRecovery` |
| 함정·발판 | `Systems/Traps/` (숲: `ThornBush`, `VineFloor`(불 투사체는 `Combat/IElementTouchReceiver`로 지나가며 태움), 통나무 = `Stalactite.CreateLog`, 덩굴 벽 = `ElementGate.DressAsVines`) |
| 퍼즐 (봉인문·등불 순서/거울 룬·바람 상자) | `Puzzles/` — `ElementGate`, `LanternPuzzle`·`LanternLight`, `WindBox`(규칙 `Chunks/WindBoxRules`, 투사체 방향 `Combat/IElementPushReceiver`). 지형은 `ChunkGenerator.Add*Puzzle`, 불러오기 `ChunkStreamer` |
| 적 공통 | `Enemies/EnemyController.cs`(체력·경직·접촉 피해), `EnemySpawner.cs`(조립 헬퍼) |
| 스테이지별 구성 (숲 = 2) | 적 바꿔 끼우기 `Chunks/StageRoster`(스테이지 1은 그대로), 숲 함정 놓기 `Chunks/ForestDressing`, 스테이지 번호 `GameSession.Stage`·`RoomData.Stage`·`ChunkData.Stage` |
| 적 종류 | 숲 `Enemies/Common/Kobold`(돌격 `KoboldController` — 와르그·족장도 씀), `Enemies/Common/KoboldNetter`(궁수 + `NetShot`), 보스 `Enemies/Bosses/KoboldChief`(무리 `KoboldPack`, 그물 `KoboldChiefNet`), 그물 속박 `Systems/StatusEffects/NetSnare`. `Enemies/Common/{Slime,Goblin,GoblinArcher}`, `Enemies/Elite/{MotherSlime,GoblinWarrior}`, 보스 `Enemies/Bosses/SlimeKing`, 중간보스 `Enemies/Bosses/SlimeKnights`·`Enemies/Bosses/PrismJelly`(패턴 `JellyPatterns`, 위험물 `JellyHazards`, 규칙 `Stats/Formula/PrismJellyRules`) — 각 폴더의 `*Spawner`가 구성의 단일 출처 |
| 숨은 능력치·선택지·엔딩 (순수 C#) | `Chunks/HiddenStatsRule`(인과율·선악도 구간), `Chunks/ChoiceRouletteRule`(A·B 몰래 고르기·룰렛, 창 `UI/SkillHUD.Choice`), `Chunks/EndingRule`(엔딩 표 가1~라2·임계치 마1~마3, 아직 게임에서 부르지 않음), 저장 `Core/SaveData`(`RecordRevival`·`Alignment`) |
| 수치 | `Systems/Stats/StatTable.cs`(스탯 문서 값), `DamageCalculator.cs`(피해·`IDamageModifier`), `Formula/`(상성, 순수 C#) |
| 겹침 조회 (프레임·틱마다) | `Systems/Combat/OverlapQuery`(OverlapBoxAll/CircleAll과 같은 결과를 부르는 쪽이 가진 `List`에 담음). 자주 부르는 곳에서 `*All` 새 배열 금지, 목록은 부르는 쪽마다 따로 |
| 체력·마나·기력 | `Systems/Health/HealthSystem.cs` (피해 `TakeDamage`, 지속 피해 `TakeDotDamage`(무적·버티기 지킴), 자해 `Drain`, 시간 무적 `GrantGrace` — 짧은 무적은 공용 `Invulnerable` 대신 이것), `Systems/Mana/`, `Systems/Stamina/` |
| 상태이상 | `Systems/StatusEffects/` |
| 플레이어 이동·전투 | `Player/PlayerMovement.cs`, `PlayerCombat.cs`, 입력 `Player/Input/` (`RoleInput` 역할별 입력. P1 키는 `KeyBindings`로만 읽는다 — `KeyCode.Space` 등 직접 금지, 규칙 `Input/Rules/KeyBindingRules`, 창 `Core/KeyBindingsWindow`) |
| 마법·스킬 | `Player/Skills/` (`SpellBook` 목록, `SpellCaster` 마법진 그리기, 규칙 `Rules/MagicCircleRules`) |
| 레벨·특성·유물 | `Player/Progression/`, `Player/Relics/` |
| 이펙트 | `Systems/Effects/` (`EffectLibrary` 프레임 로드, `VfxPlayer`, `ElementRecolor` 그림 바꿔 끼우기) |
| HUD·UI | `UI/SkillHUD*.cs`(partial, 기능별 파일. 글자 스타일은 `SkillHUD.cs` EnsureStyles에서 한 번만 만든다 — OnGUI 안에서 `new GUIStyle` 금지, 메뉴 창은 `UI/DerivedStyle`), `MinimapUI.cs`, `EnemyHealthBar.cs` |
| 에디터 툴 (애니메이션·프리팹 굽기) | `Editor/GameAssetSetup.cs`, `Editor/PrefabSetup.cs` |

## 저장소 루트

| 경로 | 내용 |
| --- | --- |
| `Assets/Tests/EditMode/` | 순수 C# EditMode 테스트. `editmode-tests` 스킬로 로컬 실행. Unity가 필요한 테스트는 `Assets/Tests/EditModeUnity/` (CI에서만) |
| `Tools/ArtGen/` | 그림 생성기 (Python PIL). `palette.py` 색, `generate_*.py`, `decorate_slime.py` |
| `Tools/AudioGen/`, `Tools/StageGen/` | 효과음 생성, 스테이지 미리보기 |
| `Docs/` | `GameDesignDocument.md`(기획), `LevelDesign.md`(방 격자), `ArtStyle.md`, 초안들(`*Draft.md`) |
| `Docs/AutoDev/` | 자동 개발 절차 설정·백로그·기록·인수인계(`HANDOFF.md`) |

## 큰 파일 읽는 법

300줄이 넘는 파일(`ChunkStreamer`(745줄), `ChunkGenerator`, `SlimeKingController`, `PlaytestBootstrap`, `README.md` 등)은 통째로 읽지 않는다.
`grep -n "함수명\|키워드"`로 줄 번호를 찾고 `sed -n 'A,Bp'`로 그 부분만 읽는다.
