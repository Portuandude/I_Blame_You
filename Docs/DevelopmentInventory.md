# I Blame You! — 개발 현황 정리 (코드·리소스 전체)

> 2026-11-24 (KST) 기준, `develop` = PR #137 머지 시점 (`3665a92`). 열린 PR은 #134(데모 빌드) 하나.
> 자동 개발은 2026-11-24 사람 요청으로 **꺼져 있다** (예약 작업 비활성화, 삭제하지 않음).
> 결정할 것은 [DesignDecisionsTodo.md](DesignDecisionsTodo.md), 파일 찾기는 [AutoDev/CODEMAP.md](AutoDev/CODEMAP.md).

## 0. 한눈에

| 항목 | 지금 |
| --- | --- |
| 엔진 | Unity 6000.3.24f1, 2D, 레거시 Input Manager(Active Input = Both), UI는 IMGUI |
| 머지된 PR | #1 ~ #137 (#134 데모 빌드만 열림) |
| 게임 코드 | `Assets/Scripts/` C# 281개, 약 44,600줄 |
| 자동 테스트 | EditMode 테스트 파일 38개, 테스트 약 330개 (Unity 없이 mono로도 돌림) |
| 씬 / 프리팹 | 씬 1개(`Scenes/testScene.unity`) / 프리팹 2개(Player, Slime). 나머지 오브젝트는 코드(Spawner)가 만든다 |
| 그림 | PNG 약 810장 (대부분 `Tools/ArtGen` 파이썬이 절차 생성한 임시 아트) |
| 소리 | WAV 113개 (배경음악 6곡 + 효과음, `Tools/AudioGen` 합성 임시음) |
| 한 판 | 스테이지 1(초원) 하나: 시작 → 방 탐험 → 중간보스 → 마을 → 보스 → 클리어, 15~30분 |
| 2인 | 한 PC 분담(키보드+마우스) + **컴퓨터 두 대 온라인(호스트 방식, TCP 직접 IP)** |

## 1. 게임 시스템 (`Assets/Scripts/`)

상태: ✅ 완료 · 🟡 일부/임시 · ⬜ 안 함

### 1-1. 코어·흐름 (`Core/`, 17파일)

| 기능 | 파일 | 상태 |
| --- | --- | --- |
| 씬 시작·판 조립·적 스폰 분기 | `PlaytestBootstrap` | ✅ |
| 시작 화면 (시작·온라인·설정·조작 키·게임 종료, 개발자 모드 토글) | `StartMenu` | ✅ |
| 프롤로그 웹툰 7컷 (P1 Space + P2 클릭 동시 꾹 = 건너뛰기, F12 임시) | `StoryIntro`, `Resources/Story/` | 🟡 그림 임시 |
| 도시 메뉴 (마음 가루·수정 탑·아카데미·출발) | `CityMenu` | 🟡 메뉴판만, 걸어 다니는 광장은 베타 뒤 |
| 저장 (설정·최고 기록·도시 진행, 모드별) | `SaveData`, `RunSave` | 🟡 런 중간 저장은 코드만 있고 쓰는 곳 없음(골드·장비 미포함) |
| 판 기록·골드 | `RunStats`, `RunGold` | ✅ 골드는 그 판 안에서만 |
| 화면 설정 (해상도·전체화면·프레임 제한·흔들림/번쩍임 %) | `DisplaySettings*` | ✅ |
| P1 조작 키 바꾸기 | `KeyBindingsWindow` (+ `Player/Input/KeyBindings`) | ✅ P2·메뉴 이동 키는 고정 |
| 온라인 창 (호스트/참가) | `OnlineWindow` | ✅ |
| 유물 도감 (C키) | `RelicCodexWindow` | ✅ |
| 기능 테스트 모드 (F5~F12: 몬스터·방 이벤트·유물·장비 시험) / 치트 F1~F4 | `FeatureTestTools`, `DevCheats` | ✅ 개발용 |
| 카메라 | `CameraFollow` | ✅ |
| 세션 상태 (개발자 모드·한 키보드 공유·데모) | `GameSession` | ✅ |

