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

## 프로젝트 구조

폴더 구조와 각 폴더의 용도는 [README.md](README.md)를 참고. 기획 문서는 [Docs/GameDesignDocument.md](Docs/GameDesignDocument.md).
