# 인수인계 (2026-11-14 08:40 UTC)

- 브랜치/PR: `claude/project-thread-lla9qi` → #102 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기, #101 head 위에 쌓음)
- 하던 작업: 없음. 이번 실행 3개 DONE (보물 고블린 장비 떨굼, 깊은 방 무리 정예, 섞인 매복)
- 바로 다음 할 일:
  1) #101·#102가 머지됐으면 develop에서, 아니면 열린 PR 중 마지막 head 위에서 시작. CI가 실패했으면 그것부터 고친다.
  2) 남은 기획 결정(② 숲 스테이지 2, ③ 런 사이 도시, 퍼즐+추리 보상 방)은 사람이 순서를 정해야 함 → 손대지 않는다.
  3) 백로그 TODO가 없으면 DIRECTION ①(콘텐츠) 안에서 3~5개 추가. 후보:
     - 궁수 탑 변형: 난이도 80%+ 세로 방에서 궁수 하나를 고블린 병사(방패)로
     - 저주 상자를 쫄몹 무리로 풀면 골드 조금 더 (무리 처치 보너스)
     - 미니맵: 보물 고블린이 떨군 장비가 남은 방에 작은 점
     - 매복 방 돌파 시간 기록 (빠르면 골드 +4)
- 건드린 파일: `Chunks/TreasureGoblinRule.cs`(DropsEquipment), `Chunks/CursedChestRule.cs`(SwarmFor 난이도 오버로드), `Chunks/AmbushRule.cs`(IsMixed, BurrowsAt), `Stage/ChunkStreamer.cs`(DropGoblinEquipment, goblinEquipmentSpots, Mixed 설정), `Stage/ArenaZone.cs`(Mixed, 경고 루프 하나로), `Core/FeatureTestTools.cs`(F8 매복 3단계)
- 알아낸 것: 보물 고블린 방에는 유물 상자·저주 상자가 없어 groundEquipment 키(방 번호)를 같이 써도 겹치지 않는다. 고블린이 도망칠 때는 Destroy만 해서 HealthChanged(0)가 오지 않는다.
- 환경 메모: 로컬 mcs는 foreach 튜플 분해·지역 함수를 못 읽는다. `unity_meta.py add`가 만든 부모 폴더 .meta(추적 안 되는 것)는 지운다.
