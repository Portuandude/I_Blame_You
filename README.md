# I Blame You!

로그라이크 기반 2D 액션 게임. 시드로 매번 새로 만들어지는 청크식 스테이지를 지나며 패턴 파훼형 전투와 함정 지형을 결합한다.
기획은 [Docs/GameDesignDocument.md](Docs/GameDesignDocument.md), 작업 규칙은 [CLAUDE.md](CLAUDE.md) 참고.

## 개발 환경

- Engine: **Unity 6000.3.24f1** (Unity 6.3 LTS)
- 서드파티 에셋: **2D Platformer Tileset**을 `Assets/2D Platformer Tileset/`에 임포트해야 한다.
  용량(약 77MB)과 라이선스 때문에 `.gitignore`로 제외되어 있어 저장소에는 없다.
- 렌더/입력: 2D, 레거시 Input Manager 사용 (Project Settings > Player > Active Input Handling이 "Input System Package (New)" 단독이면 입력이 동작하지 않으니 "Both"로 둘 것)

## 처음 세팅

1. Unity Hub에서 이 폴더(`I Blame You!`)를 연다. (첫 실행 시 `Library/` 등이 자동 생성됨)
2. 에셋 팩을 `Assets/2D Platformer Tileset/`에 임포트한다.
3. 메뉴 **`Tools > I Blame You > Generate Sprites And Animations`** 를 한 번 실행한다.
   - 플레이어(Player Sword)/슬라임 애니메이션 클립 + 애니메이터 컨트롤러
   - 바닥 타일 선택, `Assets/Resources/LevelArtConfig.asset` 생성
   - 이어서 자동으로 플레이어/슬라임 **프리팹** 생성 (`Tools > I Blame You > Generate Prefabs`) — 이때 `Player`/`Enemy` 레이어도 없으면 자동 추가
   - 컴포넌트 구성이 바뀐 커밋(예: 경직 추가)을 받은 뒤에는 이 메뉴를 한 번 다시 실행해 프리팹을 갱신할 것
   - 컨트롤러를 매번 새로 만들기 때문에, `GameAssetSetup.cs`를 수정했다면 반드시 다시 실행해야 결과물에 반영된다.
4. 빈 씬에 GameObject를 만들고 `PlaytestBootstrap` 컴포넌트를 붙인 뒤 Play. (`Assets/Scenes/testScene.unity`가 이렇게 구성되어 있음)

헤드리스로 실행하려면 (Unity 에디터를 모두 닫은 상태에서):

```bash
Unity.exe -batchmode -nographics -projectPath "<프로젝트 경로>" -executeMethod IBlameYou.EditorTools.GameAssetSetup.Generate -quit
```

## 조작키

Play를 누르면 시작 화면이 뜬다. **개발자 모드**(기본 켜짐)는 한 사람이 A/B를 동시에 조작하고 스킬 미니게임을 숫자키 즉시 발동으로 대체한다.
한 캐릭터를 플레이어 A(이동·방어)와 B(공격)가 나눠 조작하며, 마나는 하나를 같이 쓴다.

| 역할 | 입력 | 동작 |
| --- | --- | --- |
| A | A / D | 좌우 이동 |
| A | Space | 점프 |
| A | Shift (누르고 이동) | 달리기 — 기력을 지속 소모 |
| A | Left Ctrl | 대쉬 — 기력 50, 대쉬 중 무적 |
| A | 방향키 + Enter | 순간이동 (마나 20, 미니게임 없음) |
| A | 넘패드 1~5 | 불/물/바람/땅/무 방어막 → 화면에 뜬 W/A/S/D 순서 입력 (입력 중 정지, 틀리면 마나만 소모) |
| B | 마우스 좌클릭 | 평타 (마우스 쪽으로) |
| B | 상단 1~5 | 불/물/바람/땅/무 공격 스킬 → 영창문 타이핑 후 Enter (오타면 마나만 소모, Esc 취소) |
| 개발자 모드 | 상단 1~5 / 6~9·0 | A 방어막 / B 공격 스킬 즉시 발동 |
| 개발자 모드 | F1 / F2 / F3 / F4 | 무적 / 마나 무한 / 주변 적 처치 / 체력 회복 |
| 공통 | R / M (쓰러졌을 때) | 다시 시작 / 시작 화면 |

수치는 기획 스탯 문서를 옮긴 `Systems/Stats/StatTable.cs`가 기준이고, 피해는 비율식(`공격 × 100 / (100 + 방어)`)으로 계산한다.
고블린 4종과 스킬 이펙트 그림은 `Tools/ArtGen/generate_sprites.py`로 만든 임시 아트다 (`python3 Tools/ArtGen/generate_sprites.py`로 다시 생성).
함정/발판/체크포인트/출구 그림도 같은 스크립트가 `Assets/Resources/Stage/`에 만든다.
배경음악(메뉴/던전)과 마법 발동·적중 효과음은 `Tools/AudioGen/generate_audio.py`로 합성한 임시 사운드다 (`python3 Tools/AudioGen/generate_audio.py`로 다시 생성).

