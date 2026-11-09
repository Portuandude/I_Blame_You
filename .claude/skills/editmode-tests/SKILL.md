---
name: editmode-tests
description: Unity 없이 EditMode 테스트(스테이지 생성·방 격자·피해 공식·유물)를 mono+NUnit으로 로컬 실행. 스테이지/방 생성 코드나 테스트를 고친 뒤, 푸시 전에 쓴다.
---

# editmode-tests — Unity 없이 순수 C# 테스트 돌리기

이 저장소의 EditMode 테스트는 UnityEngine을 쓰지 않는 어셈블리(`IBlameYou.StageGeneration`, `IBlameYou.Formula`, `IBlameYou.Relics`)만 시험하므로 mono로 바로 돌릴 수 있다.

```bash
bash Tools/Dev/run_editmode_tests.sh             # 전체 (지금 52개)
bash Tools/Dev/run_editmode_tests.sh RoomShaper  # 파일 이름으로 좁히기
```

- 처음 한 번은 apt로 mono/NUnit 2.6을 설치한다 (1~2분). 출력은 `Tests run:` 요약 줄과 실패 내용만 나온다. 종료 코드 0이면 통과.
- 테스트 어셈블리 이름은 `IBlameYou.Tests.EditMode`여야 한다 (`Chunks/AssemblyInfo.cs`의 `InternalsVisibleTo`).
- 새 순수 어셈블리를 테스트 asmdef 참조에 추가하면 스크립트의 `mcs` 줄도 하나 늘린다.
- UnityEngine을 쓰는 코드(MonoBehaviour 등)의 컴파일은 여기서 확인되지 않는다. 그건 CI `editmode` 잡이 본다.

## 결과를 남기는 법

PR 본문과 `Docs/AutoDev/LOG.md`에는 `로컬 52/52`처럼 숫자만 적는다. 실패 로그 전체를 붙이지 않는다.
