---
description: 자동 개발 1회 실행. 백로그에서 작업을 골라 구현하고 develop 대상 PR을 올린다.
---

# /auto-dev — 무인 자동 개발 1회분

이 명령은 5시간마다 예약 실행되는 무인 세션이 쓴다. 사람에게 질문하지 말고 끝까지 진행한다.
저장소 루트의 `CLAUDE.md` 규칙이 이 문서보다 우선한다 (삭제 금지 → Trash, 폴더 단위 커밋, Git-flow, `--no-ff`).

설정값은 `Docs/AutoDev/README.md`의 "설정" 표를 읽어서 쓴다 (실행당 작업 수, 머지 방식).

## 0. 준비

1. `git fetch origin` 후 `CLAUDE.md`, `README.md`, `Docs/GameDesignDocument.md`, `Docs/AutoDev/README.md`, `Docs/AutoDev/BACKLOG.md`, `Docs/AutoDev/LOG.md`의 최근 3개 기록을 읽는다.
2. `master` 브랜치는 절대 건드리지 않는다. release/hotfix 브랜치도 만들지 않는다.

## 1. 이전 실행 정리 (새 작업보다 먼저)

`gh api repos/Portuandude/I_Blame_You/pulls?state=open` 으로 head가 `feature-auto-`로 시작하는 열린 PR을 찾는다.

- CI(`Unity CI` 워크플로)가 **실패**한 PR이 있으면: 그 브랜치를 체크아웃해 실패 로그(`gh api .../actions/runs`, job 로그)를 읽고 최소 수정으로 고친 뒤 푸시한다. 이번 실행의 첫 작업으로 센다.
- CI가 **진행 중**인 PR이 있으면: 그 브랜치 위에서 이어서 작업한다 (같은 PR에 커밋이 쌓인다).
- 열린 PR이 없으면: `origin/develop`에서 `feature-auto-YYYYMMDD-HHMM` (UTC) 브랜치를 새로 만든다.
- 같은 실패를 두 번 고쳤는데도 CI가 계속 실패하면, 그 PR은 그대로 두고 `LOG.md`에 "사람 확인 필요"로 적은 뒤 develop에서 새 브랜치로 진행한다.

## 2. 작업 반복

설정의 "실행당 작업 수"만큼 아래를 반복한다. 백로그가 비면 GDD의 "구현 로드맵"과 코드 상태를 보고 다음 작업 3~5개를 `BACKLOG.md`에 먼저 추가한 뒤 진행한다.

1. **고르기**: `BACKLOG.md`의 `TODO` 중 맨 위 항목. `BLOCKED`나 "사람 필요"가 붙은 항목은 건너뛴다.
2. **계획**: 파일 3개 이상을 건드리는 작업이면 `planner` 에이전트로 계획을 받는다. 작으면 바로 구현.
3. **구현**: 기존 구조를 따른다 (스포너 코드가 단일 출처, 프리팹은 직접 수정하지 않음, 에디터 툴은 `Scripts/Editor`).
   - 새 `.cs` 파일이나 폴더를 `Assets/` 아래 만들면 반드시 `.meta` 파일도 만든다. GUID는 `python3 -c "import uuid;print(uuid.uuid4().hex)"`로 새로 생성한다.
   - 이 환경에는 Unity가 없다. 컴파일은 CI가 확인하므로, 커밋 전에 using/네임스페이스/타입 이름/메서드 시그니처를 직접 grep으로 대조한다.
   - 서드파티 타일셋(`Assets/2D Platformer Tileset/`)은 저장소에 없으므로 그 파일에 의존하는 코드를 새로 만들지 않는다.
4. **검증**: `adversarial-reviewer` 에이전트에 목표, diff, 근거를 넘겨 판정을 받는다 (`review-loop` 스킬 절차). `REQUEST_CHANGES`면 고치고 다시 받는다. 최대 3회.
5. **문서**: 동작이 바뀌었으면 `README.md`와 `Docs/GameDesignDocument.md`의 해당 부분을 갱신한다.
6. **커밋**: CLAUDE.md 규칙대로 폴더 단위로 나눠 커밋한다. `BACKLOG.md`에서 항목을 `DONE`으로 바꾸고 브랜치 이름을 적는다.

## 3. PR

1. `git push -u origin <브랜치>`.
2. 열린 PR이 없으면 develop 대상으로 PR을 만든다. 본문: 이번에 한 작업 목록, 바뀐 조작/수치, **플레이테스트 체크리스트** (Unity에서 무엇을 눌러 확인하면 되는지), 에디터 툴 재실행이 필요한지.
3. 설정의 머지 방식이 `auto`면 PR에 자동 머지(merge commit 방식, `--no-ff`와 같음)를 켠다: `gh api -X PUT repos/Portuandude/I_Blame_You/pulls/<번호>/ccr/auto_merge -f merge_method=merge`. `manual`이면 켜지 않는다.

## 4. 기록

`Docs/AutoDev/LOG.md` 맨 위에 이번 실행을 추가하고 커밋, 푸시한다:

```
## YYYY-MM-DD HH:MM UTC
- 브랜치/PR: ...
- 완료: ...
- 실패/보류: ...
- 다음 실행 메모: ...
```
