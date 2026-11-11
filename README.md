# I Blame You!

로그라이크 기반 2D 액션 게임. 시드로 매번 새로 만들어지는 청크식 스테이지를 지나며 패턴 파훼형 전투와 함정 지형을 결합한다.
기획은 [Docs/GameDesignDocument.md](Docs/GameDesignDocument.md), 작업 규칙은 [CLAUDE.md](CLAUDE.md) 참고. 아트 방향(팔레트·외곽선·비율)은 [Docs/ArtStyle.md](Docs/ArtStyle.md).

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
시작 화면에서 배경음악/효과음 볼륨을 맞출 수 있고, 개발자 모드·볼륨 설정과 최고 기록(가장 빠른 클리어 시간, 최고 레벨 — 개발자 모드/일반 모드 따로)은 `Core/SaveData`가 PlayerPrefs에 저장해 시작 화면에 보여준다. 최근 클리어 5판의 평균 시간·목표 시간·방 수도 함께 보여 준다 (15분 조정 판단용). 최근 게임오버 5판의 평균 진행도·주 경로 평균 순번("최근 쓰러진 곳")도 그 아래 줄에 나온다 (어려운 구간 찾기용). 클리어 결과 창에는 목표 15분 대비 빠름(초록)/느림(주황)과 이번 맵 방 수, 게임오버 창에는 쓰러진 곳(주 경로 몇 번째 방·진행도)과 버틴 시간이 한 줄로 나오고, 최근 쓰러진 곳 기록이 있으면 그 아래에 이번 판을 기록하기 전 평균과 비교한 줄("평소보다 12%p 더 갔다"는 초록, "덜 갔다"는 주황)이 붙는다 (`ClearPace`). 두 결과 창 모두 날린 마나 줄 아래에 "누구 탓?" 판정(더 많이 날린 쪽 탓, 차이 5 이하·10% 이하는 반반)이 그 사람 색으로, 창이 뜨고 0.6초 뒤 도장처럼 쾅 찍혀 나온다 (`BlameVerdict`). 요약 줄 아래에는 이번 판 B가 가장 많이 쓴 공격 마법·A가 가장 많이 쓴 방어막 속성과 횟수("B 최애 마법: 불(12회) · A 최애 방어막: 땅(4회)", 속성 이름은 그 속성 색, 실제 발동만 셈, `RunTally`)가 나오고, 이번 판에 처음 도감에 오른 유물이 있으면 같은 줄 끝에 "새 유물 2개 도감 등록!"이 초록으로 붙는다. 시작 화면에는 최근 10판의 판정 횟수와 탓을 더 받은 쪽의 장난 칭호("최근 10판 탓: A 4 · B 5 · 반반 1 — B: 마나 흘리개", 차이에 따라 마나 흘리개·마나 먹는 하마·마나 블랙홀·전설의 탓받이, 같으면 환상의 짝꿍)도 나온다. 기록 줄이 창 높이에 다 들어가지 않으면 작은 글씨로, 그래도 모자라면 첫 줄만 두고 "▼ 기록 더 보기 (H)"로 접는다(펼치면 남는 높이 안에서 스크롤). **유물 도감**(`SaveData`, `Chunks/RelicCodex` 비트 마스크, 모드별): 한 번이라도 고른 유물을 기억해 시작 화면에 "유물 도감 7/19 — 보기 (C)" 줄을 두고, 누르거나 C를 누르면 모든 유물 격자 창(모은 것은 아이콘·이름, 아직이면 어두운 실루엣과 ???, C/Esc/닫기)이 열린다. 칸에 마우스를 올리거나 방향키/WASD로 고르면(금색 테두리, 마우스를 움직이면 마우스 쪽이 우선) 창 아래에 "[A] 그림자 베일 — 설명" 한 줄이 나온다(모르는 유물은 "아직 모르는 유물"). 처음으로 도감을 다 모은 판의 결과 창에는 "새 유물 N개" 대신 금색 "★ 유물 도감 완성! ★"이 붙고, 이후 시작 화면 도감 줄 끝에 ★이 붙는다. 유물 상자 카드에는 도감에 없는 유물에 초록 NEW가 붙는다.
런 진행 저장(`Core/RunSave`): **체크포인트 제거(2026-11-10 사람 결정)로 지금은 저장하지 않는다.** 스테이지 1이 15분 안팎이고 스테이지가 하나뿐이라 "스테이지 시작 지점에서 이어하기"는 새 런과 같기 때문. 저장·이어하기 코드(시드·방·레벨/특성·유물·체력·청크 기록 → PlayerPrefs JSON, 시작 화면 **이어하기**)는 스테이지 2가 생기면 스테이지 전환 때 저장하는 용도로 남겼고, `RunSave.Version` 2로 옛 체크포인트 저장은 읽지 않는다.

