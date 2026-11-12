# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다. 끝난 항목(`DONE`)은 [BACKLOG_ARCHIVE.md](BACKLOG_ARCHIVE.md)로 옮겨 이 파일을 짧게 유지한다 (실행마다 읽는 토큰 절약).

## 최우선 (2026-11-10 사람 결정 4건)

- TODO (사람 필요: 15분 플레이테스트 뒤 판단): 숨 고르기 방 채우기 — 체크포인트가 빠진 휴식(Rest) 방이 빈 평지가 됐다. 회복 없이 볼거리만(반짝이는 버섯·나비 장식, 다음 방 미리보기 표지판 등) 두거나, 방 리듬 완충을 이동 방으로 바꿀지 플레이테스트 뒤 판단. 회복 구슬을 두면 회복 수지가 다시 커지므로 넣지 않는다

## 다음 (2026-11-11 22:40 자동 실행이 추가)

- DONE (claude/project-thread-wp6c3h): 별사탕 연출 — 별사탕으로 마나가 찰 때 머리 위로 작은 별사탕이 톡 (`MedalGlintFx.Create(..., "StarCandy")`, 도토리 주머니와 겹치면 옆으로 비켜서)
- DONE (claude/project-thread-wp6c3h): 젤리 방석 무너짐 방울 — 젤리 방석으로 버틴 발판이 무너질 때 나무 먼지 대신(또는 함께) 분홍 젤리 방울이 톡톡 튀게 (`CrumblingPlatform.Crumble` 끝)
- DONE (claude/project-thread-wp6c3h): 보상 구슬 자석 마무리 — 끌려오기 시작할 때 작은 "슝" 소리, 벽 너머 플레이어에게는 끌려가지 않게 (`ArenaReward.FindMagnetTarget`, 한쪽 발판은 막지 않게 주의)

## 다음 (2026-11-12 자동 실행이 추가)

- TODO: 유물 말굽 자석 — 보상 구슬 자석 반경 1.5 → 4유닛 (`RelicId.HorseshoeMagnet`=24, 공용, `ArenaReward.MagnetRadius`를 유물에 따라, 아이콘 `generate_relic_icons.py`·테스트, 유물 25종)
- TODO: 자석 꼬리 — 끌려오는 보상 구슬 뒤로 분홍 잔상이 짧게 남게 (`Afterimage` 재사용 가능 여부 확인, 끌려올 때만)
- TODO: 말굽 자석 연출 — 말굽 자석으로 먼 구슬이 끌려오기 시작할 때 머리 위로 작은 말굽 자석 아이콘이 톡 (`MedalGlintFx.Create(..., "HorseshoeMagnet")`, 1.5유닛 밖에서 잡혔을 때만)

## 우선순위 (2026-11-09 사람 지시): 비주얼 먼저

아트 컨셉은 **귀엽지만 화려하게** (둥글고 통통한 실루엣, 큰 눈, 채도 높은 색, 빛나는 테두리·반짝임·잔상). 그림은 `Tools/ArtGen/generate_sprites.py`(절차 생성)와 `.claude/skills/pixelfx`로 만들고, 기존 파일 이름·프레임 수·피벗(발 위치)을 지켜 교체해 코드 변경을 최소화한다. 사람이 직접 그린 그림(마법사 Idle, 슬라임 원본)도 새 그림으로 **덮어써도 된다** (2026-11-09 사람 지시, 원본은 git 기록에 남는다).