### 1-2. 플레이어 (`Player/`, 38파일)

| 기능 | 파일 | 상태 |
| --- | --- | --- |
| 이동 (걷기·달리기·점프·대시, 코요테 타임, 입력 버퍼) | `PlayerMovement` | ✅ 벽 점프 없음(결정) |
| 4방향 순간이동 (WASD+Enter, 발판 위아래 통과, 낙사 방지) | `Skills/TeleportSkill`, `Rules/TeleportRules` | ✅ |
| A/B 역할 입력 (P1 = 이동·순간이동, P2 = 공격) | `Input/RoleInput`, `PlayerControlState` | ✅ |
| 마법 목록 (기본 4속성 + 아카데미 8개) | `Skills/SpellBook` | ✅ |
| P2 마법진 그리기 (한붓그리기 룬, 정확도 → 피해 배율, 50% 이하 실패) | `Skills/SpellCaster`, `Rules/MagicCircleRules` | ✅ 수치는 플레이테스트 대기 |
| P2 2단계 원형 다이얼 (속성 → 마법) | `Rules/SpellDialRules`, `UI/SkillHUD.SpellDial` | ✅ |
| 아카데미 마법 8개 (불꽃 장막·불꽃 비 / 얼음 족쇄·물안개 / 순풍·칼바람 / 바위 창·운석) | `Skills/FireWall`, `FireRainDrop`, `MistZone`, `MistMark`, `GroundSpike`, `Rules/ShackleRules`, `CursorSpellPreview` | ✅ 수치는 플레이테스트 대기 |
| 레벨업 특성 트리 | `Progression/TraitTree`, `PlayerProgression`, `LastStandGuard` | ✅ |
| 유물 31종 (한 판용) | `Relics/Data/RelicBook`, `PlayerRelics` | ✅ 정리안(RELIC_LIST) 답 없음 |
| 장비 (로브·지팡이·신발 × 4종, 수식어 4종 비틀린/흔한/숙련자의/영웅의, A/B 공용 한 벌) | `Equipment/Data/*`, `PlayerEquipment`, `LunchboxGuard` | ✅ |
| 피격 자세·잔상·찌그러짐·무적 깜빡임 | `PlayerHurtPose`, `DashAfterimage`, `PlayerSquash`, `GraceBlink` | ✅ |
| 방어막 마법 | — | ⬜ 2026-11-16 결정으로 삭제 |

### 1-3. 적 (`Enemies/`, 43파일)

| 분류 | 적 | 상태 |
| --- | --- | --- |
| 쫄몹 | 슬라임(속성 색), 고블린, 고블린 병사, 고블린 궁수 | ✅ |
| 이벤트 | 보물 고블린 (`TreasureGoblin`) | ✅ |
| 정예 | 어미 슬라임(분열), 고블린 대전사(광폭화) | ✅ |
| 중간보스 (둘 중 무작위) | 쌍둥이 슬라임 기사(불·물), 프리즘 젤리(색 바뀌는 속성) | ✅ 프리즘 젤리 수치 플레이테스트 대기 |
| 보스 | 슬라임 킹 (패턴 6개, 50%에서 2페이즈) | ✅ |
| 공통 | `EnemyController`(체력·경직·접촉 피해), 예고(`EnemyTelegraph`), 피격 자세, 등장 연출(`BossEntrance`) | ✅ |
| 숲 스테이지 적 (코볼트 등) | — | ⬜ 미룸 |

### 1-4. 스테이지·맵 (`Systems/MapGeneration/`)

