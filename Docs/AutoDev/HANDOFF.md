# 인수인계 (2026-11-13 09:45 UTC)

- 브랜치/PR: `claude/project-thread-jji0wc` → #76 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #75(쓰러짐 연출 첫 푸시)·#74는 머지됨
- 하던 작업: 없음 (쓰러짐 연출 5개 + 작은 지도 2개 DONE)
- 바로 다음 할 일: 1) #76이 머지됐으면 develop에서, 아직이면 #76 head 위에서 시작. CI 실패면 그것부터 — 이번에 새로 쓴 곳: `UI/SkillHUD.Death.cs`의 `DrawDeathSpotlight`·`DeathSpotTexture`(런타임 Texture2D)·`TryDeathScreenPoint`·`DrawDeathSoul`·`BeginFall`·`UpdateFall`, `UI/MinimapUI.cs`의 `SmallSlideOffset`·`DrawOffMapMarks`·`DrawArrowMark` 2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①·코드 상태로 3~5개 추가. 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·HUD 알림 줄·화면 설정 창·큰 지도·(이번) 쓰러짐 연출·작은 지도. 후보: 기능 테스트 도구 창(F7/F9/F12) 키보드, 레벨 재설계 쪽 새 지형 변형, 적 공격 예고 다듬기. ContentIdeas·EquipmentDesign 열린 결정은 손대지 않기
- 알아낸 것: 플레이어 그림 회전은 HitStun 젖히기만 쓰고(죽으면 한 번 되돌림), 크기·위치는 PlayerSquash가 매 LateUpdate 덮어쓰므로 그림을 직접 움직이려면 PlayerSquash를 꺼야 한다. `UI/HUD/Star.png`는 하늘색(마나 별)이라 GUI.color 크림을 곱하면 색이 탁해진다. 효과음 .wav에는 .meta가 없다(원래 그렇다)
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름` (그 소리만 만든다)
