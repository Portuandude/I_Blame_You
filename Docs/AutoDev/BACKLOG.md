# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다. 끝난 항목(`DONE`)은 [BACKLOG_ARCHIVE.md](BACKLOG_ARCHIVE.md)로 옮겨 이 파일을 짧게 유지한다 (실행마다 읽는 토큰 절약).

## 우선순위 (2026-11-09 사람 지시): 비주얼 먼저

아트 컨셉은 **귀엽지만 화려하게** (둥글고 통통한 실루엣, 큰 눈, 채도 높은 색, 빛나는 테두리·반짝임·잔상). 그림은 `Tools/ArtGen/generate_sprites.py`(절차 생성)와 `.claude/skills/pixelfx`로 만들고, 기존 파일 이름·프레임 수·피벗(발 위치)을 지켜 교체해 코드 변경을 최소화한다. 사람이 직접 그린 그림(마법사 Idle, 슬라임 원본)도 새 그림으로 **덮어써도 된다** (2026-11-09 사람 지시, 원본은 git 기록에 남는다).

- DONE (claude/project-thread-iz8mmy): 순간이동·방어막·평타 이펙트 다시 그리기 — `LevelArtConfig`의 옛 teleport/shield/slash 프레임 대신 `generate_magic_fx.py` 방식(색·빛 두 층)으로 귀엽고 화려하게, 런타임 `Resources/Effects/{Teleport,Shield,Slash}`에서 읽고 없으면 옛 프레임 (`EffectLibrary`)
- DONE (claude/project-thread-iz8mmy): 유물 상자 열 때 연출 — 뚜껑이 튀어 오르며 gold 빛기둥(레벨업 기둥 재사용) + 별 반짝임 터짐, 유물 이름 띠
- DONE (claude/project-thread-iz8mmy): 중간보스(슬라임 기사) 등장 연출 — 두 기사가 위에서 떨어져 착지할 때 충격파 고리 + 투구 반짝임, 화면 살짝 흔들림
- DONE (claude/project-thread-iz8mmy): 보스·중간보스 처치 연출 — 마지막 한 방에 0.35초 슬로모션(Time.timeScale 0.3, 실제 시간 기준으로 복구) + 화면 흰 번쩍임 + 큰 별·하트 폭발(`ClearBurst` 크게) + 보스 이름 띠 "격파!"(`RelicBannerView` 재사용). 일시정지·유물 창의 timeScale과 겹치지 않게
- DONE (claude/project-thread-iz8mmy): 슬라임 왕 등장 연출 — 보스 방에 들어가면 왕이 위에서 떨어져 쿵(`KnightEntrance` 방식 일반화) + 왕관 반짝임 + 이름 띠 "슬라임 왕"
- TODO: 회복 구슬 다시 그리기 — 지금 구슬을 통통한 하트 구슬(berry + cream 하이라이트 + 반짝임 반복)로, 주울 때 하트가 튀는 작은 터짐
- TODO: 물 기사 방패가 깨질 때 연출 — 방패 거품이 유리처럼 조각나 흩어지고(조각 6~8개 프레임) aqua 반짝임, 깨진 순간 기사가 잠깐 움찔(`EnemySquash`)
- TODO: HUD 체력·마나 바 귀엽게 — IMGUI 바를 둥근 캡슐 + 빛나는 테두리 + 체력 하트 아이콘·마나 별 아이콘 텍스처(생성기)로, 피해 받을 때 줄어드는 부분이 흰색으로 잠깐 남는 잔상

- BLOCKED (사람 결정: 회복만 / 특성 포인트 +1 / 다음 구간 지도 공개 등): 중간보스 처치 보상 — 지금은 정예와 같은 회복 구슬 + 유물 상자. `Docs/MidBossDraft.md` "사람이 정할 것"

- BLOCKED (사람 결정: 달리기·대쉬로 건너뛰기를 허용할지): 낭떠러지 건너뛰기 정리 — 검증기 점프 거리는 걷기 기준(최대 4~5칸)인데 실제 달리기 점프는 약 7.3칸, 걷기 점프+공중 대쉬 약 8.1칸, 달리기 점프+대쉬 약 11칸. 그래서 단일 움직이는 발판(7~8칸)·낭떠러지 일부는 발판 없이 건널 수 있다. 막으려면 폭을 늘리거나 검증기를 달리기 기준으로, 허용하려면 문서에 명시
