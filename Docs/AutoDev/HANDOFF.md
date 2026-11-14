# 인수인계 (2026-11-14 01:05 UTC)

> 2026-11-14 01:05 UTC: 사람 요청으로 자동 개발 일시 정지 (기획안 다듬기). 그 사이 기능 테스트 맵 "버섯 탑 방"만 추가 (`claude/project-thread-5nx76k`). 재개할 때 기획 결정부터 백로그에 반영할 것.

- 브랜치/PR: `claude/project-thread-ix71zl` → #95 (develop 대상, GitHub 자동 머지 켬, #94 위에 쌓임, `editmode` CI 대기)
- 하던 작업: 없음 (무너지는 버섯 탑·저격 선반·기획서 DONE, 리뷰 APPROVE, LOW 주석 2개 고침)
- 바로 다음 할 일:
  1) #94·#95가 머지됐으면 develop에서, 아직이면 #95 head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - `RoomShaper.TowerCrumbles`(난이도 ≥ CrumbleStairsMinDifficulty → P2가 `FeatureKind.Crumble`), `TowerHasSniper`·`AddTowerSniper`·`TowerSniperX0/X1`(19~22, 거울 8~11, 행 g+TowerStep-1, 궁수는 탑 반대쪽 벽에 가까운 끝)
     - 테스트 `MushroomTowersClimbOnlyByMushrooms`가 무너지는 P2·저격 선반을 확인
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: (이전 목록) + 버섯 굴 선반·버섯 탑 + 버섯 탑 변형(무너지는 P2·저격 궁수)
     - 후보: 가로 청크의 길 버섯(낭떠러지 위 섬을 버섯으로 잇기 — `StageValidator`가 Direction 1 버섯을 세로 간선으로만 앎), 바람 기둥과 섞은 탑(검증기에 바람 넣기), 기능 테스트 맵에서 버섯 탑을 시험할 방법
     - 기능 테스트 도구 창 키보드 조작은 BLOCKED. ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 무너진 P2의 안쪽 2칸(왼쪽 탑 10~11열)에서 떨어지면 P1을 못 만나 바닥부터 다시 오른다 (의도로 둠).
- 환경 메모: 로컬 mcs는 지역 함수를 못 읽는다. 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (.wav에는 .meta를 만들지 않는다). `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말 것.