| 기능 | 파일 | 상태 |
| --- | --- | --- |
| 방 격자 5×4, 14~18방 (전투 5+ 중 좌우 2+, 퍼즐 2+, 마을 1, 중간보스 1, 보물·샘 1~2, 보스 1) | `Chunks/RoomLayout`, `RoomShaper` | ✅ |
| 청크 지형 생성·검증·계획 (순수 C#, 시드) | `Chunks/ChunkGenerator(.Puzzles)`, `StageValidator`, `StagePlanner`, `StageGenerator` | ✅ |
| 씬에 불러오기·방 전환·보상·방 이벤트 | `Stage/ChunkStreamer(.Puzzles/.Queries/.Rewards/.RoomEvents)`, `ChunkBuilder`, `TerrainDresser` | ✅ |
| 전투 구역 잠금·보상 구슬 | `Stage/ArenaZone`, `ArenaGate`, `ArenaReward` | ✅ |
| 방 이벤트: 매복(+경고), 보물 고블린, 저주 상자, 궁수 탑 | `Chunks/AmbushRule`, `TreasureGoblinRule`, `CursedChestRule`, `Stage/CursedChest` | ✅ |
| 마을 (식당·장비 상점·수식어 다시 뽑기, NPC·장식·음악) | `Stage/Village*`, `Equipment/Data/VillageFood`, `EquipmentShop` | ✅ |
| 유물 상자·장비 줍기·동전 무더기·출구·낙사 복귀 | `Stage/RelicChest`, `EquipmentPickup`, `CoinPile`, `StageExit`, `FallRecovery` | ✅ |
| 미니맵 (던그리드식) | `UI/MinimapUI`, `Chunks/MinimapRules` | ✅ |
| 스테이지 2 이후 | — | ⬜ |

### 1-5. 퍼즐·함정·지형

| 분류 | 내용 | 상태 |
| --- | --- | --- |
| 퍼즐 방 (몹 없음) | 속성 봉인문 `ElementGate`, 등불 순서·거울 룬 `LanternPuzzle`, 바람 상자 `WindBox`, 마법 스위치 발판 `PlatformSwitch` | ✅ 바람 상자 플레이테스트 대기 |
| 함정 (`Systems/Traps/`) | 가시, 화살, 화염 분출, 종유석, 무너지는 발판, 움직이는 발판, 깜빡 발판, 통통 버섯, 바람 구멍 | ✅ |
| 지형 변형 | 기둥 숲, 두 갈래 길, 세로 탑·굴, 버섯 탑, 넓은 방, 위험한 길 끝 동전 | ✅ |

### 1-6. 전투·수치·공통 시스템 (`Systems/`)

| 기능 | 파일 | 상태 |
| --- | --- | --- |
| 피해 공식·속성 상성 | `Stats/DamageCalculator`, `Formula/DamageFormula`, `ElementCore`, `StatTable` | ✅ |
| 체력·마나(공유 하나)·기력 | `Health/`, `Mana/`, `Stamina/` | ✅ |
| 상태이상 (화상·젖음·경직, 아이콘) | `StatusEffects/` | ✅ |
| 전투 공통 (투사체·히트스톱·넉백·피해 숫자·겹침 조회) | `Combat/` | ✅ |
| 이펙트 26종 (`EffectLibrary`, `VfxPlayer`, 속성 색 바꾸기) | `Effects/` | ✅ |
| 소리 (`GameAudio`), 배경 시차 (`ParallaxBackground`) | `Audio/`, `Background/` | 🟡 소리는 임시 합성음 |

### 1-7. UI (`UI/`, 38파일, 전부 IMGUI)

HUD(캡슐 바·보스 바·스킬·마나 링), 일시정지, 결과 창, 사망 화면, 유물·장비 카드, 상점·식당, 특성 창, 도감, 적 안내, 마법진·다이얼, 연속 처치, 낮은 체력 경고, 온라인 일시정지 안내, 미니맵·큰 지도, 적 체력 바. ✅ (정식 UI 아트·uGUI 전환은 안 함 🟡)

### 1-8. 온라인 2인 (`Net/`, 23파일)

| 기능 | 상태 |
| --- | --- |
| 호스트가 게임 전체를 돌리고 친구는 화면(그림 조각·HUD 명령·소리 이름)을 받음, 친구 → P2 입력 | ✅ |
| TCP 직접 IP(포트 27777), 주소 복사, 가상 LAN 주소 우선 | ✅ |
| 지연·KB/s 표시, 10초 끊김, 친구 자동 재접속(8번), 친구 끊기면 호스트 일시정지 | ✅ |
| P2 "여기!" 신호(가운데 버튼) | ✅ |
| 스팀 친구 초대·스팀 중계(Steam Networking Sockets) | ⬜ `ITransport` 자리만 있음 |
| 친구가 메뉴 조작 | ⬜ 호스트만 |

### 1-9. 빌드·배포

| 항목 | 상태 |
| --- | --- |
| `Tools > I Blame You > Windows 빌드 만들기` (`Editor/WindowsBuild`) | ✅ |
| 데모 빌드 (IBY_DEMO, zip 메뉴, Actions Demo Build) | 🟡 PR #134 열림 (사람 머지 대기) |
| 스팀 (Steamworks, 업적, 클라우드 저장) | ⬜ |

## 2. 리소스

| 경로 | 내용 | 만든 방법 |
| --- | --- | --- |
| `Assets/Art/Sprites/Characters/Wizard/` | 마법사 Idle·Walk·Run·Jump·Dash·Attack (39장) | `generate_wizard.py` |
| `Assets/Art/Sprites/Enemies/` | 슬라임·고블린 4종 Idle·Run·Attack·Die | `generate_sprites.py`, `decorate_slime.py` |
| `Assets/Art/Sprites/Effects/` | 마법 투사체 (화염구·물창·바람·바위 가시·비전 구슬·순간이동·베기 등) | `generate_skill_fx.py`, `generate_magic_fx.py` |
| `Assets/Art/Animations/` | 플레이어·슬라임 애니메이션 컨트롤러 | 에디터 `GameAssetSetup`가 생성 |
| `Assets/Resources/Effects/` (226장, 19종) | 타격·먼지·코인·레벨업·상자 열기·충격파 등 | `generate_vfx.py`, pixelfx |
| `Assets/Resources/Enemies/` (154장) | 적 런타임 그림 (기사 불·물, 프리즘 젤리 포함) | `generate_sprites.py`, `generate_prism_jelly.py` |
| `Assets/Resources/Bosses/` (13장) | 슬라임 킹 | ArtGen |
| `Assets/Resources/Stage/` (93장) | 배경 3층, 지형 장식, 함정, 룬 문·스위치, 출구, 바람 상자 | `generate_background.py`, `generate_terrain.py` |
| `Assets/Resources/UI/HUD/` (68장) | 바·버튼·패널·카드·지도 아이콘·장비·음식 아이콘 | `generate_hud.py`, `generate_equipment_icons.py`, `generate_food_icons.py`, `generate_relic_icons.py` |
| `Assets/Resources/Village/` (16장) | 식당·상점·분수·NPC | `generate_village.py` |
| `Assets/Resources/Story/` (8장) | 프롤로그 7컷 + 말풍선 | `generate_story.py` (임시 그림) |
| `Assets/Resources/StatusIcons/` | 화상·젖음·경직 | ArtGen |
| `Assets/Resources/Audio/` (113개) | 배경음악 6곡(메뉴·던전·후반 던전·중간보스·보스·마을) + 효과음 | `Tools/AudioGen/generate_audio.py` (임시 합성) |
| `Assets/Resources/Shaders/SpriteFlash.shader` | 피격 번쩍임 | 직접 |

모든 그림·소리는 **임시**다 (정식 아트·사운드 교체 여부는 결정 목록 참고). 외부 타일셋은 저장소에 없다(`.gitignore`).

## 3. 도구·테스트·CI

| 항목 | 내용 |
| --- | --- |
| 에디터 메뉴 | `GameAssetSetup`(스프라이트·애니메이션 굽기), `PrefabSetup`(프리팹 굽기), `WindowsBuild` — 사람은 pull 뒤 `Tools > I Blame You > Generate Sprites And Animations` + `Generate Prefabs` |
| `Tools/ArtGen/` | 그림 생성기 15개 + `palette.py` |
| `Tools/AudioGen/` | 효과음·음악 합성 |
| `Tools/StageGen/` | 스테이지 미리보기 (Unity 없이 PNG) |
| `Tools/Dev/` | `run_editmode_tests.sh`(mono+NUnit), `unity_meta.py` |
| 테스트 | `Assets/Tests/EditMode/` 36파일 (방 격자·스테이지·피해 공식·유물·장비·상점·도시·마법진·다이얼·네트워크 등), `EditModeUnity/` 2파일 (CI 전용) |
| 순수 C# 어셈블리 | StageGeneration, Formula, Relics, Equipment, SkillRules, InputRules, Net, NetView |
| CI | `.github/workflows/unity-ci.yml` — GameCI v4.3.2 고정, PR마다 컴파일 + EditMode (`editmode` 체크가 develop 머지 조건) |
| 자동 개발 | `.claude/commands/auto-dev.md`, `.claude/agents/`(planner·code-reviewer·adversarial-reviewer), `.claude/skills/`(editmode-tests·handoff·review-loop·unity-meta·pixelfx), `Docs/AutoDev/` |

## 4. 문서 (`Docs/`)

| 문서 | 내용 | 최신? |
| --- | --- | --- |
| `GameDesignDocument.md` | 기획 문서 본체 | ⚠ 2026-11-09 이후 크게 안 고침 — 새 기획서로 바꿀 대상 |
| `Story.md` | 아루·벨, 악신 바알제, 세계, 멀티 엔딩(TBD), 밈 컷씬 | ✅ |
| `LevelDesign.md` | 방 격자·방 구성 | ✅ |
| `CityDesign.md` | 도시·마음 가루·수정 탑·아카데미 | ✅ |
| `MagicCircleDesign.md` | 마법진 그리기 | ✅ |
| `EquipmentDesign.md` | 장비·마을·식당·골드 | ✅ ([결정 필요] 1개 남음, 사실상 해결) |
| `PuzzleRoomDesign.md` | 퍼즐 방 | ✅ |
| `OnlinePlay.md` | 온라인 2인 | ✅ |
| `MidBossDraft.md`, `Stage2Draft.md`, `MetaProgressionDraft.md`, `ContentIdeas.md` | 초안 | 일부 낡음 |
| `ArtStyle.md` | "귀엽지만 화려하게" | ✅ |
| `InputSystemMigration.md` | 새 Input System 검토 | 보류 |
| `DIRECTION_AND_DECISIONS.md`, `PLAYTEST_AND_PROGRESS.md` | 결정 기록·플레이테스트 | 일부 낡음 |
| `AutoDev/` | BACKLOG·LOG·CODEMAP·HANDOFF | ✅ |

## 5. 아직 안 한 것 / 알려진 빈틈

- **콘텐츠**: 스테이지 2(숲) 이후, 걸어 다니는 도시 광장, 멀티 엔딩, 보스 밈 컷씬, NPC 대사, 엔딩·크레딧.
- **출시 준비**: 스팀 연동(초대·중계·업적·클라우드 저장), 패드, 새 Input System, 정식 아트·사운드, IMGUI → 정식 UI, 다국어, 데모 PR #134 머지.
- **기술 부채**: 오래된 `.cs` 20여 개와 옛 이펙트 PNG의 `.meta`가 저장소에 없음(사람 PC에서 커밋해야 GUID가 안 깨짐), 씬 1개(`testScene`)에 모두 몰림, 런 중간 저장 안 씀.
- **플레이테스트 대기**: 마법진 난이도, 아카데미 마법 수치, 프리즘 젤리, 바람 상자, 온라인 지연·끊김 시간 (자세히는 결정 목록 G장).
