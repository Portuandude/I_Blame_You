# 자동 개발 백로그

맨 위 `TODO`부터 처리한다. 상태: `TODO` / `DOING` / `DONE (브랜치)` / `BLOCKED (이유)`.
사람이 순서를 바꾸거나 항목을 추가해도 된다.

- TODO: 백로그 재점검 — develop의 현재 코드와 README/GDD를 대조해, 아래 항목 중 이미 구현된 것은 `DONE (기존)`으로 바꾸고 GDD "구현 로드맵"에서 빠진 항목을 추가
- TODO: HP 화면 경고등 — 체력 30% 이하일 때 화면 가장자리가 붉게 맥동 (GDD UI/UX)
- TODO: 상태이상 시스템 기반 — `Systems/StatusEffects`에 상태이상 정의(지속시간, 틱 효과)와 적용/해제 API, 머리 위 상태이상 아이콘 바
- TODO: 일시정지 메뉴 — Esc로 일시정지, 계속하기/다시 시작/시작 화면 (입력 잠금과 Time.timeScale 처리)
- TODO: 플레이어 사망 연출 — 사망 애니메이션 대체 연출(페이드, 슬로모션)과 게임오버 화면 정리
- TODO: 정예몹 1종 — 기존 적 구조(EnemyController + Spawner 폴더 형태)를 따라 패턴 2개를 가진 정예몹 추가
- TODO: 방 보상 — 방 클리어 시 체력/마나 회복 오브젝트 드롭
- TODO: 저장/불러오기 기초 — 설정값(개발자 모드, 볼륨)과 최고 기록을 PlayerPrefs 또는 JSON으로 저장
- TODO: EditMode 테스트 기반 — 테스트 가능한 순수 로직(데미지 공식, 스탯 표)에 대한 asmdef 분리 검토와 첫 테스트
