# 인수인계 (2026-11-13 09:30 UTC)

- 브랜치/PR: `claude/project-thread-uxupno` → #73 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #72(유물 도감 창) head 위에 쌓음 (#72는 #71 위). HUD 알림 줄 다듬기 5개 끝
- 하던 작업: 없음 (5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일: 1) #71·#72·이번 PR이 머지됐으면 develop에서, 아직이면 이번 PR head 위에서 시작. CI 실패면 그것부터 — 이번에 새로 쓴 곳: 새 파일 `UI/SkillHUD.Feed.cs`(`FeedEntry`가 struct→class, `AddFeed`·`UpdateFeed`·`FeedSlotY`·`DrawFeed`·`DrawFeedCount`·`FeedShake`·`EaseOutBack`이 SkillHUD.cs에서 옮겨 옴), `UI/SkillHUD.Notice.cs`의 `NoticeRect`·`PushOldNotice`·`DrawOldNotice`·`DrawNoticeRibbon` 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·(이번) HUD 알림 줄. 후보: 화면 설정 창 키보드, 게임오버 창 다듬기, 미니맵 펼친 보기(` 키) 연출. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: 피드에 줄을 넣을 때는 `feed.Add` 대신 `AddFeed(text, color, bad)` (같은 줄 합치기·MaxFeed 처리 포함)
- 환경 메모: 로컬 테스트 `bash Tools/Dev/run_editmode_tests.sh` (이번엔 해당 코드 안 바꿈)
