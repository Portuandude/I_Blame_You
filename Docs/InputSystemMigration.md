# 새 Input System 이전 검토

> 2026-11-09 자동 개발 실행에서 작성. **문서만** 정리했고 코드는 바꾸지 않았다. 이전 작업은 사람이 확인한 뒤 진행한다.

## 현재 상태

- `Packages/manifest.json`에 `com.unity.inputsystem` 1.20.0이 이미 설치되어 있다.
- Project Settings > Player > Active Input Handling = **Both** (`activeInputHandler: 2`). 게임 코드는 전부 레거시 `UnityEngine.Input`을 쓴다.
- "Input System Package (New)" 단독으로 바꾸면 아래 호출이 모두 예외를 던져 입력이 동작하지 않는다.

## 레거시 Input 사용처

| 파일 | 호출 | 용도 |
| --- | --- | --- |
| `Player/Input/RoleInput.cs` (17곳) | `GetKey`, `GetKeyDown`, `GetMouseButtonDown(0)`, `mousePosition` | A/B 역할별 모든 게임 조작: 이동 A/D, 점프 Space, 달리기 Shift, 대쉬 LeftCtrl, 순간이동 방향키+Enter, 방어막 넘패드/상단 숫자, 미니게임 W/A/S/D, 평타·조준(마우스), 공격 스킬 숫자키 |
| `Core/DevCheats.cs` (4곳) | `GetKeyDown(F1~F4)` | 개발자 모드 치트 |
| `UI/SkillHUD.cs` (5곳) | `GetKeyDown(R/M)`, `imeCompositionMode`, `compositionString` | 게임오버/클리어 후 다시 시작, B의 영창 타이핑(한글 IME 조합) |
| `UI/SkillHUD.Pause.cs` (1곳) | `GetKeyDown(Escape)` | 일시정지 |
| `UI/SkillHUD.Traits.cs` (2곳) | `GetKeyDown(Tab/Escape)` | 특성 창 |
| `UI/*`, `Core/StartMenu.cs` | IMGUI (`OnGUI`, `Event.current`) | 버튼·슬라이더·영창 입력칸. IMGUI 이벤트는 두 입력 방식 모두에서 동작 |

좋은 점: 게임 조작이 거의 전부 `RoleInput` 한 곳에 모여 있어서, 이전의 대부분은 이 파일 하나를 바꾸는 일이다.

## 이전 계획 (제안)

1. **RoleInput 내부만 교체** (1단계, 동작 변화 없음)
   - `Keyboard.current[Key.X].isPressed / wasPressedThisFrame`, `Mouse.current.leftButton.wasPressedThisFrame`, `Mouse.current.position.ReadValue()`로 바꾼다.
   - 공개 메서드(`MoveAxis`, `JumpDown` …) 시그니처는 그대로 둬서 호출하는 쪽(PlayerMovement, PlayerCombat, 스킬)은 건드리지 않는다.
   - `Keyboard.current`가 null인 경우(키보드 없음) false를 돌려준다.
2. **DevCheats, SkillHUD의 단축키**도 같은 방식으로 교체.
3. **영창 타이핑(IME)**: 새 Input System의 `Keyboard.onTextInput`/`onIMECompositionChange`로 옮기거나, 지금처럼 IMGUI 텍스트 입력칸(`Event.current`)을 유지한다. 한글 조합 확인이 필요해 **사람이 직접 테스트**해야 한다.
4. 모두 옮긴 뒤 Active Input Handling을 "Input System Package (New)"로 바꾸고 README의 안내 문구를 고친다.
5. (선택, 나중) 키 재설정·게임패드·온라인 2인 분리를 위해 `InputActionAsset`으로 A/B 액션 맵을 만든다. 2인이 각자 기기를 쓰게 되면 이 단계가 필요하다.

## 위험 요소 / 사람 확인 필요

- 한글 IME 조합 중 Enter 처리 (`compositionString`이 비었을 때만 확정) — 새 시스템에서 같은 동작을 재현해야 한다.
- 넘패드 키(방어막)와 상단 숫자키 구분: `Key.Numpad1` vs `Key.Digit1`로 대응.
- B의 영창 중 A 키보드 잠금(`PlayerControlState.AKeyboardBlocked`)은 RoleInput 쪽 로직이라 그대로 유지된다.
- CI(EditMode)는 입력을 실행하지 않으므로 이전 후에는 **플레이테스트로만** 확인할 수 있다.
