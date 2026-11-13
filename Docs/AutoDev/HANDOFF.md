# 인수인계 (2026-11-13 09:12 UTC)

- 브랜치/PR: `claude/project-thread-cph33a` → #71 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #70(유물 상자 창·특성 창) head 위에 쌓음. 마을 창·장비 카드 다듬기 5개 끝
- 하던 작업: 없음 (5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일: 1) #70·#71이 머지됐으면 develop에서, 아직이면 #71 head 위에서 시작. CI 실패면 그것부터 — #71에서 새로 쓴 곳: `SkillHUD.Village.cs`의 `UpdateVillageRowKeys`·`VillageKeysReady`·`VillageConfirmKey`·`VillageKeysHint`·`VillageFlash`·`VillageGoldShake`·`DrawVillageRowFlash`·`DrawVillageGold`·`CloseVillage(keepLock, playSound)`·`DrawVillageBackdrop`(이제 인스턴스), `SkillHUD.Shop.UpdateShopKeys`·`ShopKeyRows`, `SkillHUD.EquipmentCard.UpdateEquipCardInput`의 ←→ 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·(#71) 마을 창·장비 카드. 후보: 도감(C) 창 열림 연출·키보드, HUD 알림 줄 다듬기, 미니맵 확대 보기. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: 마을 창 키보드는 W·↑가 "닫기"와 겹쳐서, ↓로 고르기를 시작하기 전엔 닫기, 시작한 뒤엔 위로 옮기기로 나눴다. 골드 표시는 이제 창 왼쪽 위 `DrawVillageGold` (안내 글엔 없음)
- 환경 메모: 로컬 테스트 `bash Tools/Dev/run_editmode_tests.sh` (이번엔 해당 코드 안 바꿈)
