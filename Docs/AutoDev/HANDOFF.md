# 인수인계 (2026-11-14 10:22 UTC)

- 브랜치/PR: `claude/project-thread-z9975f` → #105 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #104는 머지됨
- 하던 작업: 없음. 이번 실행 6개 DONE (F8 궁수 탑 파수꾼, 보물 고블린·저주 상자 세로 방 버그, 섞인 매복·무리 정예 문턱, 비율 테스트, 결과 창 방 이벤트 줄, 저주 상자 자리)
- 바로 다음 할 일:
  1) #105가 머지됐으면 develop에서, 아니면 #105 head 위에서 시작. CI가 실패했으면 그것부터 고친다.
  2) 남은 기획 결정(② 숲 스테이지 2, ③ 런 사이 도시, 퍼즐+추리 보상 방)과 "전투 방 수"는 사람 판단 → 손대지 않는다.
  3) 백로그 TODO가 없으면 DIRECTION ①(콘텐츠) 안에서 3~5개 추가. 후보:
     - 세로 방 보물 고블린: 위·아래 문으로 들어올 때 고블린 자리·도망 방향이 자연스러운지 코드로 확인하고 다듬기
     - 세로 방 저주 상자: 첫 발판 바로 밑이면 정예가 발판에 걸리지 않는지 (FindStandSpot에 머리 위 3칸 비움 조건?)
     - 새 방 이벤트 후보를 정할 때는 반드시 `RoomEventFrequencyTests`에 넣어 60시드에서 나오는지 확인
- 건드린 파일: `Chunks/RoomEventRule.cs`(새), `Chunks/CursedChestRule.cs`(IsCursedChestShaft, SwarmEliteDifficulty 0.5), `Chunks/RoomShaper.cs`(IsWallArcherAmbush에 !IsBurrow), `Stage/ChunkStreamer.cs`(RoomEventRule 사용, EventTally, FindStandSpot avoidEnemies), `UI/SkillHUD.Pause.cs`(RoomEventText), `UI/SkillHUD.EndScreen.cs`(이벤트 줄), `Core/FeatureTestTools.cs`(towerCount)
- 알아낸 것: 4×3 격자에서 남는 이동 방은 **전부 세로 방**(위·아래 문, 32칸 폭)이고, 전투 방은 깊이 0.1~0.3·0.8~0.9에만, 판마다 1~1.5개. 바닥 막힌 세로 방은 깊이 0.6 미만뿐. "가로 이동 방만"·"깊이 0.7+" 같은 조건은 실제로 0번일 수 있으니 새 변형은 60시드로 세어 본다.
- 환경 메모: `unity_meta.py add`가 만든 부모 폴더 .meta(추적 안 되는 것)는 지운다. 로컬 mcs는 foreach 튜플 분해·지역 함수를 못 읽는다.
