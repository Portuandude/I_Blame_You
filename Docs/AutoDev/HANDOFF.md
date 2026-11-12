# 인수인계 (2026-11-12 14:25 UTC)

- 브랜치/PR: claude/project-thread-41del8 → #48 (자동 머지 켬, editmode CI 대기)
- 하던 작업: 없음 (7개 끝). 작업 6개 초과로 `handoff` 규칙에 따라 새 실행으로 넘김
- 바로 다음 할 일: 1) #48이 머지됐는지 확인 (CI 실패면 고치기 — Unity 전용 새 파일 `UI/SkillHUD.{Village,Shop,OrbGuide}.cs`, `Stage/VillageBuilding.cs`, `Player/Equipment/LunchboxGuard.cs`는 로컬 컴파일이 안 됨) 2) BACKLOG "마을 다듬기" 5개를 위에서부터
- 건드린 파일: `Chunks/{RoomLayout,ClearPace}.cs`, `Stage/{StageEvents,ChunkStreamer,VillageBuilding,ArenaReward}.cs`, `Player/Equipment/Data/{VillageFood,EquipmentShop}.cs`, `Player/Input/RoleInput.cs`(InteractDown), `UI/SkillHUD.{Village,Shop,OrbGuide,EndScreen,Gold}.cs`, `UI/MinimapUI.cs`, `Tools/ArtGen/generate_village.py`
- 알아낸 것: 마을 창은 `villageOpen`으로 유물 창·장비 카드·일시정지·특성·도감과 서로 막는다(새 창을 만들면 같은 곳에 조건 추가, `CanOpenRelicWindow`). 상호작용 W는 "짧게 톡(떼는 순간)·잠긴 동안 누른 W는 무시"라 순간이동 W+Enter와 안 겹친다. 마을 방은 Rest 청크라 샘 방과 같은 청록 틴트를 받는다(꾸미기 작업에서). 진행도는 이제 `StageEvents.Depth`(화면에 안 보임)
- 환경 메모: 없음