| 역할 | 입력 | 동작 |
| --- | --- | --- |
| A | A / D | 좌우 이동 |
| A | Space | 점프 |
| A | Shift (누르고 이동) | 달리기 — 기력을 지속 소모 |
| A | Left Ctrl | 대쉬 — 기력 50, 대쉬 중 무적 |
| A | 방향키 + Enter | 순간이동 4칸 (마나 20, 미니게임 없음). 상/하/좌/우 중 가장 크게 누른 방향 하나. 아래는 발판을 뚫고 내려가되 발 밑 2.5칸 안에 설 곳이 없으면(낙사 위험) 거리를 줄이거나 발동하지 않음 (마나 소모 없음) |
| A | 넘패드 1~5 | 불/물/바람/땅/무 방어막 → 화면에 뜬 W/A/S/D 순서 입력 (입력 중 정지, 틀리면 마나만 소모) |
| B | 마우스 좌클릭 | 평타 (마우스 쪽으로) |
| B | 상단 1~5 | 불/물/바람/땅/무 공격 스킬 → 영창문 타이핑 후 Enter (오타면 마나만 소모, Esc 취소) |
| 개발자 모드 | 상단 1~5 / 6~9·0 | A 방어막 / B 공격 스킬 즉시 발동 |
| 개발자 모드 | F1 / F2 / F3 / F4 | 무적 / 마나 무한 / 주변 적 처치 / 체력 회복 |
| 공통 | ` (백쿼트) | 방 격자 모드: 지도 크게 펼치기/닫기 (오른쪽 아래에는 늘 작은 미니맵). 영창 중에는 무시 |
| 공통 | Esc | 일시정지 메뉴 (배경음악/효과음 볼륨, 계속하기/다시 시작/시작 화면). 옆 패널에 이번 런 특성·유물과 지금까지 날린 마나·지금 판정("지금은 B 탓")이 나온다. B의 영창 중에는 영창 취소, Tab 특성 창이 열려 있으면 창 닫기 |
| 공통 | R / M (쓰러졌을 때) | 다시 시작 / 시작 화면 (사망 연출이 끝나고 게임오버 창이 뜬 뒤) |

수치는 기획 스탯 문서를 옮긴 `Systems/Stats/StatTable.cs`가 기준이고, 피해는 비율식(`공격 × 100 / (100 + 방어)`)으로 계산한다.
고블린 4종과 스킬 이펙트 그림은 `Tools/ArtGen/generate_sprites.py`로 만든 임시 아트다 (`python3 Tools/ArtGen/generate_sprites.py`로 다시 생성).
함정/발판/출구 그림도 같은 스크립트가 `Assets/Resources/Stage/`에 만든다.
배경음악(메뉴/던전/후반 던전/보스 — 보스·중간보스 전투 구역이 잠기면 보스 곡, 돌파하면 진행도에 맞는 던전 곡: 50% 전 `Dungeon`(124 BPM A단조), 50% 뒤 `Dungeon2`(138 BPM E단조, 16분 하이햇·반짝이 아르페지오). 외길 모드는 50% 넘은 구역을 돌파할 때 바뀜), 마법 발동·적중 효과음, 동작 효과음(점프/대쉬/플레이어 피격/적 사망/레벨업, 높은 곳 착지 `Land`(낙하 속도 11 넘을 때), 슬라임 무리 피격 `SquishHit`(마법 적중음에 겹침, 0.12초 공용 쿨다운)), 진행 효과음(봉인문 열림/보상 구슬/구역 잠김/스위치, 별똥 조각 금별이 닿을 때 맑은 `StarShard`), 결과 징글(클리어 팡파르·게임오버, 이때 배경음악은 꺼짐)은 `Tools/AudioGen/generate_audio.py`로 합성한 임시 사운드다 (`python3 Tools/AudioGen/generate_audio.py`로 다시 생성).

## 폴더 구조

```
Assets/
  Art/
    Animations/Player/        # 생성된 애니메이션 클립 + PlayerAnimator.controller
    Animations/Enemies/Slime/ # 슬라임 클립 + SlimeAnimator.controller
    Materials/                # Frictionless.physicsMaterial2D (프리팹 콜라이더용)
    Sprites/Characters/Wizard/ # 플레이어 마법사 프레임(투명 PNG, 300x256 캔버스, 발 y=193). Idle 7장(수정 빛이 단계적으로 켜짐, 왕복 재생) + Walk 8 / Run 8 / Jump 2 / Dash 6(aqua 잔상) / Attack 8장(지팡이를 들었다 내지르며 수정 번쩍임). 모두 `Tools/ArtGen/generate_wizard.py`가 같은 몸으로 그린다 (사람이 그린 예전 Idle은 Trash에 보존). 다시 그린 뒤에는 툴 재실행
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
  Resources/Effects/{Dust,LevelUp,DeathPop,Sparkle,ChestOpen,RelicBanner,...}/ # 착지 먼지, 레벨업 빛기둥, 적 사망 별·하트, 유물 상자 반짝임·열기, 유물 이름 띠 (Tools/ArtGen/generate_vfx.py, Systems/Effects/VfxPlayer)
  Resources/Effects/{Teleport,Shield,Slash}/ # 순간이동·방어막·베기 (Tools/ArtGen/generate_skill_fx.py, 없으면 LevelArtConfig 옛 프레임)
  Resources/Effects/StunStars/ # 적 경직 어질어질 별 12프레임 (generate_skill_fx.py, Systems/Effects/StunStars)
  Resources/UI/HUD/           # HUD 캡슐 바·빛 테두리·패널·하트·별·왕관·깃발 (Tools/ArtGen/generate_hud.py, UI/SkillHUD.Bars·UI/CapsuleBar)
  Resources/Audio/{Music,SFX}/    # 배경음악, Cast_/Hit_<속성>, Jump/Dash/Hurt/EnemyDie/LevelUp·Checkpoint/GateOpen/Pickup/ArenaLock·Jingle_* 효과음 (런타임 로드, Tools/AudioGen)
  Resources/Stage/<이름>/         # 가시/화염 분출구/화살 발사대/발판/무너지는 발판/전투 구역 문/구역 보상 구슬/봉인문·문양/출구 그림 (런타임 로드, Tools/ArtGen)
  Resources/Bosses/SlimeKing/     # 슬라임 킹 왕관, 내려찍기 그림자 (런타임 로드, Tools/ArtGen)
  Scenes/                     # testScene (플레이테스트), Boot/, Chapters/
  Scripts/
    Core/                     # PlaytestBootstrap — 시드로 스테이지 1 생성 + 청크 스트리머/플레이어/카메라/HUD 준비, 적 종류별 생성
                              # CameraFollow — 플레이어 추적, 스테이지(전투 중에는 전투 구역) 경계 제한, 피격 시 화면 흔들림
    Player/                   # PlayerMovement(A: 이동/점프/달리기/대쉬), PlayerCombat(B: 평타), PlayerSpawner
      Input/                  # RoleInput(A/B 역할별 키), PlayerControlState(미니게임 정지/영창 중 입력 잠금)
      Skills/                 # A: TeleportSkill, ElementShieldSkill/ElementShield · B: SpellCaster/SpellBook, GroundSpike
      Progression/            # PlayerProgression(경험치/레벨/특성 포인트), TraitTree(던그리드식 특성 5갈래), LastStandGuard
    Core/ (추가)              # StartMenu(개발자 모드 선택, 볼륨, 최고 기록, 이어하기), GameSession, SaveData(PlayerPrefs 설정·최고 기록), RunSave(런 진행 저장), DevCheats
    Enemies/                  # EnemyController(추상 기반: 체력/경직/접촉 데미지/사망), EnemySpawner(공용 조립 헬퍼)
      Common/Slime/           # SlimeController(플레이어 추적/왕복 순찰), SlimeSpawner(구성 수치/콜라이더/프리팹 조립) — 적마다 이 형태로 폴더를 추가
      Common/Goblin/          # GoblinController(근접 고블린류 공통), GoblinSpawner(고블린/병사)
      Common/GoblinArcher/    # GoblinArcherController(거리 유지 + 화살), GoblinArcherSpawner
      Elite/MotherSlime/      # 어미 슬라임(사망 시 슬라임 3기로 분열)
      Elite/GoblinWarrior/    # 고블린 대전사 + EnrageAbility(광폭화)
      Bosses/                 # BossEvents(보스 체력바 값)
      Bosses/SlimeKing/       # 스테이지 1 보스: SlimeKingController(패턴 6개·페이즈 2개), SlimeKingSpawner, SlimeBrood(소환 무리·합체), SlimeKingTentacle, SlimeKingCrown
    Systems/
      Health/                 # HealthSystem (체력, 무적 플래그)
      Stamina/                # StaminaSystem (소모/재생)
      Mana/                   # ManaSystem (소모/자동 재생, A/B가 쓴·날린 마나 기록)
      Stats/                  # StatTable(스탯 문서 값), CombatStats, DamageCalculator(비율식 공식), Element
      Combat/                 # HitStun(피격 경직, 몸이 뒤로 젖혀지는 경직 연출), HitFlash/HitDebris(피격 시 0.08초 흰색 번쩍임 + 파편, HitStun이 자동으로 붙임), CharacterLayers(레이어/몸체 충돌 규칙), SpriteColliderFitter(스프라이트 프레임에 맞춰 폴리곤 콜라이더 갱신)
      MapGeneration/          # SolidSpriteFactory, PhysicsMaterialFactory
        Chunks/               # 순수 C# 스테이지 생성기 (별도 어셈블리 IBlameYou.StageGeneration, Unity 없이 Tools/StageGen에서도 검증): ChunkData(32×24 타일 격자), StagePlanner(청크 순서·예상 시간),
                              # ChunkGenerator(청크 14종 지형/함정/적 배치), StageValidator(점프 거리 기준 통과 가능·갇힘 검사), StageGenerator
        Stage/                # 씬 쪽: ChunkStreamer(플레이어 주변 청크만 만들고 치움, 처치 기록 유지), ChunkBuilder(타일 → 콜라이더/그림),
                              # ArenaZone(전투 구역 잠금), ArenaReward(구역 돌파 회복 구슬), RelicChest(정예 구역 유물 상자), StageRecords(저장용 청크 기록), StageExit, FallRecovery(낭떠러지 복귀), StageEvents(HUD 알림)
      Traps/                  # SpikeTrap(가시), FlameJetTrap(화염 분출구), ArrowTrap(화살 발사대), CrumblingPlatform(무너지는 발판, 플레이어·적이 밟으면 무너짐), MovingPlatform(움직이는 발판), TrapCommon
      LevelArtConfig.cs       # 아트/프리팹 설정 ScriptableObject 정의
    UI/                       # StatusBarsUI — 캐릭터 머리 위 HP/마나/기력 바, SkillHUD — 체력·마나 캡슐 바(SkillHUD.Bars, 그림 Tools/ArtGen/generate_hud.py), 미니게임/영창/마나 탓하기 피드(IMGUI)
                              # SkillHUD.LowHealth — 체력 30% 이하 화면 경고등, SkillHUD.Pause — Esc 일시정지(볼륨), SkillHUD.Death — 사망 연출. 게임오버/클리어 창에 런 기록(Core/RunStats)
                              # SkillHUD.Traits — 레벨 막대 + Tab 특성 창, SkillHUD.Boss — 보스 체력바, SkillHUD.Relics — 유물 고르기 창·가진 유물
                              # EnemyHealthBar — 적 머리 위 체력 바(맞으면 잠깐 보임), CapsuleBar — 머리 위 둥근 캡슐 바 + 흰 잔상 공용
    Editor/                   # GameAssetSetup, PrefabSetup (에디터 전용 툴)
    Puzzles/                  # ElementGate(속성 마법으로 여는 봉인문)
    Data/                     # (비어 있음)
  Plugins/, Settings/
