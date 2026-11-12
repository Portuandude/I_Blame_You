# 인수인계 (2026-11-12 14:55 UTC)

- 브랜치/PR: claude/project-thread-zpaf21 → #49 (자동 머지 켬, editmode CI 대기)
- 하던 작업: 없음 (10개 끝). 작업 6개 초과로 `handoff` 규칙에 따라 새 실행으로 넘김
- 바로 다음 할 일: 1) #49가 머지됐는지 확인 (CI 실패면 고치기 — Unity 전용 새 파일 `Stage/VillageDecor.cs`·`VillageNpc.cs`·`VillageAmbience.cs`, 바뀐 `UI/SkillHUD.{Village,Shop,Equipment,Gold}.cs`·`Effects/MedalGlintFx.cs`·`Core/RunStats.cs`는 로컬 컴파일이 안 됨) 2) 백로그 TODO가 사람 판단 대기 하나뿐이라, GDD "구현 로드맵"과 코드 상태로 다음 작업 3~5개를 `BACKLOG.md`에 먼저 추가 (Docs/ContentIdeas.md 후보는 사람이 번호를 고르기 전이면 고르지 말 것)
- 건드린 파일: `Stage/{ChunkStreamer(LoadRoomExtras 마을·EnterRoom 곡/첫 발견),StageEvents(VillageNpcSay),VillageDecor,VillageNpc,VillageAmbience}.cs`, `UI/SkillHUD.{Village,Shop,Equipment,Gold,cs}`, `Effects/{MedalGlintFx,VfxPlayer}.cs`, `Core/RunStats.cs`, `Tools/ArtGen/{generate_village,generate_food_icons}.py`, `Tools/AudioGen/generate_audio.py`
- 알아낸 것: 마을 창은 `Time.timeScale = 0`이라 창 위 연출은 실제 시간을 써야 보인다(`MedalGlintFx.CreateFromPath(..., unscaledTime)`). NPC 반응은 창이 닫힌 뒤 보인다. `EnterRoom`의 곡 if/else 사이에 다른 if를 끼우면 else가 그쪽에 묶인다(이번 리뷰 HIGH)
- 환경 메모: 기존 폴더 Stage에는 .meta가 없으니 `unity_meta.py add`가 만든 `Stage.meta`는 커밋하지 말 것
