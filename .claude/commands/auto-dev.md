---
description: 자동 개발 1회 실행. 백로그에서 작업을 골라 구현하고 develop 대상 PR을 올린다.
---

# /auto-dev — 무인 자동 개발 1회분

이 명령은 5시간마다 예약 실행되는 무인 세션이 쓴다. 사람에게 질문하지 말고 끝까지 진행한다.
저장소 루트의 `CLAUDE.md` 규칙이 이 문서보다 우선한다 (삭제 금지 → Trash, 폴더 단위 커밋, Git-flow, `--no-ff`).

설정값은 `Docs/AutoDev/README.md`의 "설정" 표를 읽어서 쓴다 (실행당 작업 수, 머지 방식).

## 0. 준비

1. `git fetch origin` 후 `Docs/AutoDev/HANDOFF.md`, `Docs/AutoDev/README.md`, `Docs/AutoDev/BACKLOG.md`, `Docs/AutoDev/CODEMAP.md`, `Docs/AutoDev/LOG.md`의 **맨 위 기록 하나**만 읽는다 (`CLAUDE.md`는 세션이 자동으로 읽는다).
   `HANDOFF.md`가 "없음"이 아니면 거기 적힌 "바로 다음 할 일"부터 한다 (`handoff` 스킬의 받는 절차).
2. `master` 브랜치는 절대 건드리지 않는다. release/hotfix 브랜치도 만들지 않는다.

## 토큰 절약 규칙 (2026-11-09 사람 지시)

- **탐색 범위 제한**: 저장소를 넓게 훑지 않는다. `CODEMAP.md`에서 작업에 해당하는 줄의 파일로 바로 가고, 모르는 것은 `grep -rn`으로 찾는다.
  `README.md`·`Docs/GameDesignDocument.md`는 통째로 읽지 않고, 고칠 부분만 `grep -n`으로 찾아 그 근처만 읽는다. 300줄 넘는 파일도 같은 방식.
  `Trash/`, 생성된 PNG, `.meta`/`.prefab`/`.unity`, `*_ARCHIVE.md`는 필요할 때만 (CODEMAP "읽지 않는 곳").
- **리뷰 에이전트**: 작업당 적대적 리뷰 1회 + 수정 확인 1회가 기본. 리뷰어에게는 diff 범위(`git diff A..B`)와 확인할 점만 넘기고 "diff에 나온 파일과 직접 호출하는 곳만 읽어라"라고 적는다.
  문서·그림만 바뀐 작업은 리뷰를 건너뛴다. 수정 확인은 새 에이전트 대신 같은 리뷰어에게 메시지로 이어서 맡긴다.
- **반복 작업은 스킬로**: 로컬 테스트 `editmode-tests`, 새 파일 .meta `unity-meta`, 긴 세션 넘기기 `handoff`, 검증 `review-loop`. 같은 손작업을 세 번째 하게 되면 `Tools/Dev/` 스크립트 + `.claude/skills/` 스킬로 만들고 이 목록에 더한다.
- **긴 세션은 넘긴다**: 컨텍스트 요약이 한 번 일어났거나 작업을 6개 넘게 끝냈으면 `handoff` 스킬대로 `HANDOFF.md`를 쓰고 실행을 끝낸다.
- **파일을 짧게**: `BACKLOG.md`의 `DONE` 항목은 `BACKLOG_ARCHIVE.md` 맨 위로, `LOG.md`는 최근 3개만 두고 나머지는 `LOG_ARCHIVE.md` 맨 위로 옮긴다 (실행 끝에).

## 1. 이전 실행 정리 (새 작업보다 먼저)

`gh api repos/Portuandude/I_Blame_You/pulls?state=open` 으로 head가 `feature-auto-`로 시작하는 열린 PR을 찾는다.
세션 환경이 브랜치 이름을 `claude/...`로 정해 주는 경우도 있으므로, base가 `develop`이고 head가 `claude/`로 시작하며 제목이 `AutoDev`로 시작하는 열린 PR도 같은 방식으로 처리한다.
이번 세션에 개발 브랜치가 지정되어 있으면 새 `feature-auto-*` 대신 그 브랜치를 `origin/develop`에서 다시 만들어 쓰고, PR 제목은 `AutoDev: `로 시작한다.

- CI(`Unity CI` 워크플로)가 **성공**했고 설정의 머지 방식이 `auto`면: `gh api -X PUT repos/Portuandude/I_Blame_You/pulls/<번호>/merge -f merge_method=merge`로 머지한다 (`--no-ff`와 같은 merge commit). 그 뒤 develop에서 새 브랜치로 진행한다.
- CI(`Unity CI` 워크플로)가 **실패**한 PR이 있으면: 그 브랜치를 체크아웃해 실패 로그(`gh api .../actions/runs`, job 로그)를 읽고 최소 수정으로 고친 뒤 푸시한다. 이번 실행의 첫 작업으로 센다.
- CI가 **진행 중**인 PR이 있으면: 그 브랜치 위에서 이어서 작업한다 (같은 PR에 커밋이 쌓인다).
- 열린 PR이 없으면: `origin/develop`에서 `feature-auto-YYYYMMDD-HHMM` (UTC) 브랜치를 새로 만든다.
- 같은 실패를 두 번 고쳤는데도 CI가 계속 실패하면, 그 PR은 그대로 두고 `LOG.md`에 "사람 확인 필요"로 적은 뒤 develop에서 새 브랜치로 진행한다.

