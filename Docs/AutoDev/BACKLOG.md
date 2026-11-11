# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다. 끝난 항목(`DONE`)은 [BACKLOG_ARCHIVE.md](BACKLOG_ARCHIVE.md)로 옮겨 이 파일을 짧게 유지한다 (실행마다 읽는 토큰 절약).

## 최우선 (2026-11-10 사람 결정 4건)

- TODO (사람 필요: 15분 플레이테스트 뒤 판단): 숨 고르기 방 채우기 — 체크포인트가 빠진 휴식(Rest) 방이 빈 평지가 됐다. 회복 없이 볼거리만(반짝이는 버섯·나비 장식, 다음 방 미리보기 표지판 등) 두거나, 방 리듬 완충을 이동 방으로 바꿀지 플레이테스트 뒤 판단. 회복 구슬을 두면 회복 수지가 다시 커지므로 넣지 않는다

## 다음 (2026-11-11 자동 실행이 추가)

- DONE (claude/project-thread-qysenh): 꿀단지 연출 — 꿀단지가 있을 때 보상 구슬을 주우면 머리 위로 작은 꿀단지가 톡 떠올랐다 사라지게 (`VfxPlayer.AcornGlint`처럼 `MedalGlintFx.Create(..., "HoneyJar")`), 알림에 "· 꿀단지 +15%" 붙이기
- DONE (claude/project-thread-qysenh): 흡혈 하트 소리 — 붉은 하트가 닿을 때 작고 말랑한 "뽁" (`Tools/AudioGen` vampire_heart, StarShard처럼 작게, `PlayerRelics.OnVampireHeartArrived`)
- DONE (claude/project-thread-qysenh): 무너지는 발판 위 적 — 적(슬라임·고블린)이 무너지는 발판에 올라서도 무너지게 할지 확인하고, 무너지면 적도 떨어지게 (`CrumblingPlatform.OnCollisionStay2D`가 지금은 플레이어만 봄). 낙사 판정이 적에게 문제없는지 확인
- TODO: 적 낙사 연출 — 무너지는 발판이나 낭떠러지로 떨어진 적이 낙사선(`ChunkStreamer` KillLine) 아래에서 조용히 사라진다. 사라질 때 낙사선 위 화면 안쪽(카메라 아래 가장자리)에 작은 먼지 펑 + 작은 `EnemyDie` 소리를 내서 "떨어져 처치됨"이 보이게 (경험치는 지금처럼 주지 않는다)
- TODO: 유물 젤리 방석 — 무너지는 발판이 흔들리는 시간 0.55초 → 0.9초 (`CrumblingPlatform.ShakeTime`, 플레이어가 밟았을 때만). `RelicId` 맨 끝에 추가, 아이콘(`Tools/ArtGen/generate_relic_icons.py`), 테스트, 유물 23종
- TODO: 흡혈 하트·별똥 조각 겹침 — 한 적에서 하트와 금별이 같이 나오면 시작 위치가 겹친다. 별은 0.25유닛 옆에서 출발하게 (`PlayerRelics.OnEnemyKilled`)

## 우선순위 (2026-11-09 사람 지시): 비주얼 먼저

아트 컨셉은 **귀엽지만 화려하게** (둥글고 통통한 실루엣, 큰 눈, 채도 높은 색, 빛나는 테두리·반짝임·잔상). 그림은 `Tools/ArtGen/generate_sprites.py`(절차 생성)와 `.claude/skills/pixelfx`로 만들고, 기존 파일 이름·프레임 수·피벗(발 위치)을 지켜 교체해 코드 변경을 최소화한다. 사람이 직접 그린 그림(마법사 Idle, 슬라임 원본)도 새 그림으로 **덮어써도 된다** (2026-11-09 사람 지시, 원본은 git 기록에 남는다).

