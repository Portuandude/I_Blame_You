# 인수인계 (2026-11-13 15:50 UTC)

- 브랜치/PR: `claude/project-thread-ahr3bv` → #88 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기)
- 하던 작업: 없음 (깜빡 발판 연출 5개 + 움직이는 발판 도착·출발 연출 5개, 10개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #88이 머지됐으면 develop에서, 아직이면 #88 head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - 새 파일 `Systems/Effects/BlinkPlatformFx.cs` (`Vanish`·`Appear`·`Underfoot`·`CloudRise`·`CloudFade` + 솜 조각 컴포넌트), `BlinkPlatform.PlayFx`(상태 Visible/Cloud/Gone 비교)·`UpdatePop`·`OnCollisionStay2D`
     - 새 파일 `Systems/Effects/MovingPlatformFx.cs` (`Arrive`·`Depart`), `MovingPlatform.UpdateEndFx`·`Dip`·`OnCollisionEnter2D`·`UpdateDepartWarn` (LateUpdate가 이제 일반 발판에서도 돈다)
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: (이전 목록) + 스위치 발판·깜빡 발판·움직이는 발판 연출
     - 후보: 무너지는 발판 다시 생길 때 연출(지금은 `Crumble_Back` + 반짝 하나), 기능 테스트 도구 창(F7/F9/F12, IMGUI) 키보드 조작, 레벨 재설계 쪽 새 지형 변형, 스프링(점프대) 튕김 연출
     - ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 상태 변화 연출은 "처음 프레임·OnEnable 뒤 첫 프레임은 상태만 기억"(primed 플래그)으로 해야 방 스트리밍 때 헛연출이 없다. 깜빡 발판은 `Time.time` 박자라 일시정지 중엔 바뀌지 않지만 솜구름 유물 확인은 `unscaledTime`이라 timeScale 0 체크가 필요.
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (.wav에는 .meta를 만들지 않는다). `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말 것.
