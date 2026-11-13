# 인수인계 (2026-11-13 10:00 UTC)

- 브랜치/PR: `claude/project-thread-2nbd1f` → #78 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #77(적 공격 예고) head 위에 쌓음
- 하던 작업: 없음 (슬라임 킹 패턴 예고 5개 + 불 기사 돌진 3개 DONE)
- 바로 다음 할 일: 1) #77·#78이 머지됐으면 develop에서, 아직이면 #78 head 위에서 시작. CI 실패면 그것부터 — 이번에 새로 쓴 곳: `SlimeKingController`의 `Squash(…, System.Action<float> eachFrame)`·`PlaceMark`·`HeadbuttVx`·`ClearTelegraphs`(필드 `slamMark`·`headbuttMark`·`tentacleLine`·`summonMarks`), `LandingMarkFx.Create`·`AimLineFx.Create`의 `Color? color` 인자, `KnightCharge`의 `UpdateDashLine`·`RemoveDashLine`·`Bump`(using `IBlameYou.Core` 추가) 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·HUD 알림 줄·화면 설정 창·큰 지도·쓰러짐 연출·작은 지도·적 공격 예고·(이번) 슬라임 킹 패턴 예고·불 기사 돌진. 후보: 기능 테스트 도구 창(F7/F9/F12) 키보드, 레벨 재설계 쪽 새 지형 변형, 물 기사(방패) 연출. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: `EnemySquash.Windup`은 웅크린 채 유지되므로 공격 순간 `Release()`를 불러야 풀린다. 킹은 `RunGold.RankOf`로 Boss라 `EnemyTelegraph.Windup`의 정예 고리는 안 나온다
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (그 소리만 만든다)
