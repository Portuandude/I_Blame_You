# 인수인계 (2026-11-14 01:00 UTC)

- 브랜치/PR: `claude/project-thread-2m1ufe` → #94 (develop 대상, GitHub 자동 머지 켬, #93 위에 쌓임, `editmode` CI 대기)
- 하던 작업: 없음 (버섯 굴 선반·버섯 탑·꺾쇠·탑 동전·기획서 5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #93·#94가 머지됐으면 develop에서, 아직이면 #94 head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - `RoomShaper`: `AddMushroomShelf`(굴 방, 순간이동 선반이 없을 때), `ShaftStyle.MushroomTower`(enum 맨 끝)·`AddMushroomTower`·`TowerCol`(오른쪽은 15열 기준 거울)·`ClearTowerFloor`, `AddTeleportShelf`가 bool을 돌려주고 함정보다 먼저 불림
     - `StageValidator.BuildEdges`: 길 버섯(`BounceMushroom` + `Direction == 1`)에서 같은 열 위로 `TowerMushroomReach`(7)칸 오르는 간선
     - `BounceMushroom.Create(parent, feet, pathHint)` 꺾쇠 표시, `ChunkStreamer`가 `f.Direction == 1`을 넘김
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: (이전 목록) + 바람 구멍 넓히기 + 버섯 굴 선반·버섯 탑
     - 후보: 버섯 탑 변형(무너지는 선반 하나, 바람 기둥과 섞은 탑 — 검증기에 바람도 넣기), 가로 청크의 길 버섯(낭떠러지 위 섬을 버섯으로 잇기), 버섯 탑에 궁수 한 마리
     - 기능 테스트 도구 창 키보드 조작은 BLOCKED. ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 로컬 mcs는 지역 함수(local function)를 못 읽는다 → static 메서드로 쓴다. `ChunkFeature`는 struct라 `Features[i]`를 꺼내 고친 뒤 다시 넣어야 한다.
- 남은 LOW: 왼쪽 버섯 탑에서 쫄몹 둘이 옮겨지면 9·10열이 되어 아래 문 도착 칸(11)과 1칸 (300시드 중 2방).
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (.wav에는 .meta를 만들지 않는다). `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말 것.
