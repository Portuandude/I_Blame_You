# 인수인계 (2026-11-14 08:45 UTC)

- 브랜치/PR: `claude/project-thread-6u2cnc` → #101 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #100은 머지됨
- 하던 작업: 없음. 이번 실행 4개 DONE (저주 상자 쫄몹 무리, 땅속 매복, 보물 고블린 동전 자국, 저주 상자 빠른 해제)
- 바로 다음 할 일:
  1) #101이 머지됐으면 develop에서, 아니면 #101 head 위에서 시작. CI가 실패했으면 그것부터 고친다.
  2) 남은 기획 결정(② 숲 스테이지 2, ③ 런 사이 도시, 퍼즐+추리 보상 방)은 사람이 순서를 정해야 함 → 손대지 않는다.
  3) 백로그 TODO가 없으면 DIRECTION ①(콘텐츠) 안에서 3~5개 추가. 후보:
     - 매복 변형 섞기: 땅속 + 천장 동시 (난이도 70%+ 방, 적 절반씩)
     - 보물 고블린이 가끔 동전 대신 장비 하나를 떨어뜨림 (잡았을 때 10%)
     - 저주 상자 쫄몹 무리에 정예 하나 섞기 (난이도 70%+)
     - 기능 테스트 F7 목록에 "보물 고블린" 이벤트 (지금은 몬스터 소환으로만)
- 건드린 파일: `Chunks/CursedChestRule.cs`(IsSwarm, SwarmFor, SwarmOffsets, QuickTime, OfferFor), `Stage/CursedChest.cs`(Watch params, swarm 문구, 빠른 해제 초 표시, Break가 후보 수 반환), `ChunkStreamer.cs`(AwakenCursedSwarm, AnyAlive, cursedQuick, Burrow 설정), `Chunks/AmbushRule.cs`(IsBurrow), `Stage/ArenaZone.cs`(Burrow, GroundRumble), `Enemies/Common/TreasureGoblin/TreasureGoblinTrail.cs`(새 파일), `Core/FeatureTestTools.cs`
- 알아낸 것: 낭떠러지 아래로 떨어진 적은 죽지 않고 치워진다(HealthChanged 안 옴) → 여러 마리 처치 판정은 카운터 대신 "살아 있는 것(non-null && !IsDead)이 남았나"로. EnemyController.Move는 y 속도를 유지해서 소환 직후 위로 튀기기가 먹힌다.
- 환경 메모: 로컬 mcs는 foreach 튜플 분해·지역 함수를 못 읽는다. `unity_meta.py add`가 만든 부모 폴더 .meta(추적 안 되는 것)는 지운다.
