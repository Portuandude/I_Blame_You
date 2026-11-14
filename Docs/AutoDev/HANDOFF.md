# 인수인계 (2026-11-14 08:30 UTC)

- 브랜치/PR: `claude/project-thread-c3ckyd` → #99 (develop 대상, 자동 머지 켬, `editmode` CI 대기)
- 하던 작업: 없음. 백로그 "기획안 결정" 중 바로 할 수 있는 8개 DONE (누구 탓 제거, 수식어 4종, 매복, 보물 고블린, 저주받은 상자, 궁수 탑, A/B 전환, 곁방 문구)
- 바로 다음 할 일:
  1) #99가 머지됐으면 develop에서, 아니면 #99 head 위에서 시작. CI가 실패했으면 그것부터 고친다.
  2) 남은 기획 결정(② 숲 스테이지 2, ③ 런 사이 도시, 퍼즐+추리 보상 방)은 사람이 순서를 정해야 함 → 손대지 않는다.
  3) 그 사이 할 일은 DIRECTION ①(콘텐츠) 안에서 3~5개 추가. 후보:
     - 기능 테스트 맵에 매복 방·저주받은 상자 방·궁수 탑 방 (F 키로 바로 시험)
     - 미니맵에 방 이벤트 표시(저주 상자 보라 점, 보물 고블린 금 점 — 들어간 뒤에만)
     - 상점 장비 카드에 수식어 마법 공격·마나 소모 줄 강조
     - 매복 변형: 궁수가 양옆 벽 위에서 등장
- 건드린 파일: `Chunks/AmbushRule|TreasureGoblinRule|CursedChestRule.cs`, `Stage/ArenaZone.cs`, `Stage/CursedChest.cs`, `ChunkStreamer.cs`(FindStandSpot, HasTerrainFeature, PlaceCursedChest, SpawnTreasureGoblin), `Enemies/Common/TreasureGoblin/`, `RoomShaper.AddArcherTower`, `Player/Input/RoleInput.cs`(Focus), `Core/FeatureTestTools.cs`, `Equipment/Data/EquipmentModifier.cs`
- 알아낸 것: 방 이벤트 규칙은 모두 시드+방 번호 해시(지형 난수 안 씀). 보물 고블린 방과 저주 상자 방은 겹치지 않게 함. 저주 정예가 엄마 슬라임이면 엄마가 죽을 때 상(분열 새끼는 별도). MinimapUI도 ` 키를 쓰지만 기능 테스트는 방 모드가 아니라 겹치지 않음.
- 환경 메모: 로컬 mcs는 foreach 튜플 분해·지역 함수를 못 읽는다. `unity_meta.py add`가 만든 부모 폴더 .meta(추적 안 되는 것)는 지운다.
