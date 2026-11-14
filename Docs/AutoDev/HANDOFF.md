# 인수인계 (2026-11-14 00:55 UTC)

- 브랜치/PR: `claude/project-thread-yrkr64` → 새 PR (develop 대상, GitHub 자동 머지 켬, #92 위에 쌓임, `editmode` CI 대기)
- 하던 작업: 없음 (바람 구멍 넓히기 5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #92·이번 PR이 머지됐으면 develop에서, 아직이면 이번 PR head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - `WindVent.LiftAt`·`Active`·`DescendSpeed`, 땅에 선 채로는 안 뜨는 규칙, `RoleInput.DescendHeld`, `PlayerSquash.Flutter`, `Projectile.BendInWind`, `WindVentFx.SpinCurl`
     - `ChunkGenerator.TryPlaceLedge(wind)`·`WantsWindLedge` (홀수 청크 번호 = 바람 선반), 테스트 `OddChunksGetWindLedge`
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: (이전 목록) + 통통 버섯 넓히기·튕김 연출 + 바람 구멍 + 바람 구멍 넓히기
     - 후보: 세로 방 모양 더 (사다리 대신 버섯 계단 등), 바람이 종유석 조각·골드 방울을 날리기, 낭떠러지 위 바람 (검증기에 바람 넣기 필요)
     - 기능 테스트 도구 창 키보드 조작은 BLOCKED. ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 로컬 NUnit은 2.6이라 `TestContext.WriteLine`이 없다 (메시지는 Assert 문구에 넣는다).
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (.wav에는 .meta를 만들지 않는다). `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말 것.
