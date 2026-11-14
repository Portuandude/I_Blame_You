# 인수인계 (2026-11-14 00:35 UTC)

- 브랜치/PR: `claude/project-thread-7ql488` → #90 (develop 대상, GitHub 자동 머지 켬, #89 head 위에 쌓음, `editmode` CI 대기)
- 하던 작업: 없음 (통통 버섯 넓히기 4개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #89·#90이 머지됐으면 develop에서, 아직이면 #90 head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - `ChunkGenerator.BounceReachGuard`·`AddBounceLedge`(public, 기능 테스트 맵용)·`PlaceTwinPathMushroom`, `BuildHills` 끝에서 `PlaceBounceLedge`
     - `BounceMushroom.TryBounceEnemy` (적 튕김, 중력 0 제외), `FeatureTestStage.BounceArea = 1`
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: (이전 목록) + 통통 버섯 넓히기
     - 기능 테스트 도구 창 키보드 조작은 BLOCKED (게임을 멈추지 않는 창이라 Enter·↑·Space와 겹침). 하려면 `control.Locked`를 쓰는 멈춤 창으로 바꾸는 것부터
     - ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 지형 덤은 BuildX 끝에서 난수 없이, `occupied`를 보고 놓으면 기존 시드가 안 바뀐다. 가시 줄은 `FeatureKind.Spikes`(X, Width).
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (.wav에는 .meta를 만들지 않는다). `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말 것.
