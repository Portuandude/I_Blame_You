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
- DONE (claude/project-thread-nsj3x2): 저장/불러오기 기초 — 설정값(개발자 모드, 볼륨)과 최고 기록(클리어 시간, 최고 레벨)을 PlayerPrefs로 저장, 시작 화면에 최고 기록 표시
- DONE (claude/project-thread-nsj3x2): EditMode 테스트 기반 — 테스트 가능한 순수 로직(데미지 공식, 스탯 표, 청크 생성기 검증)에 대한 asmdef 분리 검토와 첫 테스트. 현재 asmdef/테스트가 하나도 없어 CI `editmode`는 컴파일만 확인한다
- DONE (claude/project-thread-nsj3x2): GDD 현행화 — `Docs/GameDesignDocument.md`가 방(Room) 구조·슬라임 1종 시점에 멈춰 있음. README 기준으로 청크식 스테이지, 2인 A/B 조작, 마나 스킬, 적/정예/보스, 특성 트리, 상태이상 현황을 반영
- DONE (claude/project-thread-nsj3x2): 피격 이펙트 — GDD "경직 전용 애니메이션/피격 이펙트는 미구현". 피격 시 스프라이트 흰색 번쩍임(0.08초)과 작은 파편 이펙트
- DONE (claude/project-thread-nsj3x2): 사운드 보강 — 점프/대쉬/피격/적 사망/레벨업 효과음을 `Tools/AudioGen`으로 합성해 `GameAudio`로 재생, 볼륨 설정 연동
- DONE (claude/project-thread-nsj3x2, 봉인문): 맵 퍼즐 기초 — GDD의 컨트롤 퍼즐/스킬 퍼즐 중 하나(예: 속성 스킬로 여는 문)를 청크 1종으로 추가 (`ChunkGenerator` + `StageValidator` 규칙 지키기)
- BLOCKED (사람 필요: 동작별 그림): 마법사 Walk/Run/Jump/Dash/Attack 정식 프레임 교체
- DONE (claude/project-thread-nsj3x2): 새 Input System 이전 검토 — 레거시 Input Manager 사용처 목록과 이전 계획만 문서로 (코드 변경은 사람 확인 후) → [InputSystemMigration.md](../InputSystemMigration.md)
- DONE (claude/project-thread-nsj3x2): 일시정지 메뉴 볼륨 조절 — Esc 메뉴에 배경음악/효과음 슬라이더 (시작 화면과 같은 SaveData 저장)
- DONE (claude/project-thread-nsj3x2): 런 결과 통계 — 처치 수, 도달 레벨, 받은 피해, 연 봉인문/돌파 구역 수를 게임오버·클리어 창에 표시
- DONE (claude/project-thread-nsj3x2): 컨트롤 퍼즐 1종 — 일정 주기로 움직이는 발판을 타고 건너는 청크 (`ChunkGenerator` + `StageValidator`는 발판이 없을 때도 막히지 않게, 낭떠러지 복귀 규칙 활용)
- DONE (claude/project-thread-nsj3x2): 봉인문 보강 — 땅 속성 가시를 문 앞에서 쓰면 안내, 문 근처에서는 유도 마법이 적보다 문을 우선하지 않도록 안내 문구 정리
- DONE (claude/project-thread-nsj3x2): 중간보스 기획 초안 — GDD에 스테이지 1 중간보스 후보 2~3개(패턴, 위치)를 문서로만 (구현은 사람 확인 후) → [MidBossDraft.md](../MidBossDraft.md). 후보 선택은 사람 필요
- DONE (claude/project-thread-nsj3x2): 경직 연출 — 경직 전용 애니메이션 대체로 피격 순간 몸 그림을 뒤로 젖혔다가 되돌림 (회전만, Animator·보스 찌그러짐과 겹치지 않게)
- DONE (claude/project-thread-nsj3x2): 두 속성 봉인문 — 진행도 60% 이후 봉인문 중 일부는 두 속성을 순서대로 맞혀야 열림 (문양 2개, 첫 속성을 맞히면 첫 문양이 꺼짐). 청크 데이터에 둘째 속성 추가, 테스트
- DONE (claude/project-thread-nsj3x2): 발판 퍼즐 변형 — 난이도 0.7 이상에서 발판 두 개를 갈아타는 넓은 낭떠러지 (가운데 작은 기둥, 발판 위상 엇갈림), 검증기·테스트
- DONE (claude/project-thread-nsj3x2): 스테이지 배치 규칙 테스트 — 정예 전투 40%/72%, 마지막 휴식+보스, 퍼즐 청크 진행도 범위, 예상 시간 30분 ±를 EditMode 테스트로 고정
- BLOCKED (사람 결정: 달리기·대쉬로 건너뛰기를 허용할지): 낭떠러지 건너뛰기 정리 — 검증기 점프 거리는 걷기 기준(최대 4~5칸)인데 실제 달리기 점프는 약 7.3칸, 걷기 점프+공중 대쉬 약 8.1칸, 달리기 점프+대쉬 약 11칸. 그래서 단일 움직이는 발판(7~8칸)·낭떠러지 일부는 발판 없이 건널 수 있다. 막으려면 폭을 늘리거나 검증기를 달리기 기준으로, 허용하려면 문서에 명시
- DONE (claude/project-thread-nsj3x2): 두 속성 봉인문 진행 기록 — 첫 속성을 맞힌 뒤 청크를 치웠다 다시 만들어도 맞힌 개수가 이어지게 (ChunkStreamer 기록)
- DONE (claude/project-thread-nsj3x2): 이동 청크 선택 안전장치 — `PickTravel`의 반올림 대비 기본값이 직전 종류(평지)와 겹칠 수 있던 문제 정리
- DONE (claude/project-thread-5ne2al): 유물(런 중 아이템) 기초 — 정예 전투 구역을 돌파하면 회복 구슬 옆에 유물 상자. 닿으면 게임이 멈추고 유물 3개 중 1개를 고른다 (A/B 공용, 한 런에 같은 유물은 한 번). 효과는 기존 수치 훅(최대 체력/마나, 방어, 이동속도, 주는 피해, 마나 재생, 치명타)만 쓴다. 고르는 후보는 시드로 결정, 순수 로직은 EditMode 테스트
- DONE (claude/project-thread-5ne2al): 유물 HUD·기록 — 가진 유물을 레벨 막대 아래에 표시, 게임오버·클리어 창 런 기록에 유물 수
- DONE (claude/project-thread-5ne2al): 런 진행 저장 기초 — 휴식 체크포인트를 켜면 시드·체크포인트 위치·레벨·특성·유물·런 기록을 PlayerPrefs에 저장, 시작 화면에 "이어하기". 쓰러지거나 클리어하면 지운다
- DONE (claude/project-thread-5ne2al): 스테이지 2 기획 초안 — 테마, 새 쫄몹 2~3종, 새 함정·퍼즐, 보스 후보를 문서로만 (구현은 사람 확인 후) → [Stage2Draft.md](../Stage2Draft.md). 테마 선택은 사람 필요
- DONE (claude/project-thread-5ne2al): 런 사이 성장 기획 초안 — 메타 진행(런이 끝나도 남는 해금/재화) 후보 2~3개를 문서로만 → [MetaProgressionDraft.md](../MetaProgressionDraft.md). 후보 선택은 사람 필요
- DONE (claude/project-thread-5ne2al): `PickTravel` 반올림 대비 경로 테스트 — 기본값이 직전 종류와 겹치지 않음을 테스트로 고정
- DONE (claude/project-thread-5ne2al): 특수 효과 유물 3종 — 깃털 부적(낙하 피해 15% → 5%), 흡혈 송곳니(처치 시 체력 +3), 집중 구슬(날린 마나 25% 환급, 신비 10과 합산)
- DONE (claude/project-thread-5ne2al): 일시정지 메뉴 런 요약 — Esc 메뉴에 이번 런의 특성 단계와 가진 유물 효과를 한눈에 (A/B가 서로 뭘 골랐는지 확인용)
- DONE (claude/project-thread-5ne2al): 결과 창 유물 목록 — 게임오버·클리어 창에 고른 유물 이름
- DONE (claude/project-thread-5ne2al): 이어하기 확인 — 저장된 런이 있을 때 "새로 시작"은 한 번 더 눌러야 시작 (따로 버리는 버튼은 새로 시작이 대신함)
- DONE (claude/project-thread-5ne2al): 적 머리 위 체력 바 — 맞으면 3초 동안 보이고 서서히 사라짐 (보스 제외)
