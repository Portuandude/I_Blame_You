# 자동 개발 실행 기록

최신 실행이 맨 위. 최근 3개만 두고 오래된 기록은 [LOG_ARCHIVE.md](LOG_ARCHIVE.md)로 옮긴다.

## 2026-11-09 17:40 UTC
- 브랜치/PR: `claude/project-thread-iz8mmy` → #19 (CI 통과 후 머지), #20 (CI 대기, 자동 머지 켜 둠)
- 완료 (7개, 전부 적대적 리뷰 APPROVE): 순간이동·방어막·베기 이펙트 다시 그리기(`generate_skill_fx.py`, 런타임 `Resources/Effects/{Teleport,Shield,Slash}`), `unity_meta.py add`가 런타임 PNG .meta도 생성, 유물 상자 열기(뚜껑·gold 빛기둥·별 + 이름 띠 `RelicBannerView`), 중간보스 등장(위에서 떨어져 충격파·흙먼지·투구 반짝임·흔들림), 보스 격파(`BossDefeatFx` 0.3배 슬로모션 0.35초·흰 번쩍임·큰 폭발·"격파!" 띠), 슬라임 킹 등장(`BossEntrance`로 일반화, 분열 몸은 제외 — 리뷰 HIGH 수정), 회복 하트 구슬(`HeartOrb`·`HeartPop`), 물 기사 방패 깨짐(`ShieldShatter` + 움찔). 백로그가 비어 비주얼 5개 추가
- 실패/보류: 17:51~18:00 UTC 사용량 한도로 잠깐 멈춤. 작업 6개를 넘겨 `handoff` 규칙대로 여기서 끝냄 (남은 TODO: HUD 체력·마나 바)
- 다음 실행 메모: 플레이테스트 필요: 새 스킬 이펙트 크기(베기·하트 구슬이 예전보다 조금 큼), 방어막 거품 투명도, 이름 띠의 한글 표시(LegacyRuntime 글꼴), 보스 낙하가 천장 낮은 방에서 어색하지 않은지, 격파 슬로모션 느낌, 슬라임 킹 분열이 예전처럼 동작하는지

## 2026-11-09 14:15 UTC
- 브랜치/PR: `claude/project-thread-d1pgr2` → #18 (develop 대상, CI 통과 후 머지)
- 완료: 순간이동 상하좌우 + 아래 낙사 방지(`TeleportRules` 순수 규칙 + 새 어셈블리 `IBlameYou.SkillRules`, 테스트 7개 — APPROVE, MEDIUM 1·LOW 2 반영), 적 공격 예고 squash(`EnemySquash`, 슬라임 판정 축소 방지 `SpriteColliderFitter.SetRestPose` — APPROVE), 체크포인트 aqua 빛기둥, 구역 돌파 별·하트 터짐 + 문 빛 번쩍임(APPROVE), 마법사 피격 2프레임(`PlayerHurtPose`, 런타임 로드라 에디터 툴 재실행 불필요 — 리뷰 MEDIUM: 메타 없는 300x256 PNG가 256으로 줄어 보일 수 있어 새 런타임 PNG 22장에 .meta 추가), 백로그가 비어 새 항목 5개 추가, 순간이동 지름길 선반(위 문 없는 이동 세로 방 50%, 바닥+5칸 선반 + 회복 구슬 `FeatureKind.Reward` — APPROVE·LOW 2 반영), 고블린 궁수 공격 예고(웅크림 + 화살 끝 반짝임). 로컬 EditMode 62/62
- 실패/보류: 기존 `Resources/Effects` PNG(LevelUp 120x240 등)도 .meta가 없어 NPOT 축소로 조금 찌그러져 보일 수 있음 — 사람 PC의 로컬 .meta와 충돌할까 봐 손대지 않음(사람 확인 필요). 순간이동 전용 경로는 만들지 않음 — 검증기는 그대로 걷기·점프 기준(마나 부족·플레이어 B 때문에 갇힐 수 있어서, `LevelDesign.md`에 규칙으로 적음). 대신 선택 지름길 선반을 백로그 맨 위에 추가
- 다음 실행 메모: 플레이테스트 필요: 아래 순간이동이 발판을 뚫고 안전하게 서는지·낭떠러지 위에서 거부되는지, 적 웅크림 세기, 빛기둥·터짐 크기, 피격 그림이 0.25초 동안 어색하지 않은지, 지름길 선반에 점프+↑순간이동으로 오르기 쉬운지

## 2026-11-09 13:41 UTC
- 브랜치/PR: `claude/project-thread-j06mzc` → #17 (develop 대상, CI 통과 후 머지)
- 완료: 레벨 재설계 검증(리뷰 APPROVE, 중간보스 자리를 최종 경로 진행도 기준으로 — 짧은 스테이지 편차 MEDIUM 수정, 테스트 추가), 방 전환 연출(문 지날 때 어둡게→0.15초 밝게, 방마다 배경 색조 — APPROVE·LOW 1 반영), 세로 방 함정(이동 탑 방 바닥 가시·불기둥, 문 앞·구멍 옆·맨 아래 발판 밑 제외 — APPROVE·LOW 2 반영), 대쉬·순간이동 잔상(APPROVE), 마법 투사체 잔상·방 문 빛 표시(APPROVE·LOW 2 반영), 플레이어 점프·착지 squash & stretch, 보스 등장 이름 띠. 로컬 EditMode 54/54
- 실패/보류: 벽 점프는 사람 결정 대기로 분리(BLOCKED). `Assets/Scripts/Systems/MapGeneration/Stage` 폴더 .meta가 저장소에 없음(기존 상태, Unity가 로컬에서 만듦 — 추가하면 사람 PC의 로컬 .meta와 충돌할 수 있어 그대로 둠)
- 다음 실행 메모: 비주얼 작업 4개를 백로그에 새로 추가함. 로컬 mono(mcs)는 지역 함수·다른 어셈블리 튜플 이름을 못 읽으니 Chunks·테스트에선 피할 것. 플레이테스트 필요: 방 전환 페이드 길이, 문 빛 표시 밝기, 잔상·squash 과하지 않은지, 탑 방 가시 위치
