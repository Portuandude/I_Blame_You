# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다. 끝난 항목(`DONE`)은 [BACKLOG_ARCHIVE.md](BACKLOG_ARCHIVE.md)로 옮겨 이 파일을 짧게 유지한다 (실행마다 읽는 토큰 절약).

## 최우선: 플레이테스트 결과 (2026-11-12 사람 피드백)

사람이 [PLAYTEST_AND_PROGRESS.md](../PLAYTEST_AND_PROGRESS.md) 체크리스트를 직접 돌리고 남긴 의견이다 (원문은 그 문서 "6. 플레이테스트 결과"). 위에서부터 처리한다. 큰 항목은 한 실행 안에서 여러 작업으로 나눠도 된다. (남은 항목 없음 — 아래 "다음"부터)

## 다음: 몬스터 그림·골드·장비·마을 (2026-11-12 사람 지시)

위 플레이테스트 항목 다음에 위에서부터 처리한다. 장비·마을은 크기가 커서 **설계 문서를 먼저** 올리고 나서 구현한다.
사람 확정(2026-11-12): 중간보스 골드 50~100, 골드는 그 판 안에서만, 장비는 A/B 공용 한 벌, 장비는 유물 보상과 함께도 나오고 상점에서도 산다, **진행도 시스템 삭제**, 마을은 **스테이지당 1개**를 방을 돌아다니며 찾는다.

## 다음 (2026-11-13 10:45 자동 실행이 추가 — 낭떠러지 추락·복귀 연출)

DIRECTION ① "플레이 감각 다듬기". 낭떠러지에 떨어졌다 돌아오는 순간이 갑자기 순간이동한 것처럼 느껴지지 않게. 잃는 체력·복귀 자리·무적 1초 그대로 (그림·소리만, `Systems/Effects/FallRecoveryFx`).

- DONE (claude/project-thread-bynbiy): 추락 — 바닥 아래로 떨어지기 시작하면 새 효과음 `PitFall`("휘이이잉") 한 번
- DONE (claude/project-thread-bynbiy): 복귀 화면 — 순간 어두워졌다 밝아짐 (방 전환과 같은 `ScreenFlash.RoomFade`)
- DONE (claude/project-thread-bynbiy): 복귀 자리 — aqua 빛 고리 + 반짝 조각 + 발밑 먼지 + 새 효과음 `PitReturn`("뿅")
- DONE (claude/project-thread-bynbiy): 무적 깜빡임 — 복귀 뒤 무적 1초 동안 몸 그림이 깜빡임
- DONE (claude/project-thread-bynbiy): 모래시계 — 피해 없이 돌아오면 금빛 고리 + 머리 위에서 흘러내리는 금빛 모래 + 조금 높은 음

## 다음 (2026-11-13 10:35 자동 실행이 추가 — 가시·불기둥 함정 맞음 연출)

DIRECTION ① "플레이 감각 다듬기". 함정 예고(함정 읽기)는 끝났으니 맞는 순간이 손에 느껴지게. 피해·튕김·쿨다운·불 시간 그대로 (그림·소리만, `Systems/Effects/TrapHitFx`).

- DONE (claude/project-thread-bynbiy): 가시 밟음 — 밟은 자리에서 흰·붉은 쇳조각 + 새 효과음 `SpikeStab`("찡")
- DONE (claude/project-thread-bynbiy): 가시 튕김 — 튕겨 오르는 발밑 작은 먼지
- DONE (claude/project-thread-bynbiy): 불기둥 닿음 — 몸에서 주황·노랑 불똥 + 새 효과음 `FlameSizzle`("치익", 피해 틱마다)
- DONE (claude/project-thread-bynbiy): 불 솟음 — 분출구에서 불똥이 위로 터짐 (화면 안만)
- DONE (claude/project-thread-bynbiy): 불 꺼짐 — 분출구에서 남은 불씨 몇 개

## 다음 (2026-11-13 10:30 자동 실행이 추가 — 고블린 근접 휘두르기·함정 화살 연출)

DIRECTION ① "플레이 감각 다듬기". 고블린이 휘두르는 순간과 맞았는지·빗나갔는지가 손에 느껴지게. 판정 크기·시간·피해 그대로 (그림·소리만, `Enemies/GoblinSwingFx`).

- DONE (claude/project-thread-bynbiy): 휘두르기 소리 — 새 효과음 `GoblinSwing`("붕", 화면 안·0.08초 간격), 대전사는 낮은 음
- DONE (claude/project-thread-bynbiy): 휘두르기 궤적 — 위에서 앞으로 그어지는 흰 초승달(조각 6개가 차례로 나타났다 옅어짐), 대전사는 크고 따뜻한 색
- DONE (claude/project-thread-bynbiy): 맞힘 — 맞힌 자리에 노란 불꽃 조각, 대전사는 약한 화면 흔들림
- DONE (claude/project-thread-bynbiy): 헛스윙 — 무기 끝 아래 땅에 작은 먼지, 대전사는 흙 조각도
- DONE (claude/project-thread-bynbiy): 함정 화살 — 화살 발사대 화살도 궁수 화살 연출(꼬리·스침 "슈웅"·벽에 박힘 "턱"·나무 조각) + 발사 "탁"(BowTwang을 높고 작게)

