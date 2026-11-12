# 인수인계 (2026-11-12 14:20 UTC)

- 브랜치/PR: claude/project-thread-54keuu → #47 (자동 머지 켬, editmode CI 대기)
- 하던 작업: 없음 (7개 끝). 작업 6개 초과로 `handoff` 규칙에 따라 새 실행으로 넘김
- 바로 다음 할 일: 1) #47이 머지됐는지 확인 (CI 실패면 고치기 — Unity 전용 새 파일 `UI/SkillHUD.{Equipment,EquipmentCard}.cs`·`Stage/EquipmentPickup.cs`·`Player/Equipment/PlayerEquipment.cs`·`Core/FeatureTestTools.cs`는 로컬 컴파일이 안 됨) 2) BACKLOG "진행도 시스템 삭제" 3) 마을 → 식당 → 장비 상점 (`Docs/EquipmentDesign.md` 5·6·9·10절에 공식·표가 있음, 계산은 이미 `EquipmentItem.Price/SellPrice/RerollCost/Reroll`)
- 건드린 파일: `Player/Equipment/{PlayerEquipment.cs,Data/*}`, `Systems/{Health,Mana,Stamina}` AddMax, `Stage/{EquipmentPickup,ChunkStreamer(SpawnRelicChest·groundEquipment)}.cs`, `UI/SkillHUD.{Equipment,EquipmentCard,Pause,Relics,Codex,Traits}.cs`·`SkillHUD.cs`, `Core/FeatureTestTools.cs`, 테스트 `EquipmentTests.cs`, `Tools/Dev/run_editmode_tests.sh`(장비 어셈블리 줄)
- 알아낸 것: 순간이동에는 재사용 대기가 없다(유물 "종이비행기" 백로그 항목도 같은 문제, 대체 효과 필요). 장비 카드가 열려 있는지는 `equipCardOpen`으로 유물 창·일시정지·특성·도감과 서로 막는다(새 창을 만들면 같은 곳에 조건을 더할 것). 기존 폴더 Stage에는 .meta가 없으니 `unity_meta.py add`가 만든 `Stage.meta`는 커밋하지 말 것
- 환경 메모: 없음
