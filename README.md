# I Blame You!

로그라이크 기반 2D 액션 게임. 방/챕터 구조로 진행하며 패턴 파훼형 전투와 맵 퍼즐을 결합한다.
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

| 입력 | 동작 |
| --- | --- |
| A / D, ← / → | 좌우 이동 |
| Space | 점프 |
| Shift (누르고 이동) | 달리기 — 기력을 지속 소모, 바닥나면 걷기로 전환 |
| Left Ctrl | 대쉬 — 기력 50 소모, 대쉬 중 피격 무적 |
| 마우스 좌클릭 | 공격 (전방 판정) |

## 폴더 구조

```
Assets/
  Art/
    Animations/Player/        # 생성된 애니메이션 클립 + PlayerAnimator.controller
    Animations/Enemies/Slime/ # 슬라임 클립 + SlimeAnimator.controller
    Materials/                # Frictionless.physicsMaterial2D (프리팹 콜라이더용)
    Sprites/                  # (비어 있음) Characters, Enemies, Environment, UI
  Audio/                      # Music, SFX (비어 있음)
  Data/                       # Characters, Enemies, Rooms — ScriptableObject 인스턴스용 (비어 있음)
  Prefabs/
    Characters/Player.prefab  # 툴이 생성 (직접 수정하지 말고 스포너 코드/툴을 수정)
    Enemies/Slime.prefab      # 툴이 생성
    Bosses/, Props/, Rooms/   # (비어 있음)
  Resources/LevelArtConfig.asset  # 바닥 타일/스프라이트/컨트롤러/프리팹 참조. 런타임에서 Resources.Load
  Scenes/                     # testScene (플레이테스트), Boot/, Chapters/
  Scripts/
    Core/                     # PlaytestBootstrap — 챕터 맵 생성 + 시작 방 구성 + 플레이어/슬라임 스폰
    Player/                   # PlayerMovement(이동/점프/달리기/대쉬), PlayerCombat(공격), PlayerSpawner
    Enemies/Common/           # EnemyController(순찰/접촉 데미지/사망), EnemySpawner (슬라임)
    Systems/
      Health/                 # HealthSystem (체력, 무적 플래그)
      Stamina/                # StaminaSystem (소모/재생)
      Mana/                   # ManaSystem (소모/자동 재생)
      Combat/                 # HitStun(피격 경직), CharacterLayers(플레이어/적 레이어와 몸체 충돌 규칙)
      MapGeneration/          # RoomGenerator(방 배치), PlatformSpawner(바닥/벽/플랫폼), RoomBuilder, SolidSpriteFactory, PhysicsMaterialFactory
      LevelArtConfig.cs       # 아트/프리팹 설정 ScriptableObject 정의
    UI/                       # StatusBarsUI — 캐릭터 머리 위 HP/마나/기력 바
    Editor/                   # GameAssetSetup, PrefabSetup (에디터 전용 툴)
    Puzzles/, Data/, Enemies/Elite/, Enemies/Bosses/, Systems/StatusEffects/  # (비어 있음)
  Plugins/, Settings/
Packages/manifest.json
ProjectSettings/
Docs/                         # 기획 문서
Trash/                        # 삭제 대신 보관하는 파일함 (CLAUDE.md 규칙 참고)
```

## 프리팹 구조

`PlayerSpawner.Build` / `EnemySpawner.BuildSlime`이 오브젝트 구성의 **단일 출처**이고, `PrefabSetup`이 이를 프리팹으로 굽는다.
런타임 `Spawn`은 `LevelArtConfig`에 프리팹이 있으면 인스턴스화하고, 없으면 `Build`로 즉석 조립해 동작한다.
플레이어 프리팹의 컴포넌트: Rigidbody2D, CapsuleCollider2D, PlayerMovement, HealthSystem, StaminaSystem, ManaSystem, PlayerCombat + 자식(Visual: SpriteRenderer/Animator, GroundCheck).
HP/마나/기력 바(`StatusBarsUI`)는 위치만 따라가는 별도 오브젝트라 프리팹 밖에서 스폰 시 붙인다.

## 작업 규칙 요약

- 삭제 금지 → `Trash/`로 `git mv`
- 폴더/기능 단위로 커밋
- Git-flow: `master` / `develop` / `feature-*` / `release-*` / `hotfix-*`, merge는 항상 `--no-ff`
- 자세한 내용은 [CLAUDE.md](CLAUDE.md)
