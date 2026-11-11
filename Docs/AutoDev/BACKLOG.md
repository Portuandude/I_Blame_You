# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다. 끝난 항목(`DONE`)은 [BACKLOG_ARCHIVE.md](BACKLOG_ARCHIVE.md)로 옮겨 이 파일을 짧게 유지한다 (실행마다 읽는 토큰 절약).

## 최우선 (2026-11-10 사람 결정 4건)

- TODO (사람 필요: 15분 플레이테스트 뒤 판단): 숨 고르기 방 채우기 — 체크포인트가 빠진 휴식(Rest) 방이 빈 평지가 됐다. 회복 없이 볼거리만(반짝이는 버섯·나비 장식, 다음 방 미리보기 표지판 등) 두거나, 방 리듬 완충을 이동 방으로 바꿀지 플레이테스트 뒤 판단. 회복 구슬을 두면 회복 수지가 다시 커지므로 넣지 않는다

## 다음 (2026-11-11 자동 실행이 추가)

- TODO: 무너지는 발판 소리 — 무너질 때 나무 삐걱·툭 효과음, 다시 생길 때 작은 톡 (`Tools/AudioGen` crumble/crumble_back, `CrumblingPlatform.Crumble`, 플레이어가 가까울 때만 — `FlameJetTrap.PlayerNear` 참고)
- TODO: 회복 숫자 통일 — 회복 구슬(`ArenaReward`)·흡혈 송곳니 회복도 실제 회복량 초록 숫자 "+N" (가득 차면 생략). `ChunkStreamer.ShowHealNumber`를 `DamageNumbers.ShowHeal(Vector3, float)` 같은 공용 함수로 옮겨 같이 쓴다
- TODO: 체력 바 회복 반짝 — HUD 체력 캡슐 바가 차오를 때 늘어난 부분이 연두색으로 0.4초 빛났다 사라지게 (`UI/CapsuleBar`의 trail 반대 방향, 피해 trail은 그대로)

## 우선순위 (2026-11-09 사람 지시): 비주얼 먼저

아트 컨셉은 **귀엽지만 화려하게** (둥글고 통통한 실루엣, 큰 눈, 채도 높은 색, 빛나는 테두리·반짝임·잔상). 그림은 `Tools/ArtGen/generate_sprites.py`(절차 생성)와 `.claude/skills/pixelfx`로 만들고, 기존 파일 이름·프레임 수·피벗(발 위치)을 지켜 교체해 코드 변경을 최소화한다. 사람이 직접 그린 그림(마법사 Idle, 슬라임 원본)도 새 그림으로 **덮어써도 된다** (2026-11-09 사람 지시, 원본은 git 기록에 남는다).

