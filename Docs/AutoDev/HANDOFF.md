# 인수인계 (2026-11-12 23:25 UTC)

- 브랜치/PR: `claude/project-thread-3647un` → #53 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #53은 #52 head 위에 쌓여 있어 #52가 먼저 머지되어야 깔끔하다. 7개 작업 끝 → handoff 규칙으로 새 실행에 넘김
- 하던 작업: 없음 (조작감 7개 모두 DONE, 리뷰 APPROVE)
- 바로 다음 할 일: 1) #53이 머지됐으면 develop에서, 아직이면 #53 head 위에서 시작 (CI 실패면 그것부터 고치기 — 이번에 Unity API를 새로 쓴 곳: `PlayerMovement` BoxCast/OverlapBox(List 오버로드), `CameraFollow` Mathf.SmoothDamp 6인자) 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이므로 DIRECTION ①·코드 상태로 3~5개 추가 (ContentIdeas·EquipmentDesign 열린 결정은 사람이 고르기 전에 손대지 않기)
- 건드린 파일: `Player/PlayerMovement.cs`(코요테·버퍼·낮은 점프·모서리 보정·최대 낙하), `Player/Input/RoleInput.cs`(JumpHeld), `Core/CameraFollow.cs`(lookX/lookY)
- 알아낸 것: 지형은 CompositeCollider2D라 캐스트가 콜라이더당 hit 하나만 준다 → 자리 확인은 OverlapBox로. 달리기 점프 최대 약 8칸(코요테 포함), 스위치 낭떠러지 9~10칸
- 환경 메모: 로컬 테스트 `bash Tools/Dev/run_editmode_tests.sh` (이번엔 해당 코드 안 바꿈). 옛 mcs라 C# 7 지역 함수 못 씀