## 2. 작업 반복

설정의 "실행 방식"이 `limit`이면 **사용량 한도에 걸려 세션이 멈출 때까지** 아래를 계속 반복한다 (세션은 남은 사용량을 읽을 수 없으므로 한도 자체가 정지 신호다). 그래서:
- 작업 하나가 끝날 때마다 바로 푸시하고 PR(없으면 생성)에 반영한다. 언제 끊겨도 잃는 것은 진행 중이던 작업 하나뿐이어야 한다.
- 작업 사이마다 `LOG.md`의 이번 실행 항목을 갱신해 푸시한다 (끊겨도 기록이 남도록).
- 안전 상한: 작업 20개 또는 실행 시작 후 4시간 30분이 지나면 멈춘다 (다음 실행과 겹치지 않게).
`count`면 "실행당 작업 수"만큼만 반복한다.
백로그가 비면 GDD의 "구현 로드맵"과 코드 상태를 보고 다음 작업 3~5개를 `BACKLOG.md`에 먼저 추가한 뒤 진행한다.

1. **고르기**: `BACKLOG.md`의 `TODO` 중 맨 위 항목. `BLOCKED`나 "사람 필요"가 붙은 항목은 건너뛴다.
2. **계획**: 파일 3개 이상을 건드리는 작업이면 `planner` 에이전트로 계획을 받는다. 작으면 바로 구현.
3. **구현**: 기존 구조를 따른다 (스포너 코드가 단일 출처, 프리팹은 직접 수정하지 않음, 에디터 툴은 `Scripts/Editor`).
   - 새 `.cs` 파일이나 폴더를 `Assets/` 아래 만들면 반드시 `.meta` 파일도 만든다: `python3 Tools/Dev/unity_meta.py add <경로>` (`unity-meta` 스킬). 커밋 전 `check`.
   - 스테이지 생성·공식·유물 코드를 고쳤으면 `bash Tools/Dev/run_editmode_tests.sh`로 로컬 테스트 (`editmode-tests` 스킬).
   - 이 환경에는 Unity가 없다. 컴파일은 CI가 확인하므로, 커밋 전에 using/네임스페이스/타입 이름/메서드 시그니처를 직접 grep으로 대조한다.
   - 서드파티 타일셋(`Assets/2D Platformer Tileset/`)은 저장소에 없으므로 그 파일에 의존하는 코드를 새로 만들지 않는다.
4. **검증**: `adversarial-reviewer` 에이전트에 목표, diff, 근거를 넘겨 판정을 받는다 (`review-loop` 스킬 절차). `REQUEST_CHANGES`면 고치고 다시 받는다. 최대 3회.
5. **문서**: 동작이 바뀌었으면 `README.md`와 `Docs/GameDesignDocument.md`의 해당 부분을 갱신한다.
6. **커밋**: CLAUDE.md 규칙대로 폴더 단위로 나눠 커밋한다. `BACKLOG.md`에서 항목을 `DONE`으로 바꾸고 브랜치 이름을 적는다.

## 3. PR

1. `git push -u origin <브랜치>`.
2. 열린 PR이 없으면 develop 대상으로 PR을 만든다. 본문: 이번에 한 작업 목록, 바뀐 조작/수치, **플레이테스트 체크리스트** (Unity에서 무엇을 눌러 확인하면 되는지), 에디터 툴 재실행이 필요한지.
3. 설정의 머지 방식이 `auto`면 먼저 GitHub 자동 머지를 켠다: `gh api -X PUT repos/Portuandude/I_Blame_You/pulls/<번호>/ccr/auto_merge -f merge_method=merge` (develop 브랜치 규칙이 `editmode` 통과를 요구하므로, 세션이 먼저 끝나도 CI 통과 시 GitHub가 머지한다). 실패해도 아래 직접 머지로 진행한다.
   그다음 이번 푸시의 CI가 끝날 때까지 기다린다 (백그라운드로 `gh api .../actions/runs?head_sha=<sha>`의 status를 확인, 보통 10~20분).
   - `editmode` 잡이 **success**면 `gh api -X PUT repos/Portuandude/I_Blame_You/pulls/<번호>/merge -f merge_method=merge`로 직접 머지한다.
   - **skipped**(라이선스 없음)나 **failure**면 머지하지 않는다. 실패면 원인을 고쳐 다시 푸시하고 한 번 더 기다린다. 그래도 실패면 다음 실행에서 1단계가 처리한다.
   - `manual`이면 머지하지 않는다.

## 4. 기록

`Docs/AutoDev/LOG.md` 맨 위에 이번 실행을 추가하고, 끝난 백로그·오래된 기록을 보관 파일로 옮긴 뒤 커밋, 푸시한다:

```
## YYYY-MM-DD HH:MM UTC
- 브랜치/PR: ...
- 완료: ...
- 실패/보류: ...
- 다음 실행 메모: ...
```
