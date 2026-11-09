# 자동 개발 (AutoDev)

5시간마다 클라우드의 Claude Code 세션이 예약 실행되어, 이 저장소에서 작업을 골라 구현하고 develop 대상 PR을 올린다.
절차는 [`.claude/commands/auto-dev.md`](../../.claude/commands/auto-dev.md), 작업 목록은 [BACKLOG.md](BACKLOG.md), 실행 기록은 [LOG.md](LOG.md).

## 설정

| 항목 | 값 | 설명 |
| --- | --- | --- |
| 실행 주기 | 5시간 | claude.ai 예약 작업에서 변경 |
| 실행 방식 | limit | `limit`: 사용량 한도에 걸릴 때까지 계속 작업 (작업마다 푸시) / `count`: 아래 작업 수만큼만 |
| 실행당 작업 수 | 4 | 실행 방식이 `count`일 때만 사용 |
| 머지 방식 | auto | `auto`: Unity CI(`editmode`)가 성공하면 자동 실행 세션이 직접 develop에 머지 / `manual`: PR만 올림 |

## 사람이 하는 일

- 가끔 바탕화면 `I_BLAME_YOU` 폴더에서 develop을 pull 하고 Unity로 플레이해 본다. PR 본문의 플레이테스트 체크리스트를 참고.
- 방향을 바꾸고 싶으면 `BACKLOG.md` 순서를 바꾸거나 항목을 추가한다 (맨 위부터 처리).
- 배포(release-* → master)는 자동으로 하지 않는다.

## 검증 (Unity CI)

`.github/workflows/unity-ci.yml`이 PR마다 GameCI Docker 이미지로 프로젝트를 열어 컴파일과 EditMode 테스트를 돌린다.
저장소 Secrets에 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`가 있어야 동작하며, 없으면 Unity 단계는 건너뛰고 경고만 남긴다.

## Claude Forge

`.claude/agents`(planner, code-reviewer, adversarial-reviewer)와 `.claude/skills/review-loop`는
[sangrokjung/claude-forge](https://github.com/sangrokjung/claude-forge) (MIT, `.claude/FORGE_LICENSE.txt`)에서 가져와
이 저장소에 맞게 일부 줄을 뺐다 (홈 폴더 기록, 외부 스킬 참조).
