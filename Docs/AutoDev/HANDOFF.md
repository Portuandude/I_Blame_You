# 인수인계 (2026-11-14 08:20 UTC)

- 브랜치/PR: `claude/project-thread-gmpgdi` → #100 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #99는 머지됨
- 하던 작업: 없음. 이번 실행 7개 DONE (기능 테스트 이벤트 시험, 미니맵 이벤트 점, 수식어 줄 색, 벽 위 궁수 매복, 고블린 맞힘 금화, 매복 돌파 골드, 고블린 놓침 알림)
- 바로 다음 할 일:
  1) #100이 머지됐으면 develop에서, 아니면 #100 head 위에서 시작. CI가 실패했으면 그것부터 고친다.
  2) 남은 기획 결정(② 숲 스테이지 2, ③ 런 사이 도시, 퍼즐+추리 보상 방)은 사람이 순서를 정해야 함 → 손대지 않는다.
  3) 백로그 TODO가 없으면 DIRECTION ①(콘텐츠) 안에서 3~5개 추가. 후보:
     - 저주받은 상자 변형: 정예 대신 쫄몹 무리(3~4) 웨이브
     - 매복 변형 "땅속 매복": 슬라임이 바닥에서 튀어나옴 (바닥 흔들림 경고)
     - 보물 고블린이 도망칠 때 바닥에 동전 자국(길잡이)
     - 기능 테스트 F7에 "보물 고블린 + 저주 상자 같이" 같은 조합은 하지 않음 (필요 없음)
- 건드린 파일: `Core/FeatureTestTools.cs`(TestEvent, StartAmbush, PlaceCursedChest, StartArcherTower), `UI/MinimapUI.cs`(방 이벤트 점·범례), `Stage/ChunkStreamer.cs`(HasCursedChestLeft, HasTreasureGoblin, WallArchers 플래그), `UI/SkillHUD.EquipmentCard.cs`(StatLineText), `Chunks/RoomShaper.cs`(IsWallArcherAmbush, AddWallArchers), `Stage/ArenaZone.cs`(WallArchers, OnLedge, 매복 돌파 골드), `Chunks/AmbushRule.cs`(ClearGold), `TreasureGoblinController`(Hit 이벤트), `Core/RunGold.cs`(OnTreasureGoblinHit)
- 알아낸 것: 기능 테스트 맵은 방 격자가 아니라 방 이벤트·매복이 저절로 안 나온다 → F7/F8 이벤트로 시험. HealthSystem.Damaged는 체력을 깎은 뒤 불려서 죽는 타격은 IsDead로 거를 수 있다.
- 환경 메모: 로컬 mcs는 foreach 튜플 분해·지역 함수를 못 읽는다. `unity_meta.py add`가 만든 부모 폴더 .meta(추적 안 되는 것)는 지운다.
