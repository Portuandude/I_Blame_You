# I Blame You!

로그라이크 기반 액션 게임. 방/챕터 구조로 진행하며 패턴 파훼형 전투와 맵 퍼즐을 결합한다.
자세한 기획은 [Docs/GameDesignDocument.md](Docs/GameDesignDocument.md) 참고.

## 개발 환경

- Engine: Unity 6000.0.35f1 (LTS) — 설치된 에디터 버전이 다르면 `ProjectSettings/ProjectVersion.txt`를 실제 버전으로 수정하거나, Unity Hub에서 이 프로젝트를 열 때 뜨는 버전 전환/설치 안내를 따를 것.
- Unity Hub → Open → 이 폴더(`I Blame You!`) 선택
- 처음 열면 Unity가 `Library/` 등 나머지 캐시 폴더를 자동 생성함 (정상)
- Package Manager에서 `Packages/manifest.json`에 명시된 패키지(2D Sprite, Tilemap, Input System, TextMeshPro, Timeline)가 정상 설치되는지 확인

## 폴더 구조

```
Assets/
  Art/          # 스프라이트, 애니메이션, 머티리얼
    Sprites/Characters, Enemies, Environment, UI
    Animations/
    Materials/
  Audio/        # Music, SFX
  Prefabs/      # Characters, Enemies, Bosses, Rooms, Props
  Scenes/       # Boot(부트/타이틀), Chapters(챕터별 씬)
  Scripts/
    Core/               # GameManager, SceneLoader 등 전역 시스템
    Player/
    Enemies/            # Common(쫄몹), Elite(정예몹), Bosses(중간/챕터보스)
    Systems/            # Health, StatusEffects, MapGeneration(방/맵 랜덤 생성)
    UI/
    Puzzles/            # 맵 퍼즐(컨트롤/스킬 퍼즐)
    Data/               # ScriptableObject 정의(클래스)
  Data/                 # ScriptableObject 에셋 인스턴스 (Enemies, Rooms, Characters)
  Resources/
  Plugins/
  Settings/             # 렌더 파이프라인 등 설정 에셋
Packages/manifest.json
ProjectSettings/
Docs/                   # 기획 문서
Trash/                  # 삭제 대신 보관하는 파일함 (아래 규칙 참고)
```

## 작업 규칙

이 프로젝트의 협업/커밋 규칙은 [CLAUDE.md](CLAUDE.md)에 정리되어 있음 (삭제 금지 → `Trash/` 이동, 폴더 단위 커밋 등).
