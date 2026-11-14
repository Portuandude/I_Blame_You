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
| 씬 시작·런 조립·적 스폰 분기 | `Core/PlaytestBootstrap.cs` (`SpawnEnemy` switch), 저장 `Core/RunSave.cs`·`SaveData.cs`, 카메라 `Core/CameraFollow.cs` |
| 스테이지 계획·청크 지형 (순수 C#, 테스트 가능) | `Systems/MapGeneration/Chunks/`: `StagePlanner`(청크 순서·시간), `ChunkGenerator`(청크 지형), `StageValidator`(갈 수 있는지), `ChunkData`(타일·적·기능 데이터) |
| 방 격자 (순수 C#) | `Chunks/RoomLayout.cs`(5×4 방 배치·방 구성: 전투 5+·퍼즐 2+·마을 1~2, 이동 방 없음), `Chunks/RoomShaper.cs`(방 지형·세로 방 모양·중간보스 방 기사) |
| 스테이지를 씬에 만들기 | `Systems/MapGeneration/Stage/ChunkStreamer.cs`(로드·방 전환·보상), `ChunkBuilder.cs`(타일), `ArenaZone.cs`(전투 잠금), `Checkpoint`·`RelicChest`·`ArenaReward`·`StageExit`·`FallRecovery` |
| 함정·발판 | `Systems/Traps/` |
| 퍼즐 (봉인문) | `Puzzles/ElementGate.cs` |
| 적 공통 | `Enemies/EnemyController.cs`(체력·경직·접촉 피해), `EnemySpawner.cs`(조립 헬퍼) |
| 적 종류 | `Enemies/Common/{Slime,Goblin,GoblinArcher}`, `Enemies/Elite/{MotherSlime,GoblinWarrior}`, 보스 `Enemies/Bosses/SlimeKing`, 중간보스 `Enemies/Bosses/SlimeKnights` — 각 폴더의 `*Spawner`가 구성의 단일 출처 |
| 수치 | `Systems/Stats/StatTable.cs`(스탯 문서 값), `DamageCalculator.cs`(피해·`IDamageModifier`), `Formula/`(상성, 순수 C#) |
| 상태이상 | `Systems/StatusEffects/` |
| 플레이어 이동·전투 | `Player/PlayerMovement.cs`, `PlayerCombat.cs`, 입력 `Player/Input/` |
| 마법·스킬 | `Player/Skills/` (`SpellBook` 목록, `SpellCaster` 영창) |
| 레벨·특성·유물 | `Player/Progression/`, `Player/Relics/` |
| 이펙트 | `Systems/Effects/` (`EffectLibrary` 프레임 로드, `VfxPlayer`, `ElementRecolor` 그림 바꿔 끼우기) |
| HUD·UI | `UI/SkillHUD*.cs`(partial, 기능별 파일), `MinimapUI.cs`, `EnemyHealthBar.cs` |
| 에디터 툴 (애니메이션·프리팹 굽기) | `Editor/GameAssetSetup.cs`, `Editor/PrefabSetup.cs` |

## 저장소 루트

| 경로 | 내용 |
| --- | --- |
| `Assets/Tests/EditMode/` | EditMode 테스트. 스테이지 생성 테스트는 `stage-tests` 스킬로 로컬 실행 |
| `Tools/ArtGen/` | 그림 생성기 (Python PIL). `palette.py` 색, `generate_*.py`, `decorate_slime.py` |
| `Tools/AudioGen/`, `Tools/StageGen/` | 효과음 생성, 스테이지 미리보기 |
| `Docs/` | `GameDesignDocument.md`(기획), `LevelDesign.md`(방 격자), `ArtStyle.md`, 초안들(`*Draft.md`) |
| `Docs/AutoDev/` | 자동 개발 절차 설정·백로그·기록·인수인계(`HANDOFF.md`) |

## 큰 파일 읽는 법

300줄이 넘는 파일(`ChunkStreamer`, `ChunkGenerator`, `SlimeKingController`, `PlaytestBootstrap`, `README.md` 등)은 통째로 읽지 않는다.
`grep -n "함수명\|키워드"`로 줄 번호를 찾고 `sed -n 'A,Bp'`로 그 부분만 읽는다.
