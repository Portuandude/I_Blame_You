# 인수인계 (2026-11-13 00:37 UTC)

- 브랜치/PR: `claude/project-thread-u9omgx` → #65 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #65는 #64 head 위에 쌓여 있다 (#63·#64도 자동 머지 대기). 마을 NPC 반응 5개 끝 → 새 실행에 넘김
- 하던 작업: 없음 (5개 모두 DONE, 리뷰 APPROVE, MEDIUM 1건 반영)
- 바로 다음 할 일: 1) #65가 머지됐으면 develop에서, 아직이면 #65 head 위에서 시작 (CI 실패면 그것부터 — 이번에 새로 쓴 곳: `GameAudio.PlaySfxPitched`·`PlayOneShot(basePitch)`, `VillageNpc`의 `CreateExclaim`·`ExclaimText`·`Greeted`·`ResetGreetings`·`SituationLine`·`Farewell`·`Speak`, `SkillHUD.cs`의 `VillageNpc.ResetGreetings()` 호출) 2) 백로그 TODO가 사람 판단(숨 고르기 방)과 BLOCKED뿐이므로 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응. 다른 주제로 (예: 시작 화면·결과 창 다듬기, 일시정지 창 다듬기, 보스 처치 연출). ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 건드린 파일: `Systems/MapGeneration/Stage/VillageNpc.cs`, `Systems/Audio/GameAudio.cs`, `UI/SkillHUD.cs`, `Tools/AudioGen/generate_audio.py`(NpcTalk), `Resources/Audio/SFX/NpcTalk.wav`
- 알아낸 것: 판 단위 정적 상태는 `SkillHUD` 초기화(`RunGold.Reset()` 옆)에서 지운다 — 스테이지 시드는 고정 시드 설정·기능 테스트(4242)에서 판마다 같다
- 환경 메모: 로컬 테스트 `bash Tools/Dev/run_editmode_tests.sh` (이번엔 해당 코드 안 바꿈). wav는 .meta를 만들지 않는다 (기존 효과음과 같음)
