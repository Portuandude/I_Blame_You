# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다. 끝난 항목(`DONE`)은 [BACKLOG_ARCHIVE.md](BACKLOG_ARCHIVE.md)로 옮겨 이 파일을 짧게 유지한다 (실행마다 읽는 토큰 절약).

## 우선순위 (2026-11-09 사람 지시): 비주얼 먼저

아트 컨셉은 **귀엽지만 화려하게** (둥글고 통통한 실루엣, 큰 눈, 채도 높은 색, 빛나는 테두리·반짝임·잔상). 그림은 `Tools/ArtGen/generate_sprites.py`(절차 생성)와 `.claude/skills/pixelfx`로 만들고, 기존 파일 이름·프레임 수·피벗(발 위치)을 지켜 교체해 코드 변경을 최소화한다. 사람이 직접 그린 그림(마법사 Idle, 슬라임 원본)도 새 그림으로 **덮어써도 된다** (2026-11-09 사람 지시, 원본은 git 기록에 남는다).

- DONE (claude/project-thread-8rxr8x): HUD 체력·마나 바 귀엽게 — `UI/SkillHUD.Bars.cs` 하트·별 아이콘 + 둥근 캡슐(9분할) + 빛 테두리 + 흰 잔상(0.45초 머문 뒤 줄어듦), 체력 낮으면 하트 두근. 그림 `Tools/ArtGen/generate_hud.py` → `Resources/UI/HUD`
- DONE (claude/project-thread-8rxr8x): 보스 체력바 귀엽게 — 왕관 아이콘(`generate_hud.py` Crown) + 캡슐 바(보라, gold 빛) + 흰 잔상, 한 번에 5% 넘게 깎이면 0.25초 흔들림. 보스 교체·분열로 최대 체력이 바뀌면 잔상 초기화
- DONE (claude/project-thread-8rxr8x): 레벨·스테이지 진행 바 귀엽게 — 캡슐 바(경험치 lilac, 진행도 gold), 레벨업 0.6초 gold 반짝, 진행 바 끝 깃발(`generate_hud.py` Flag)
- DONE (claude/project-thread-8rxr8x): 머리 위 바·적 체력바 귀엽게 — `UI/CapsuleBar.cs`(런타임 9분할 Sliced 스프라이트 + 흰 잔상, 잔상 계산 `BarTrail` 공용)로 `StatusBarsUI`·`EnemyHealthBar` 교체. 그림 없으면 예전 단색 사각형
- DONE (claude/project-thread-8rxr8x): HUD 패널 귀엽게 — `panel`·`keyBox` 스타일 배경을 둥근 모서리 + night 반투명 + 크림 테두리 9분할(`generate_hud.py` Panel)로. 피드는 패널이 없어 그대로
- TODO: 적 경직 연출 — GDD 로드맵 "경직 애니메이션": 경직 동안 머리 위에 별 3개가 빙글빙글 도는 어질어질 이펙트(생성기, 반복 프레임). 적마다 피격 그림을 새로 그리는 대신 모든 적에 공통으로 (그림 교체는 슬라임 회색판·기사 투구판과 겹쳐 복잡)

- BLOCKED (사람 결정: 회복만 / 특성 포인트 +1 / 다음 구간 지도 공개 등): 중간보스 처치 보상 — 지금은 정예와 같은 회복 구슬 + 유물 상자. `Docs/MidBossDraft.md` "사람이 정할 것"

- BLOCKED (사람 결정: 달리기·대쉬로 건너뛰기를 허용할지): 낭떠러지 건너뛰기 정리 — 검증기 점프 거리는 걷기 기준(최대 4~5칸)인데 실제 달리기 점프는 약 7.3칸, 걷기 점프+공중 대쉬 약 8.1칸, 달리기 점프+대쉬 약 11칸. 그래서 단일 움직이는 발판(7~8칸)·낭떠러지 일부는 발판 없이 건널 수 있다. 막으려면 폭을 늘리거나 검증기를 달리기 기준으로, 허용하려면 문서에 명시
