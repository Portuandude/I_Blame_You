# 인수인계 (2026-11-13 10:10 UTC)

- 브랜치/PR: `claude/project-thread-jamtdv` → #80 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #79(물 기사 방패) head 위에 쌓음, #79는 #78 위
- 하던 작업: 없음 (쌍둥이 기사 짝 잃음·광폭화 연출 5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일: 1) #78·#79·이 PR이 머지됐으면 develop에서, 아직이면 이 PR head 위에서 시작. CI 실패면 그것부터 — 이번에 새로 쓴 곳: 새 파일 `Enemies/Bosses/SlimeKnights/KnightEnrageFx.cs`(`using IBlameYou.Core` CameraFollow, `SolidSpriteFactory.CreateCircle`, `VfxPlayer.ManaRing`), `SlimeKnight.UpdateEnrageFx`·`ReleaseBossNotice`(`Rigidbody2D.linearVelocity`, `HitStop.Play`, `BossEvents.Notice`), `EnrageAbility.Remaining` 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·HUD 알림 줄·화면 설정 창·큰 지도·쓰러짐 연출·작은 지도·적 공격 예고·슬라임 킹 패턴 예고·불 기사 돌진·물 기사 방패·(이번) 쌍둥이 기사 광폭화. 후보: 기능 테스트 도구 창(F7/F9/F12) 키보드, 레벨 재설계 쪽 새 지형 변형, 고블린 대전사 광폭화도 같은 김·남은 초 연출(`KnightEnrageFx` 재사용 가능), 슬라임 킹 분열/합체 연출. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: `BossEvents.Notice`는 슬라임 킹 `SlimeBrood`도 쓰므로 쓰는 쪽이 자기가 쓴 것만 지운다 (`SlimeKnight.bossNoticeOwner`). `EnrageAbility.Update`는 죽으면 바로 return이라 `Active`가 죽은 뒤에도 true로 남는다
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (그 소리만 만든다). .wav에는 .meta를 만들지 않는다(기존과 같음)
