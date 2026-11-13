# 인수인계 (2026-11-13 09:55 UTC)

- 브랜치/PR: `claude/project-thread-ssqyj3` → #77 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #76(작은 지도·쓰러짐 마무리) head 위에 쌓음
- 하던 작업: 없음 (적 공격 예고 5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일: 1) #76·#77이 머지됐으면 develop에서, 아직이면 #77 head 위에서 시작. CI 실패면 그것부터 — 이번에 새로 쓴 곳: 새 파일 `Enemies/EnemyTelegraph.cs`(static, `RunGold.RankOf`·`GameAudio`), `Systems/Effects/LandingMarkFx.cs`·`AimLineFx.cs`(런타임 스프라이트), `ManaRingFx.Create`/`VfxPlayer.ManaRing`의 `size` 인자, `SlimeController`의 `PredictLandingX`·`PlaceLandMark`(삼항 `default` RaycastHit2D), `GoblinController.SwingCenter`, `GoblinArcherController.UpdateAimLine` 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·HUD 알림 줄·화면 설정 창·큰 지도·쓰러짐 연출·작은 지도·(이번) 적 공격 예고. 후보: 기능 테스트 도구 창(F7/F9/F12) 키보드, 레벨 재설계 쪽 새 지형 변형, 슬라임 킹 패턴 예고 다듬기. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: 적 예고는 모두 `EnemySquash.Windup`을 부르는 자리(슬라임·고블린·궁수·KnightCharge)에서 시작한다. 적 루트 transform 크기는 ±1(좌우 뒤집기만), 그림 크기는 자식 Visual에 있다
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (그 소리만 만든다)
