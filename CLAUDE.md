# CLAUDE.md

이 파일은 이 저장소에서 작업하는 Claude Code 세션이 지켜야 할 작업 규칙이다.

## 삭제 금지 → Trash/ 로 이동

- 더 이상 필요 없어진 파일/폴더는 **영구 삭제하지 않는다.**
- 대신 저장소 루트의 `Trash/`로 이동시킨다. 원래 위치의 상대 경로를 그대로 유지해서 이동한다.
  예: `Assets/Scripts/Player/OldMovement.cs` → `Trash/Assets/Scripts/Player/OldMovement.cs`
- 같은 경로에 이미 파일이 있으면 파일명 뒤에 이동 날짜(`YYYYMMDD`)를 붙여 충돌을 피한다.
- `git rm`, `Remove-Item`, `rm` 등으로 직접 삭제하지 말 것. `git mv`로 이동해서 히스토리를 보존한다.

## 폴더 단위 커밋

- 하나의 폴더(기능 단위) 작업이 끝나면 바로 커밋한다. 여러 폴더를 모아 큰 커밋으로 묶지 않는다.
- 커밋 메시지는 어떤 폴더/기능에 대한 작업인지 알 수 있게 작성한다 (예: `Scripts/Enemies: 기본 쫄몹 스크립트 추가`).
- 파일을 Trash로 이동한 경우도 별도 커밋으로 남긴다 (예: `Trash: 사용하지 않는 OldMovement 이동`).

## 브랜치 전략 (Git-flow)

- **master** : 기준이 되는 브랜치로 제품을 배포하는 브랜치
- **develop** : 개발 브랜치로 개발자들이 이 브랜치를 기준으로 각자 작업한 기능들을 Merge
- **feature-\*** : 단위 기능을 개발하는 브랜치로 기능 개발이 완료되면 develop 브랜치에 Merge
- **release-\*** : 배포를 위해 master 브랜치로 보내기 전에 먼저 QA(품질검사)를 하기 위한 브랜치
- **hotfix-\*** : master 브랜치로 배포를 했는데 버그가 생겼을 때 긴급 수정하는 브랜치

master와 develop가 항상 유지되는 메인 브랜치이고, 나머지(feature/release/hotfix)는 필요할 때마다 만들고 merge 후 정리한다.

**merge 규칙**: 브랜치를 merge할 때는 항상 `--no-ff` 옵션을 붙여서 브랜치 기록이 사라지지 않게 한다.

**워크플로**

1. master 브랜치에서 develop 브랜치를 분기한다.
2. 개발자들은 develop 브랜치에 자유롭게 커밋한다.
3. 기능 구현이 있는 경우 develop 브랜치에서 `feature-*` 브랜치를 분기한다. 완료되면 `--no-ff`로 develop에 merge한다.
4. 배포를 준비하기 위해 develop 브랜치에서 `release-*` 브랜치를 분기한다.
5. 테스트 중 발생하는 버그 수정은 release 브랜치에 직접 반영한다.
6. 테스트가 완료되면 release 브랜치를 master와 develop 양쪽에 `--no-ff`로 merge한다.
7. master 배포 후 버그가 발생하면 `hotfix-*` 브랜치를 만들어 긴급 수정하고, master와 develop 양쪽에 merge한다.

## 프로젝트 구조

폴더 구조와 각 폴더의 용도는 [README.md](README.md)를 참고. 기획 문서는 [Docs/GameDesignDocument.md](Docs/GameDesignDocument.md).
