# 인수인계 (2026-11-12 22:50 UTC)

- 브랜치/PR: `claude/project-thread-60srw3` → #51 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). 7개 작업 끝 → handoff 규칙으로 새 실행에 넘김
- 하던 작업: 없음 (백로그 22:40 추가분 7개 모두 DONE, 리뷰 APPROVE)
- 바로 다음 할 일: 1) #51이 머지됐으면 develop에서, 아직이면 #51 head 위에서 시작 (CI 실패면 그것부터 고치기) 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이므로 GDD 로드맵·코드 상태로 3~5개 추가 (ContentIdeas·EquipmentDesign 열린 결정은 사람이 고르기 전에 손대지 않기)
- 건드린 파일: `Core/RunGold.cs`(OnVillageFound·LaunchCoins), `Core/RunStats.cs`(GoldSpentNow), `Core/SaveData.cs`(BestGold·도감 두 키), `UI/SkillHUD.Gold.cs`, `UI/MinimapUI.cs`(IsCharted), `Chunks/RelicCodex.cs`(long), `Chunks/MinimapRules.cs`(charted)
- 알아낸 것: 유물 30종(0~29). 도감 마스크는 이제 long 62종까지. 새 유물은 RelicId 맨 뒤에만
- 환경 메모: 로컬 테스트 `bash Tools/Dev/run_editmode_tests.sh` 162/162
