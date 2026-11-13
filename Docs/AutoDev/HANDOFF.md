# 인수인계 (2026-11-13 09:30 UTC)

- 브랜치/PR: `claude/project-thread-24x112` → #74 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #73(HUD 알림 줄) head 위에 쌓음 (#73 → #72 → #71 순). 화면 설정 창·큰 지도 다듬기 10개 끝
- 하던 작업: 없음 (10개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일: 1) #72·#73·#74가 머지됐으면 develop에서, 아직이면 #74 head 위에서 시작. CI 실패면 그것부터 — 이번에 새로 쓴 곳: 새 파일 `Core/DisplaySettingsWindow.cs`(DisplaySettings.cs에서 옮김, `HandleKeys`·`Apply`·`Applying` 공개·`Close(bool)`), `UI/MinimapUI.cs`의 `SetExpanded`·`Expanded`·`ExpandScale`·`ExpandAmount`·`SmallMapCenter`·`FoundText`·`DrawPing`, `SkillHUD.Pause.UpdatePauseInput`(Applying 중 Esc 무시, 펼친 지도 Esc로 접기), `StartMenu`의 `displayWasOpen` 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·HUD 알림 줄·(이번) 화면 설정 창·큰 지도. 후보: 게임오버 직전 사망 연출(SkillHUD.Death), 기능 테스트 도구 창(F7/F9/F12) 키보드, 작은 미니맵 다듬기. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: 화면 설정 창은 일시정지 메뉴가 Update에서 Enter로 열기 때문에 같은 프레임 OnGUI 키를 `openedFrame`으로 무시한다. 큰 지도 OnGUI는 일시정지 중 그리지 않으므로 일시정지 중 접힘 연출은 안 보인다
- 환경 메모: 로컬 테스트 `bash Tools/Dev/run_editmode_tests.sh` (이번엔 해당 코드 안 바꿈)