## 다음 (2026-11-13 10:40 자동 실행이 추가 — 슬라임 도약·착지 연출)

DIRECTION ① "플레이 감각 다듬기". 슬라임 도약 덮치기가 뛰고 떨어지는 순간이 말랑하게 느껴지게. 도약 시간·속도·피해 그대로 (그림·소리만, `Enemies/SlimeLandFx`).

- DONE (claude/project-thread-c3791f): 도약 — 뛰어오르는 순간 발밑 작은 먼지 + 새 효과음 `SlimeHop`("뾱")
- DONE (claude/project-thread-c3791f): 실제 착지 감지 — 떨어지던 몸이 멈춘 순간 한 번 (꼭대기·천장은 아님)
- DONE (claude/project-thread-c3791f): 착지 — 납작하게 눌림 + 먼지 + 작은 방울 + 새 효과음 `SlimePlop`("퐁")
- DONE (claude/project-thread-c3791f): 정예·기사 착지 — 낮은 음 + 작은 충격파 + 보라 방울 6개 + 약한 화면 흔들림
- DONE (claude/project-thread-c3791f): 소리 겹침 — 화면 안일 때만, 여러 마리라도 0.08초에 한 번

## 다음 (2026-11-13 10:25 자동 실행이 추가 — 고블린 궁수 화살 연출)

DIRECTION ① "플레이 감각 다듬기". 궁수 화살이 날아가고·스치고·박히고·맞는 순간이 손에 느껴지게. 속도·사거리·피해·간격 그대로 (그림·소리만, `Enemies/GoblinArrowFx`).

- DONE (claude/project-thread-c3791f): 시위 놓기 — 화면 안이면 새 효과음 `BowTwang`("팅")
- DONE (claude/project-thread-c3791f): 화살 꼬리 — 날아가는 화살 뒤 옅은 흰 꼬리 (0.12초)
- DONE (claude/project-thread-c3791f): 스침 — 노린 플레이어 곁 1.3칸 안을 맞히지 않고 지나가면 새 효과음 `ArrowWhiz`("슈웅") 한 번
- DONE (claude/project-thread-c3791f): 벽에 박힘 — 닿은 자리에 같은 그림으로 박혀 0.35초 부르르, 1.5초 뒤 옅어짐 + 새 효과음 `ArrowThunk`("턱") + 작은 먼지
- DONE (claude/project-thread-c3791f): 맞음 — 플레이어에게 맞으면 나무 조각 4개가 튐

## 최우선 (2026-11-10 사람 결정 4건)

- TODO (사람 필요: 15분 플레이테스트 뒤 판단): 숨 고르기 방 채우기 — 체크포인트가 빠진 휴식(Rest) 방이 빈 평지가 됐다. 회복 없이 볼거리만(반짝이는 버섯·나비 장식, 다음 방 미리보기 표지판 등) 두거나, 방 리듬 완충을 이동 방으로 바꿀지 플레이테스트 뒤 판단. 회복 구슬을 두면 회복 수지가 다시 커지므로 넣지 않는다

## 다음 (2026-11-12 23:40 자동 실행이 추가 — 위험한 길 보상 넓히기·종유석 마무리)

23:20 실행 인수인계 후보. 동전 무더기는 난수를 쓰지 않고 놓아 기존 시드 지형을 바꾸지 않는다.

(동전 무더기 N/M은 2026-11-13 결과 창 탐험 줄로 해결 → BACKLOG_ARCHIVE)

## 다음 (2026-11-12 22:50 자동 실행이 추가 — 레벨 재설계 2차: 세로 방 다양화)

DIRECTION ① "플레이 감각 다듬기" 안의 세로 방 다양화. ContentIdeas 후보와 겹치지 않는 지형 변형이다.

## 다음 (2026-11-12 23:10 자동 실행이 추가 — 레벨 재설계 2차: 가로 지형 늘리기)

DIRECTION ① "방 종류 늘리기" 중 사람 선택이 필요 없는 지형 변형만 (ContentIdeas 퍼즐·이벤트·적 배치는 사람이 고른 뒤). 6개 모두 끝 → BACKLOG_ARCHIVE.


## 우선순위 (2026-11-09 사람 지시): 비주얼 먼저

아트 컨셉은 **귀엽지만 화려하게** (둥글고 통통한 실루엣, 큰 눈, 채도 높은 색, 빛나는 테두리·반짝임·잔상). 그림은 `Tools/ArtGen/generate_sprites.py`(절차 생성)와 `.claude/skills/pixelfx`로 만들고, 기존 파일 이름·프레임 수·피벗(발 위치)을 지켜 교체해 코드 변경을 최소화한다. 사람이 직접 그린 그림(마법사 Idle, 슬라임 원본)도 새 그림으로 **덮어써도 된다** (2026-11-09 사람 지시, 원본은 git 기록에 남는다).

