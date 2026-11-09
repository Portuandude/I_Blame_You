---
name: unity-meta
description: Assets 아래에 새 .cs·.asmdef 파일이나 폴더를 만들었을 때 Unity .meta를 만들고 빠진 것을 확인. 커밋 전에 쓴다.
---

# unity-meta — 새 파일의 .meta 만들기

이 환경에는 Unity가 없어서 .meta를 직접 만들어야 한다. 손으로 GUID를 만들지 말고 스크립트를 쓴다.

```bash
python3 Tools/Dev/unity_meta.py add Assets/Scripts/Enemies/Bosses/NewBoss/NewBoss.cs   # 파일 + .meta 없는 상위 폴더까지
python3 Tools/Dev/unity_meta.py check          # 이번 작업(origin/develop 이후)에 새로 만든 것 중 빠진 .meta
python3 Tools/Dev/unity_meta.py add --missing  # check에 나온 것 전부 생성
```

- 커밋 직전 `check`가 "빠진 .meta 없음"이어야 한다.
- `check --all`에 나오는 **예전** 파일(SlimeKing, Chunks 등)은 사람 PC에서만 .meta가 생긴 것이다. 저장소에 새로 만들면 사람 PC의 추적 안 된 .meta와 충돌해 pull이 막히므로 만들지 않는다.
- `Assets/Resources/` 아래 런타임 로드 PNG와 그 폴더는 .meta 없이 두는 것이 관례다 (스크립트도 검사하지 않는다).