## 폴더 구조

```
Assets/
  Art/
    Animations/Player/        # 생성된 애니메이션 클립 + PlayerAnimator.controller
    Animations/Enemies/Slime/ # 슬라임 클립 + SlimeAnimator.controller
    Materials/                # Frictionless.physicsMaterial2D (프리팹 콜라이더용)
    Sprites/Characters/Wizard/ # 플레이어 마법사 프레임(투명 PNG, 300x256 캔버스, 발 y=193). Idle 7장(직접 준비, 수정 빛이 단계적으로 켜짐, 왕복 재생) + Walk 8 / Run 8 / Jump 2 / Dash 6 / Attack 8장(Idle 한 장을 변형한 임시 프레임. 걷기/달리기는 로브 끝자락을 발처럼 번갈아 내디딤 — 동작별 그림이 생기면 같은 이름의 PNG로 교체 후 툴 재실행)
    Sprites/Enemies/Goblin*/   # 고블린/궁수/병사/대전사 Idle/Run/Attack/Die (임시 아트, Tools/ArtGen)
    Sprites/Effects/           # 스킬/투사체/방어막/순간이동 이펙트 프레임 (임시 아트, Tools/ArtGen)
    Sprites/Enemies/Slime/     # 슬라임 프레임(직접 만든 일러스트, 투명 PNG): Attack 10장, Idle/Run/Die 각 8장. 그 외 Characters, Environment, UI는 비어 있음
  Audio/                      # Music, SFX (비어 있음)
  Data/                       # Characters, Enemies, Rooms — ScriptableObject 인스턴스용 (비어 있음)
  Prefabs/
    Characters/Player.prefab  # 툴이 생성 (직접 수정하지 말고 스포너 코드/툴을 수정)
    Enemies/Slime.prefab      # 툴이 생성
    Bosses/, Props/, Rooms/   # (비어 있음)
  Resources/LevelArtConfig.asset  # 바닥 타일/스프라이트/컨트롤러/프리팹 참조. 런타임에서 Resources.Load
  Resources/StatusIcons/          # 디버프 아이콘 (런타임 로드, Tools/ArtGen)
  Resources/Effects/{Flight,Hit}/ # 마법 비행/적중 이펙트 프레임 (런타임 로드, Tools/ArtGen)
  Resources/Audio/{Music,SFX}/    # 배경음악, Cast_/Hit_<속성> 효과음 (런타임 로드, Tools/AudioGen)
  Resources/Stage/<이름>/         # 가시/화염 분출구/화살 발사대/발판/무너지는 발판/체크포인트/전투 구역 문/출구 그림 (런타임 로드, Tools/ArtGen)
  Scenes/                     # testScene (플레이테스트), Boot/, Chapters/
  Scripts/
    Core/                     # PlaytestBootstrap — 시드로 스테이지 1 생성 + 청크 스트리머/플레이어/카메라/HUD 준비, 적 종류별 생성
                              # CameraFollow — 플레이어 추적, 스테이지(전투 중에는 전투 구역) 경계 제한, 피격 시 화면 흔들림
    Player/                   # PlayerMovement(A: 이동/점프/달리기/대쉬), PlayerCombat(B: 평타), PlayerSpawner
      Input/                  # RoleInput(A/B 역할별 키), PlayerControlState(미니게임 정지/영창 중 입력 잠금)
      Skills/                 # A: TeleportSkill, ElementShieldSkill/ElementShield · B: SpellCaster/SpellBook, GroundSpike
    Core/ (추가)              # StartMenu(개발자 모드 선택), GameSession, DevCheats
    Enemies/                  # EnemyController(추상 기반: 체력/경직/접촉 데미지/사망), EnemySpawner(공용 조립 헬퍼)
      Common/Slime/           # SlimeController(플레이어 추적/왕복 순찰), SlimeSpawner(구성 수치/콜라이더/프리팹 조립) — 적마다 이 형태로 폴더를 추가
      Common/Goblin/          # GoblinController(근접 고블린류 공통), GoblinSpawner(고블린/병사)
      Common/GoblinArcher/    # GoblinArcherController(거리 유지 + 화살), GoblinArcherSpawner
      Elite/MotherSlime/      # 어미 슬라임(사망 시 슬라임 3기로 분열)
      Elite/GoblinWarrior/    # 고블린 대전사 + EnrageAbility(광폭화)
      Bosses/                 # (비어 있음)
    Systems/
      Health/                 # HealthSystem (체력, 무적 플래그)
      Stamina/                # StaminaSystem (소모/재생)
      Mana/                   # ManaSystem (소모/자동 재생, A/B가 쓴·날린 마나 기록)
      Stats/                  # StatTable(스탯 문서 값), CombatStats, DamageCalculator(비율식 공식), Element
      Combat/                 # HitStun(피격 경직), CharacterLayers(레이어/몸체 충돌 규칙), SpriteColliderFitter(스프라이트 프레임에 맞춰 폴리곤 콜라이더 갱신)
      MapGeneration/          # SolidSpriteFactory, PhysicsMaterialFactory
        Chunks/               # 순수 C# 스테이지 생성기 (Unity 없이 Tools/StageGen에서 검증): ChunkData(32×24 타일 격자), StagePlanner(청크 순서·예상 시간),
                              # ChunkGenerator(청크 12종 지형/함정/적 배치), StageValidator(점프 거리 기준 통과 가능·갇힘 검사), StageGenerator
        Stage/                # 씬 쪽: ChunkStreamer(플레이어 주변 청크만 만들고 치움, 처치 기록 유지), ChunkBuilder(타일 → 콜라이더/그림),
                              # ArenaZone(전투 구역 잠금), Checkpoint, StageExit, FallRecovery(낭떠러지 복귀), StageEvents(HUD 알림)
      Traps/                  # SpikeTrap(가시), FlameJetTrap(화염 분출구), ArrowTrap(화살 발사대), CrumblingPlatform(무너지는 발판), TrapCommon
      LevelArtConfig.cs       # 아트/프리팹 설정 ScriptableObject 정의
    UI/                       # StatusBarsUI — 캐릭터 머리 위 HP/마나/기력 바, SkillHUD — 미니게임/영창/마나 탓하기 피드(IMGUI)
    Editor/                   # GameAssetSetup, PrefabSetup (에디터 전용 툴)
    Puzzles/, Data/, Enemies/Elite/, Enemies/Bosses/, Systems/StatusEffects/  # (비어 있음)
  Plugins/, Settings/
Packages/manifest.json
ProjectSettings/
Docs/                         # 기획 문서
Trash/                        # 삭제 대신 보관하는 파일함 (CLAUDE.md 규칙 참고)
```

