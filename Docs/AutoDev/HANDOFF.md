# 인수인계 (2026-11-14 10:25 UTC)

- 브랜치/PR: `claude/project-thread-67c9id` → #106 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기, #105 head 위에 쌓음)
- 하던 작업: 없음. 이번 실행 5개 DONE (방 이벤트 적 자리 머리 위 3칸, 세로 방 보물 고블린 바닥 근처에서 나타남, 금화 소리 힌트, 미니맵 기다림 점, 저주 상자 다시 잠듦 알림)
- 바로 다음 할 일:
  1) #105·#106이 머지됐으면 develop에서, 아니면 #106 head 위에서 시작. CI가 실패했으면 그것부터 고친다.
  2) 남은 기획 결정(② 숲 스테이지 2, ③ 런 사이 도시, 퍼즐+추리 보상 방, 전투 방 최소 수)은 사람 판단 → 손대지 않는다.
  3) 백로그 TODO가 없으면 DIRECTION ①(콘텐츠) 안에서 후보:
     - 기능 테스트에서 세로 방 보물 고블린 시험 (F8로 "기다리는 고블린" — 바닥 9칸 안으로 내려오면 나타남)
     - 저주 상자를 깨운 채 떠난 방은 미니맵 보라 점을 깜빡이게 (다시 잠들었음을 지도에서도)
     - 할 일이 사람 결정뿐이면 새 실행을 걸지 말고 요약에 "사람 결정 대기"를 적고 멈춘다 (연출 다듬기는 지금 수준 유지가 사람 결정)
- 건드린 파일: `Stage/ChunkStreamer.cs`(FindStandSpot headroom·HeadClear·EnemyHeadroom, goblinWaiting·WakeWaitingGoblin·HasWaitingTreasureGoblin, cursedLeftAwake), `UI/MinimapUI.cs`(기다리는 고블린 점)
- 알아낸 것: 방 이벤트 적은 SpawnLift 1.2만큼 띄워 만들어서 바닥 위 2칸만 비어 있으면 세로 방 첫 발판(바닥+2)에 몸이 박힌다. 세로 방(32칸 폭) 바닥에서 위 문까지 22칸.
- 환경 메모: `unity_meta.py add`가 만든 부모 폴더 .meta(추적 안 되는 것)는 지운다. 로컬 mcs는 foreach 튜플 분해·지역 함수를 못 읽는다.
