# 인수인계 (2026-11-13 10:30 UTC)

- 브랜치/PR: `claude/project-thread-8qiaw2` → #84 (develop 대상, GitHub 자동 머지 켬, `editmode` CI 대기). #83 head 위에 쌓았고, #83은 #82 위에 있다
- 하던 작업: 없음 (슬라임 킹 촉수 연출 5개 + 착지 연출 5개 DONE, 리뷰 APPROVE)
- 바로 다음 할 일:
  1) #82·#83·#84가 머지됐으면 develop에서, 아직이면 #84 head 위에서 시작한다. CI가 실패했으면 그것부터 고친다. 이번에 새로 쓴 곳:
     - 새 파일 `Enemies/SlimeTentacleFx.cs` (`Lash`·`Slap`·`Grab`·`TetherPulse`·`Snap`)
     - 새 파일 `Enemies/SlimeKingLandFx.cs` (`Fall`·`Slam`·`Land`·젤리 얼룩), `SlimeKingController.glowSlamIndex`(Glow 루프·Interrupt에서 -1), `Impact` 끝·`Headbutt` 착지에서 호출
     - `SlimeKingTentacle`의 `TipScale` 상수, `Launch`·`Extend`(땅에 닿을 때만 Slap)·`TryCatch`·`UpdateTether`·`Release`(끝 구슬 크기·선 색 되돌림)
  2) 백로그 TODO가 사람 판단(숨 고르기 방)뿐이면 DIRECTION ①과 코드 상태를 보고 3~5개를 추가한다.
     - 끝난 주제: 이동 조작감·전투 손맛·전투 읽기·함정 읽기·소리 다듬기·전투 구역 연출·적 등장/보상 연출·방 입장 연출·레벨업/특성 연출·마을 NPC 반응·일시정지 창·시작 화면·보스 처치 연출·결과 창·유물 상자 창·특성 창·마을 창·장비 카드·유물 도감 창·HUD 알림 줄·화면 설정 창·큰 지도·쓰러짐 연출·작은 지도·적 공격 예고·슬라임 킹 패턴 예고·불 기사 돌진·물 기사 방패·쌍둥이 기사 광폭화·고블린 대전사 광폭화·슬라임 분열·슬라임 무리 합체·(이번) 슬라임 킹 촉수·슬라임 킹 착지
     - 후보: 기능 테스트 도구 창(F7/F9/F12) 키보드 조작, 레벨 재설계 쪽 새 지형 변형, 고블린 궁수 화살 날아감·꽂힘 연출, 어미 슬라임 점프 착지 연출
     - ContentIdeas·EquipmentDesign의 열린 결정은 손대지 않는다.
- 알아낸 것: 촉수 그랩(끌려감)은 `SlimeKingController.GrabRoutine`이 한다. 촉수는 사거리 끝에서 되감길 때와 벽에 닿아 되감길 때를 이제 구분한다(벽일 때만 소리).
- 환경 메모: 새 효과음은 `python3 Tools/AudioGen/generate_audio.py 이름`으로 만든다 (그 소리만 만든다). .wav에는 .meta를 만들지 않는다. `Enemies/Bosses/SlimeKing/`에는 새 .cs를 만들지 말고 `Enemies/` 아래에 둔다 (그 폴더 .cs의 .meta가 저장소에 없다).