Assets/Tests/EditMode/        # EditMode 테스트 (IBlameYou.Tests.EditMode.asmdef)
Packages/manifest.json
ProjectSettings/
Docs/                         # 기획 문서
Trash/                        # 삭제 대신 보관하는 파일함 (CLAUDE.md 규칙 참고)
```

## 스테이지 생성 (청크식)

**방 격자 모드 (기본, `PlaytestBootstrap.roomGrid`)**: 던그리드식으로 방들이 격자로 이어진 맵 ([Docs/LevelDesign.md](Docs/LevelDesign.md)). `RoomLayout`이 시드로 방 배치(주 경로 + 곁방, 중간보스 55%)를, `RoomShaper`가 방마다 지형(좌우 방은 아래 청크 생성기 + 벽·문, 위/아래 문이 있는 방은 탑 지형)을 만든다. `ChunkStreamer`는 지금 방 하나만 만들어 두고, 방 가장자리·천장/바닥 구멍(문)을 지나면 옆 방의 반대쪽 문 자리로 옮기며 카메라를 그 방에 가둔다. 곁방: 보물 방 = 유물 상자, 샘 방 = 회복 구슬. 중간보스(쌍둥이 기사)를 잡으면 체력 50% 즉시 회복 + 유물 상자 확정. 방을 옮기면 떠난 방의 적은 치우고(다시 들어가면 다시 나옴), 미니맵(`UI/MinimapUI`, 던그리드 방식 — 표시 규칙 `MinimapRules`)이 가 본 방만 칸으로 그리고 안 가 본 방은 칸 없이 가 본 방에서 뻗은 통로 실선만 보이며, 지금 방·방 종류 색(보스 빨강, 중간보스 주황, 정예 보라, 보물 금색, 휴식·샘 하늘색)과 마법 스위치 퍼즐 방의 룬 속성 색 점(왼쪽 위)은 가 본 방에만 보여 준다. 돌파한 전투·정예·중간보스 방 칸에는 오른쪽 아래에 초록 체크가 붙는다. 처음 들어간 방의 칸은 0.3초 동안 "톡" 커졌다 줄며 나타나고 흰 반짝임이 한 번 지나간다. 런 저장은 방 번호로 이어 하고, 모드가 다른 저장은 새 런으로 시작한다. 끄면 아래의 예전 외길 스테이지.

맵은 손으로 만들지 않고 시드 하나로 매번 새로 만든다 (기본은 실행할 때마다 무작위 시드, `PlaytestBootstrap`의 `randomSeedEachRun`을 끄면 `seed` 고정).

- 스테이지 = 32×24 타일(1타일 = 1유닛) 청크를 가로로 이어 붙인 것. 청크 종류: 시작, 평지, 언덕, 낭떠러지, 가시밭, 절벽, 동굴, 무너지는 다리, 봉인문(맵 퍼즐), 움직이는 발판(컨트롤 퍼즐), 전투, 정예 전투, 휴식(체크포인트), 보스.
- `StagePlanner`가 청크 종류별 예상 시간(가안)을 더해 목표 15분(2026-11-10 사람 결정, 30분에서 줄임)이 될 때까지 이동 구간 1~3개 + 전투 구역을 번갈아 놓는다. 진행도가 오를수록 어려운 지형·함정·적이 늘고, 30% 지점에 정예 전투(그다음 고비는 55% 중간보스), 마지막에 숨 고르기 + 보스. 방 격자 모드는 중간보스 방과 그 뒤 숨 고르기 방 몫을 빼고 계획해(`RoomLayout.PlanSeconds`) 전체가 15분 안팎(시드 200개 평균 약 15.4분, 주 경로 19방 안팎).
- 지형 규칙은 점프 능력에서 나왔다 (최대 2칸 오르기, 최대 3칸 낭떠러지). `StageValidator`가 청크마다 통과 가능 여부와 빠지면 못 나오는 구덩이를 검사하고, 실패하면 다시 만든다.
- 게임 중에는 `ChunkStreamer`가 플레이어 좌우 2청크만 씬에 만들고 3청크보다 멀어지면 치운다 (마인크래프트 방식). 쓰러뜨린 적, 돌파한 전투 구역, 주운 구역 보상은 기록해 둔다.
- 정예(어미 슬라임·고블린 전사)가 나오는 구역이 잠기면 경고음 `EliteWarn` + 화면 가장자리 보라 깜빡임 0.6초(`ScreenFlash.EdgeWarn`) + "정예 등장!" 안내.
- 일반·정예 전투 구역을 돌파하면 가운데에 회복 구슬(`ArenaReward`)이 떨어진다. 닿으면 최대 체력 30%(생명 5 특성이면 45%), 최대 마나 50% 회복. 그림은 두근거리는 통통한 하트 + aqua 마나 고리, 주우면 하트·별이 톡톡 튄다 (`Resources/Effects/HeartOrb`·`HeartPop`, 없으면 예전 구슬). 줍지 않고 떠나도 다시 오면 그대로 있다 (보스 구역은 출구가 보상이라 없음).
- 정예 전투 구역은 구슬 왼쪽에 **유물 상자**(`RelicChest`)도 남긴다. 닿으면 게임이 멈추고 유물 3개 중 하나를 1/2/3 키나 클릭으로 고른다 (A/B 공용, 한 런에 같은 유물은 한 번). 후보는 시드와 청크 번호로 정해진다(`RelicBook.Offer`). 영창·방어막 미니게임·특성 창·일시정지 중에는 열리지 않고 끝난 뒤 열린다.
  유물 24종(`Player/Relics`): 마력 수정(최대 마나 +20), 무쇠 망토(방어 +8), 바람 장화(이동속도 +8%), 분노의 부적(주는 피해 +12%), 생명의 씨앗(최대 체력 +20), 샘물 반지(마나 재생 +25%), 매의 눈(치명타 +8%) — 특성 약 4포인트 분량 — 과 특수 효과 16종: 깃털 부적(낭떠러지 낙하 피해 15% → 5%), 흡혈 송곳니(적 처치 시 쓰러진 적 자리에서 작은 붉은 하트가 날아와 닿을 때 말랑한 "뽁" 소리와 함께 체력 +3 — `VfxPlayer.VampireHeart`), 집중 구슬(실패로 날린 마나 25% 돌려받기, 신비 10과 합산), 그림자 베일(대쉬가 끝난 뒤 0.5초 더 무적), 피의 계약(마나가 모자라면 모자란 만큼 체력으로 대신 냄, 40초에 한 번, 체력 1은 남김), 불사조 깃털(쓰러질 피해를 한 번 버티고 체력 30%로, 특성 버티기와 합산), 되감기 모래시계(낭떠러지에 떨어져도 2번은 피해 없이 복귀, 남은 횟수는 이어하기에도 저장), 쌍둥이 반지(A의 방어막이 발동하면 마나 +5, 플레이어 둘레에 하늘색 빛 고리가 퍼진다 — `Effects/ManaRingFx`), 탐험가의 나침반(미니맵에 안 가 본 방도 흐린 칸·흐린 종류 아이콘으로 보이고 통로 실선이 지도 전체를 잇는다. 고르면 게임이 다시 흐를 때 큰 지도가 1.5초 저절로 펼쳐지고 흐린 칸이 가까운 방부터 차례로 나타난다), 네잎 클로버(다음 유물 상자부터 후보 3개 → 4개, 4번 키로도 고른다. 화면이 좁으면 유물 창 카드 폭이 줄고, 180px보다 좁아지면 2×2로 놓인다), 응원 리본(B의 공격 마법이 발동하면 마나 +4, 주황 빛 고리 — 쌍둥이 반지의 B 쪽 짝), 별똥 조각(적 처치 시 15% 확률로 쓰러진 적 자리에서 작은 금별이 위로 톡 튀었다가 플레이어에게 빨려 들어와, 닿을 때 마나 +6 — 흡혈 송곳니의 마나 짝, `Effects/StarShardFx`), 용사의 훈장(정예 구역을 돌파하면 최대 체력 15% 회복, 머리 위로 금 메달이 반짝 떠올랐다 사라진다 — `ChunkStreamer.OnArenaCleared`, `Effects/MedalGlintFx`), 도토리 주머니(레벨이 오를 때마다 체력 +10 — `PlayerProgression.LeveledUp`), 꿀단지(전투 구역 보상 구슬 체력 회복 +15%p, 생명 5 특성과 합산, 주우면 머리 위로 작은 꿀단지가 톡 떠오르고 알림에 "· 꿀단지 +15%"가 붙는다 — `ArenaReward`), 젤리 방석(플레이어가 밟은 무너지는 발판이 0.55초 대신 0.9초 분홍빛으로 말랑하게 눌렸다 튄 뒤 무너진다 — `CrumblingPlatform`), 별사탕(B, 레벨이 오를 때마다 마나 +30, 머리 위 하늘색 고리). 회복은 모두 실제 찬 양을 머리 위 초록 숫자로 띄우고(`DamageNumbers.ShowHeal`, 가득 차 있으면 생략), HUD 체력 바는 늘어난 부분이 연두색으로 0.4초 빛난다. HUD 유물 목록에서 모래시계·불사조 깃털은 남은 횟수를 숫자 배지로, 다 쓰면 아이콘을 어둡게 보인다.
- **봉인문 (맵 퍼즐)**: 진행도 25~85% 구간에 스테이지당 최대 2개. 가운데 마력 벽이 천장까지 길을 막고, 문 색·문양과 같은 속성(불/물/바람)의 B 공격 마법을 맞히면 열린다. 다른 속성이나 평타는 튕겨 나가며 필요한 속성을 알려 준다. 진행도 60% 이후의 봉인문은 문양이 둘이라 서로 다른 두 속성을 아래 문양부터 순서대로 맞혀야 한다 (맞힌 개수도 기록된다). 연 문은 청크를 다시 만들어도 열린 채로 남는다 (`Puzzles/ElementGate`, 투사체는 `IElementReceiver`로 속성을 전달). **협동 봉인문**: 진행도 25% 이후의 보물 곁방은 상자 앞을 문양 둘짜리 봉인문이 막는다. 아래 문양(방어막 거품 표시)은 A가 그 속성 방어막을 두른 채 문에 닿아야 풀리고(마법은 튕김), 위 문양은 B가 다른 속성 마법을 맞혀 푼다 (좌우 곁방 24열, 세로 곁방 8열·상자 4열 — `RoomShaper.HasPairGate`).
- **움직이는 발판 (컨트롤 퍼즐)**: 진행도 20% 이후. 점프로 못 건너는 7~10칸 낭떠러지를 3칸짜리 발판이 오간다 (초당 2.2유닛, 양 끝에서 0.8초 멈춤). 위에 선 캐릭터를 실어 나르며, 검증기는 발판이 지나는 칸을 모두 발판으로 본다 (`Traps/MovingPlatform`). 진행도 70% 이후에는 절반 확률로 갈아타기 변형: 9칸 낭떠러지 두 개 사이에 2칸 기둥이 있고, 발판 두 개가 반 박자 엇갈려 오간다 (첫 발판이 기둥에 닿으면 둘째 발판이 반대편에서 기다리는 이어달리기, 놓치면 기둥에서 한 바퀴 대기). 진행도 50% 이후 한 발판 낭떠러지의 절반은 **마법 스위치 발판**: 발판이 출발 쪽에 멈춰 있고, 건너편 절벽 첫 칸의 룬(`Puzzles/PlatformSwitch`, 봉인문 문양, 길을 막지 않는 트리거)에 같은 속성 B 마법을 맞히면 1.2초 뒤 출발해 10초 동안 오간다 (시간이 끝나도 끝점까지는 마저 간다, 다시 맞히면 연장). 진행도 80% 이후 갈아타기 변형의 절반은 **이중 스위치**: 두 발판 모두 멈춰 있고 기둥 첫 칸·건너편 절벽 첫 칸에 서로 다른 속성 룬 (k번째 룬 ↔ k번째 스위치 발판). 플레이어 마법 투사체는 트리거에 붙은 `IElementReceiver`에도 속성을 전달한다 (쏜 자리에서 이미 겹친 장치는 건너뛴다).
- 낭떠러지에 떨어지면 체력 15%를 잃고 마지막으로 안전하게 서 있던 땅으로 돌아온다. 보스 구역을 돌파하면 출구가 열리고, 들어가면 클리어.

## 레벨업 특성 (던그리드식)

적을 쓰러뜨리면 경험치가 쌓이고 레벨이 오를 때마다 특성 포인트 1을 받는다. 다음 레벨까지 경험치는 `60 + 25 × (레벨 - 1)`이라 스테이지 1 보스 앞에서 13~14레벨쯤 된다.
Tab으로 특성 창을 열면 게임이 멈추고, 다섯 갈래 중 하나에 포인트를 찍는다. 포인트는 A와 B가 함께 쓰는 하나의 주머니다.

| 갈래 | 주로 | 포인트당 | 5 | 10 | 20 |
|---|---|---|---|---|---|
| 분노 | B | 주는 피해 +3% | 평타 마나 5 → 3 | 치명타 +10% | 처치 시 마나 +5 |
| 신속 | A | 이동속도 +2% | 대쉬 기력 50 → 35 | 순간이동 4 → 6 | 대쉬 거리 1.5배 |
| 인내 | A | 방어력 +2 | 피격 무적 0.2 → 0.5초 | 디버프 저항 +25 | 체력 30% 이하 피해 -25% |
| 신비 | 공용 | 최대 마나 +5 | 마나 재생 +30% | 실패로 날린 마나 절반 환급 | 모든 마나 소모 -20% |
| 생명 | 공용 | 최대 체력 +5 | 회복 구슬 체력 45% | 처치 시 체력 +2 | 한 번 쓰러질 피해를 30%로 버팀 |

## 스테이지 1 중간보스: 쌍둥이 슬라임 기사

방 격자 모드에서 주 경로 55% 지점의 중간보스 방(정예 전투 구역 지형)에 들어가면 투구 쓴 불 기사·물 기사 둘이 0.3초 간격으로 위에서 떨어져 쿵 착지하며 나온다(충격파 고리·흙먼지·투구 반짝임·화면 흔들림, 떨어지는 동안은 기사가 꺼져 있어 맞거나 공격하지 않는다 — `Enemies/Bosses/BossEntrance`) (`Enemies/Bosses/SlimeKnights`, [Docs/MidBossDraft.md](Docs/MidBossDraft.md) 후보 C). 체력 각 200, 화면 위 보스 체력바에 합쳐 보인다. 둘 다 쓰러뜨리면(슬라임 킹도 마지막 몸을 쓰러뜨리면) 0.35초 슬로모션(0.3배) + 화면 흰 번쩍임 + 큰 별·하트 폭발 + "이름 격파!" 띠 (`Systems/Effects/BossDefeatFx`, 그사이 일시정지·특성 창이 시간을 바꾸면 그쪽을 따른다).

- **불 기사**: 슬라임 도약 + **돌진** (주황으로 깜빡이며 0.6초 예고 후 바닥을 빠르게 미끄러짐, 불 피해 + 화상). 불 몸이라 물 마법에 약하다.
- **물 기사**: 슬라임 도약 + **물방울 방패** (가까이 가면 3초 동안 멈춰 서서 받는 피해 75% 감소·경직 안 됨). **바람·땅 마법**을 맞히면 방패가 유리처럼 조각나 흩어지며(aqua 반짝임, `Resources/Effects/ShieldShatter`) 기사가 잠깐 움찔한다.
- 한쪽이 쓰러지면 남은 쪽이 바로 **광폭화** (12초 동안 이동 +40%, 기술 재사용 빨라짐, 초당 체력 2 감소, 붉게 맥동).
- 둘이 붙어 있으면 화상·젖음이 서로 상쇄돼 속성 공격이 꼬이므로 떼어 놓고 하나씩 잡는 것이 요령. 돌파하면 정예 구역처럼 회복 구슬 + 유물 상자.

## 스테이지 1 보스: 슬라임 킹

체력 1200 · 방어 25 · 물리 35 · 마나 100(초당 4) · 기력 100(초당 10) · 경험치 1000. 패턴 중에는 경직되지 않는다. 보스 방에 들어가면 위에서 떨어져 쿵 착지한다(기사보다 큰 충격파·흔들림, 왕관 반짝임, "슬라임 킹" 이름 띠 — `BossEntrance`).

- 1페이즈: 패턴 1~4. 체력 50%에서 패턴 5(분열)로 2기가 되며 2페이즈 시작, 2페이즈는 패턴 2·3·4·6 (소환 무리가 없을 때만 1).
- 패턴 1 소환 (마나 60): 어미 슬라임 3체. 60초 안에 분열한 슬라임까지 다 잡지 못하면 킹에게 돌아가 합체하고, 어미 하나당 8%·슬라임 하나당 3% 체력을 회복한다.
- 패턴 2 내려찍기 (기력 30): 6.5칸 떠올라 0.9초 동안 플레이어 머리 위를 따라오며 그림자로 예고한 뒤 낙하 (폭 4.5칸, 피해 ×1.2).
- 패턴 3 촉수 (기력 25): 촉수 4가닥(하나는 정확히, 나머지는 ±35°)을 사거리 9칸으로 뻗는다. 닿으면 2초 동안 이동속도 절반, 5초 안에 11칸 넘게 멀어지지 못하면 끌려가 피해 ×1.2 + 1초 경직.
- 패턴 4 박치기: 웅크렸다가 도약해 덮친다.
- 패턴 5 분열: 체력·최대 체력·경험치를 반씩 나눈 2기로 갈라지고 몸이 0.8배로 작아진다.
- 패턴 6 발광 (체력 5%): 2페이즈 전용. 빛나며 구역 안 무작위 위치를 5번 빠르게 내려찍는다 (피해 ×0.8).

Unity 없이 생성기를 검증하고 미리보기 그림을 만들 수 있다 (mono 필요):

```bash
mcs -out:/tmp/StagePreview.exe Tools/StageGen/StagePreview.cs Assets/Scripts/Systems/MapGeneration/Chunks/*.cs
mono /tmp/StagePreview.exe 1 /tmp/stage1.txt       # 시드 300개 검증 + 시드 1 지도 텍스트
python3 Tools/StageGen/render_stage.py /tmp/stage1.txt /tmp/stage1.png
```

## 테스트 (EditMode)

`Assets/Tests/EditMode`의 NUnit 테스트를 Unity **Window > General > Test Runner > EditMode**에서 돌릴 수 있고, PR마다 Unity CI(`editmode` 잡)도 같은 테스트를 돌린다.

- 테스트 어셈블리(`IBlameYou.Tests.EditMode.asmdef`)는 `Assembly-CSharp`를 참조할 수 없어서, 테스트할 순수 로직은 별도 asmdef로 분리한다.
  지금은 청크 생성기(`Systems/MapGeneration/Chunks`, `IBlameYou.StageGeneration.asmdef`), 유물 데이터(`Player/Relics/Data`, `IBlameYou.Relics.asmdef`), 데미지 공식·속성 상성(`Systems/Stats/Formula`, `IBlameYou.Formula.asmdef`, `Element` enum 포함)이 분리되어 있다 (셋 다 UnityEngine 참조 없음). 이 폴더 코드는 바깥 게임 코드(MonoBehaviour 등)를 참조하면 안 된다.
- `StageGeneratorTests`: 시드 40개로 모든 청크·대체 청크 검증 통과, 이음새 높이, 시작/보스 청크와 출구, 예상 시간 ±15%(15분 기준), 같은 시드 = 같은 지형, 봉인문 배치 규칙(진행도 구간, 최대 1개)과 우회 불가, 후반 봉인문은 서로 다른 두 속성, 움직이는 발판 없이는 못 건넘(갈아타기는 발판마다), 마법 스위치 룬 위치(건너편 첫 칸, 출발 쪽에서 12칸·쏘는 높이 안), 배치 규칙(정예 30%와 그 뒤 휴식, 휴식 간격, 지형별 진행도 문턱, 같은 이동 청크 연속 금지).
- `StagePlannerPickTests`: 이동 청크 뽑기의 반올림 대비 경로도 직전 종류와 겹치지 않고 진행도 문턱을 지킨다.
- `DamageFormulaTests`: 비율식 공식 값(방어 -50 하한 포함), 방어가 오르면 피해가 줄기만 함, 속성 상성이 한 바퀴 순환(각 속성은 하나를 이기고 하나에 짐).
- `RelicBookTests`: 유물 후보 3개가 서로 다름, 같은 시드·청크 = 같은 후보, 가진 유물 제외, 남은 게 적으면 줄어듦, 정예 구역 두 곳의 후보가 다름.

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
