# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다.

- DONE (claude/project-thread-ulxu6c): 백로그 재점검 — develop의 현재 코드와 README/GDD를 대조해, 아래 항목 중 이미 구현된 것은 `DONE (기존)`으로 바꾸고 GDD "구현 로드맵"에서 빠진 항목을 추가
- DONE (claude/project-thread-ulxu6c): HP 화면 경고등 — 체력 30% 이하일 때 화면 가장자리가 붉게 맥동 (GDD UI/UX)
- DONE (기존): 상태이상 시스템 기반 — `Systems/StatusEffects`(화상/젖음/경직, `TryApply` API, 확률식)와 머리 위 `StatusIconBar`가 이미 있음. 플레이어도 화염 분출구로 화상을 받는다
- DONE (claude/project-thread-ulxu6c): 일시정지 메뉴 — Esc로 일시정지, 계속하기/다시 시작/시작 화면 (입력 잠금과 Time.timeScale 처리). 영창 중 Esc(취소), Tab 특성 창과 겹치지 않게
- DONE (claude/project-thread-ulxu6c): 플레이어 사망 연출 — 사망 애니메이션 대체 연출(페이드, 슬로모션)과 게임오버 화면 정리
- DONE (기존): 정예몹 1종 — `Enemies/Elite/MotherSlime`(분열), `Enemies/Elite/GoblinWarrior`(광폭화)가 이미 있음
- DONE (claude/project-thread-nsj3x2): 전투 구역 보상 — 방 구조가 청크식 스테이지로 바뀌었으므로, `ArenaZone` 클리어 시 체력/마나 회복 오브젝트 드롭 (기존 "방 보상" 대체)
- TODO: 저장/불러오기 기초 — 설정값(개발자 모드, 볼륨)과 최고 기록(클리어 시간, 최고 레벨)을 PlayerPrefs로 저장, 시작 화면에 최고 기록 표시
- TODO: EditMode 테스트 기반 — 테스트 가능한 순수 로직(데미지 공식, 스탯 표, 청크 생성기 검증)에 대한 asmdef 분리 검토와 첫 테스트. 현재 asmdef/테스트가 하나도 없어 CI `editmode`는 컴파일만 확인한다
- TODO: GDD 현행화 — `Docs/GameDesignDocument.md`가 방(Room) 구조·슬라임 1종 시점에 멈춰 있음. README 기준으로 청크식 스테이지, 2인 A/B 조작, 마나 스킬, 적/정예/보스, 특성 트리, 상태이상 현황을 반영
- TODO: 피격 이펙트 — GDD "경직 전용 애니메이션/피격 이펙트는 미구현". 피격 시 스프라이트 흰색 번쩍임(0.08초)과 작은 파편 이펙트
- TODO: 사운드 보강 — 점프/대쉬/피격/적 사망/레벨업 효과음을 `Tools/AudioGen`으로 합성해 `GameAudio`로 재생, 볼륨 설정 연동
- TODO: 맵 퍼즐 기초 — GDD의 컨트롤 퍼즐/스킬 퍼즐 중 하나(예: 속성 스킬로 여는 문)를 청크 1종으로 추가 (`ChunkGenerator` + `StageValidator` 규칙 지키기)
- BLOCKED (사람 필요: 동작별 그림): 마법사 Walk/Run/Jump/Dash/Attack 정식 프레임 교체
- TODO: 새 Input System 이전 검토 — 레거시 Input Manager 사용처 목록과 이전 계획만 문서로 (코드 변경은 사람 확인 후)
