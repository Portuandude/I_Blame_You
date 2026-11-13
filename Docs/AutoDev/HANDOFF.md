# 인수인계 (2026-11-13 09:15 UTC)

- 브랜치/PR: `claude/project-thread-z8f3og` → #72 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #71(마을 창·장비 카드) head 위에 쌓음. 유물 도감 창 다듬기 5개 끝
- 하던 작업: 없음 (5개 DONE)
- 바로 다음 할 일: 1) #71·#72가 머지됐으면 develop에서, 아직이면 #72 head 위에서 시작. CI 실패면 그것부터 — #72에서 새로 쓴 곳: `Core/RelicCodexWindow.cs`의 `Show(dev, owned)`·`Close(playSound)`·`Draw`→`DrawWindow`(try/finally로 GUI.matrix 복원)·`PanelScale`·`IconPopScale`·`OwnedNow`·`DrawOwnedDot`·`DrawProgressBar`·`OwnerTally`, `SkillHUD.Codex.CloseCodex(playSound)` 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·(#72) 유물 도감 창. 후보: HUD 알림 줄 다듬기, 미니맵 확대 보기(M 또는 Tab 길게), 화면 설정 창 키보드. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: 도감 창은 시작 화면·게임 중 공용이라 소리·연출은 `RelicCodexWindow` 안에 두고, 쓰러짐·클리어로 닫힐 때는 `Close()`(소리 없음)
- 환경 메모: 로컬 테스트 `bash Tools/Dev/run_editmode_tests.sh` (이번엔 해당 코드 안 바꿈)
