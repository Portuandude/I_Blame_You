# 데모 빌드

2026-11-23 사람 요청 "데모버전 생성". 친구와 하는 베타 테스트(컴퓨터 두 대, 호스트 방식 온라인 — [OnlinePlay.md](OnlinePlay.md))에 쓸 Windows 데모.
나중에 스팀 체험판(데모)도 같은 방식으로 굽는다.

## 데모에서 달라지는 것 (`IBY_DEMO`)

- 시작 화면에 **개발자 모드 토글·기능 테스트 버튼이 없다**. `GameSession.DevMode`는 늘 false라 F1~F4 치트, 숫자 즉시 발동, 도시 F1 가루도 없다.
- 시작 버튼 이름이 "게임 시작" / "새로 시작".
- 시작 화면 패널 오른쪽 아래에 **"데모 0.1"** (`GameSession.DemoVersion`). 친구와 같은 버전인지 이걸로 맞춘다.
- 제품 이름이 **"I Blame You! Demo"** 라서 저장(PlayerPrefs)·창 제목이 개발용 빌드와 따로 간다. 예전 개발자 모드로 하던 판은 이어하지 않는다.
- 내용(스테이지·방·적·보스)은 develop 그대로. 이야기 건너뛰기 임시 키 F12도 남겨 둔다 (베타용).

## 만드는 법

### A. 내 컴퓨터에서 (Unity)

1. GitHub Desktop에서 develop **Pull**.
2. Unity에서 `Tools > I Blame You > 데모 빌드 만들기 (친구에게 줄 zip)`.
   그림·프리팹 생성(Generate Sprites And Animations + Generate Prefabs)도 이 메뉴가 먼저 돌린다.
3. 끝나면 탐색기가 `Builds/Demo/IBlameYou_Demo_0.1.zip`을 보여 준다. 이 zip을 친구에게 보낸다 (안에 `읽어 주세요.txt`).

### B. GitHub에서 (Unity 없이)

1. 저장소 **Actions** 탭 > **Demo Build** > **Run workflow** (branch: develop).
2. 20~40분 뒤 그 실행 화면 아래 **Artifacts**의 `IBlameYou_Demo_0.1-<커밋>`을 받는다 (안에 zip). 30일 보관.
3. `release-*` 브랜치 푸시나 `demo-*` 태그로도 저절로 만들어진다.

## 새 데모를 줄 때

- `GameSession.DemoVersion`을 올린다 (0.1 → 0.2). zip 이름·시작 화면 표시가 같이 바뀐다.
- 온라인은 두 사람이 **같은 빌드**여야 한다. 새 zip을 친구에게도 꼭 같이 보낸다.
