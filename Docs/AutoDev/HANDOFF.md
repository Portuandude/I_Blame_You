# 인수인계

없음 (2026-11-25 06:33 UTC 실행이 PR #158로 끝남).
다음 실행 메모: 백로그 남은 TODO는 사람 확인·아트 대기. 지난 두 실행에서 함정·소리·배경·이펙트·슬라임 왕·카메라·지도·HUD·유물·설정·쉼터·에디터·호스트 화면·평타·기력·장비·레벨·시작 화면·도시·친구 화면·전투 공통·봉인문·스위치·보상 구슬·유물 상자·출구·전투 구역을 점검했다.
아직 덜 본 곳: `Systems/Effects`(26개 중 일부만), `Systems/MapGeneration/Stage`의 마을 파일(VillageNpc·VillageBuilding·VillageDecor·VillageAmbience)·TerrainDresser·RoomDoorMarkers·StageEvents·StageRecords, `Player/Skills`의 SpellCaster 말고 나머지.
남은 LOW: 호스트 화면 전송 선·꼬리 곡선 할당·매 프레임 `FindObjectsByType` 4번(`Net/View/NetWorld.cs`, 측정 필요), 처음 보는 텍스처 PNG 인코딩 순간 멈춤(`NetAssets`), 도시·시작 화면 OnGUI 문자열 매 이벤트(메뉴만), 저주 상자 다시 잠든 뒤 위에 선 채 안 깨어남(Stay 쓰면 소환 실패 반복 위험).
