# 인수인계 (2026-11-14 00:25 UTC)

- 브랜치/PR: `claude/project-thread-gim36q` → #89 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기)
- 하던 작업: 없음 (무너지는 발판 연출 5개 + 통통 버섯 덤 길 5개, 10개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #89가 머지됐으면 develop에서, 아직이면 #89 head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - 새 파일 `Systems/Effects/CrumblePlatformFx.cs` (`Splinters`·`Creak`·`Gather`·`Back`·`CameraNear`), `CrumblingPlatform.Pop`·`Darken`
     - 새 파일 `Systems/Traps/BounceMushroom.cs`, `FeatureKind.BounceMushroom`(enum 맨 끝), `ChunkGenerator.PlaceBounceLedge`(BuildPlains 끝, 난수 안 씀), `ChunkStreamer`·`TerrainDresser` 연결
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: (이전 목록) + 무너지는 발판 연출·통통 버섯
     - 후보: 통통 버섯을 다른 청크(언덕·휴식 곁)에도 / 버섯으로 적을 튕기기, 기능 테스트 구역에 버섯 넣기(FeatureTestStage), 기능 테스트 도구 창(F7/F9/F12, IMGUI) 키보드 조작
     - ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 플레이어 보통 점프 높이 약 2.45칸(jumpForce 12, gravityScale 3), 최대 낙하 20. 새 지형은 BuildX 끝에서 난수 없이 넣으면 기존 시드가 안 바뀐다. `unity_meta.py add`가 만든 부모 폴더 .meta(`Systems/Traps.meta`)는 지웠다.
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (.wav에는 .meta를 만들지 않는다). `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말 것.
