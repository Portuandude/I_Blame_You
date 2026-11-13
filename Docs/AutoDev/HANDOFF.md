# 인수인계 (2026-11-13 11:05 UTC)

- 브랜치/PR: `claude/project-thread-bynbiy` → #86 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #85는 이미 머지됨
- 하던 작업: 없음 (고블린 휘두르기·함정 화살 / 가시·불기둥 맞음 / 낭떠러지 추락·복귀 / 봉인문 연출 5개씩, 20개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #86이 머지됐으면 develop에서, 아직이면 #86 head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - 새 파일 `Enemies/GoblinSwingFx.cs` (`Swing`·`Connected`·`Whiffed`), `GoblinController.Swing`·`IsEliteRank`
     - 새 파일 `Systems/Effects/TrapHitFx.cs` (`SpikeStab`·`FlameTouch`·`FlameBurst`·`FlameOut`), `SpikeTrap`·`FlameJetTrap`에서 호출. `ArrowTrap.Fire`가 `GoblinArrowFx` 공유
     - 새 파일 `Systems/Effects/FallRecoveryFx.cs` (`Falling`·`Returned` + 깜빡임 컴포넌트, `forceRenderingOff`), `FallRecovery`의 `fallSoundPlayed`·`FallSoundY`
     - 새 파일 `Puzzles/ElementGateFx.cs` (`SigilHit`·`Deflected`·`Shatter`), `ElementGate.Advance`·`Deflect`에서 호출
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·HUD 알림 줄·화면 설정 창·큰 지도·쓰러짐 연출·작은 지도·적 공격 예고·슬라임 킹 패턴 예고·불 기사 돌진·물 기사 방패·쌍둥이 기사 광폭화·고블린 대전사 광폭화·슬라임 분열·슬라임 무리 합체·슬라임 킹 촉수·슬라임 킹 착지·궁수 화살·슬라임 도약 착지·(이번) 고블린 휘두르기·함정 맞음·낭떠러지 복귀·봉인문
     - 후보: 스위치 발판(`Puzzles/PlatformSwitch`) 켜짐·꺼짐 연출, 움직이는 발판 출발·멈춤 연출, 무너지는 발판 다시 생길 때 연출, 기능 테스트 도구 창(F7/F9/F12, IMGUI) 키보드 조작, 레벨 재설계 쪽 새 지형 변형
     - ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 플레이어 몸 그림 깜빡임은 `forceRenderingOff`로 한다 (HitFlash는 머티리얼, GraceBlink는 색을 쓰므로 겹치지 않음). 창이 열리면 `Time.timeScale` 0이라 `Time.time`이 멈춘다 — 깜빡임은 그때 보이게 둘 것. 봉인문은 마지막 문양을 맞히면 바로 Destroy되므로 연출은 문에 붙이지 말 것.
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름`으로 만든다 (그 소리만 만든다, 난수는 전역 `RNG`를 써야 결정적). .wav에는 .meta를 만들지 않는다. `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말고 `Enemies/` 아래에 둔다.