## 스테이지 생성 (청크식)

맵은 손으로 만들지 않고 시드 하나로 매번 새로 만든다 (기본은 실행할 때마다 무작위 시드, `PlaytestBootstrap`의 `randomSeedEachRun`을 끄면 `seed` 고정).

- 스테이지 = 32×24 타일(1타일 = 1유닛) 청크를 가로로 이어 붙인 것. 청크 종류: 시작, 평지, 언덕, 낭떠러지, 가시밭, 절벽, 동굴, 무너지는 다리, 전투, 정예 전투, 휴식(체크포인트), 보스.
- `StagePlanner`가 청크 종류별 예상 시간(가안)을 더해 목표 30분이 될 때까지 이동 구간 2~3개 + 전투 구역을 번갈아 놓는다. 진행도가 오를수록 어려운 지형·함정·적이 늘고, 40%/72% 지점에 정예 전투, 마지막에 휴식 + 보스.
- 지형 규칙은 점프 능력에서 나왔다 (최대 2칸 오르기, 최대 3칸 낭떠러지). `StageValidator`가 청크마다 통과 가능 여부와 빠지면 못 나오는 구덩이를 검사하고, 실패하면 다시 만든다.
- 게임 중에는 `ChunkStreamer`가 플레이어 좌우 2청크만 씬에 만들고 3청크보다 멀어지면 치운다 (마인크래프트 방식). 쓰러뜨린 적, 돌파한 전투 구역, 켠 체크포인트는 기록해 둔다.
- 낭떠러지에 떨어지면 체력 15%를 잃고 마지막으로 안전하게 서 있던 땅으로 돌아온다. 보스 구역을 돌파하면 출구가 열리고, 들어가면 클리어.

Unity 없이 생성기를 검증하고 미리보기 그림을 만들 수 있다 (mono 필요):

```bash
mcs -out:/tmp/StagePreview.exe Tools/StageGen/StagePreview.cs Assets/Scripts/Systems/MapGeneration/Chunks/*.cs
mono /tmp/StagePreview.exe 1 /tmp/stage1.txt       # 시드 300개 검증 + 시드 1 지도 텍스트
python3 Tools/StageGen/render_stage.py /tmp/stage1.txt /tmp/stage1.png
```

## 프리팹 구조

`PlayerSpawner.Build` / `SlimeSpawner.Build`(적마다 각자의 Spawner)가 오브젝트 구성의 **단일 출처**이고, `PrefabSetup`이 이를 프리팹으로 굽는다.
런타임 `Spawn`은 `LevelArtConfig`에 프리팹이 있으면 인스턴스화하고, 없으면 `Build`로 즉석 조립해 동작한다.
플레이어 프리팹의 컴포넌트: Rigidbody2D, CapsuleCollider2D, PlayerMovement, HealthSystem, StaminaSystem, ManaSystem, PlayerCombat + 자식(Visual: SpriteRenderer/Animator, GroundCheck).
HP/마나/기력 바(`StatusBarsUI`)는 위치만 따라가는 별도 오브젝트라 프리팹 밖에서 스폰 시 붙인다.

## 작업 규칙 요약

- 삭제 금지 → `Trash/`로 `git mv`
- 폴더/기능 단위로 커밋
- Git-flow: `master` / `develop` / `feature-*` / `release-*` / `hotfix-*`, merge는 항상 `--no-ff`
- 자세한 내용은 [CLAUDE.md](CLAUDE.md)
