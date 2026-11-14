# 인수인계 (2026-11-14 00:45 UTC)

- 브랜치/PR: `claude/project-thread-wqz11v` → 새 PR (develop 대상, GitHub 자동 머지 켬, #89 → #90 → #91 → 이번 PR로 쌓임, `editmode` CI 대기)
- 하던 작업: 없음 (바람 구멍 5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #90·#91·이번 PR이 머지됐으면 develop에서, 아직이면 이번 PR head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - 새 파일 `Systems/Traps/WindVent.cs` (`Create(parent, feet, width, height)`, `OnTriggerStay2D` 밀어 올림), `Systems/Effects/WindVentFx.cs` (`WindVentFx.Attach/Enter`, `WindParticle`)
     - `FeatureKind.WindVent`(enum 맨 끝, Width = 기둥 폭, Direction = 기둥 높이), `RoomShaper.AddWindShortcut`·`WantsWindShortcut`, `SafeTrapColumns`에서 환풍구 열 막음, `FeatureTestStage.AddWindVent`
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: (이전 목록) + 통통 버섯 넓히기·튕김 연출 + 바람 구멍
     - 후보: 바람 구멍을 가로 청크(낭떠러지 위 바람으로 건너기, 검증기에 바람 넣기 필요)·곁방에도 / 바람 구멍이 적 투사체·종유석 조각을 날리기 / 세로 방 모양 더 (사다리 대신 버섯 계단 등)
     - 기능 테스트 도구 창 키보드 조작은 BLOCKED. ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 세로 방 계단 판은 짝수 행(20, 18, …, 6)이라 새 선반은 홀수 행에 두면 `UnsteadyStairs` 테스트와 안 겹친다.
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (.wav에는 .meta를 만들지 않는다). `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말 것.
