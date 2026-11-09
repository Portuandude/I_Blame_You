#!/usr/bin/env bash
# EditMode 테스트(순수 C# 어셈블리: 스테이지 생성, 피해 공식, 유물, 스킬 규칙)를 Unity 없이 mono + NUnit 2.6으로 돌린다.
#   bash Tools/Dev/run_editmode_tests.sh            → 전체
#   bash Tools/Dev/run_editmode_tests.sh RoomShaper → 이름에 RoomShaper가 들어간 테스트 파일만
# 처음 한 번은 mono/NUnit을 apt로 설치한다 (1~2분). 출력은 요약 줄과 실패만 보여 준다.
set -euo pipefail
cd "$(dirname "$0")/../.."

if ! command -v mcs >/dev/null || ! command -v nunit-console >/dev/null || [ ! -f /usr/lib/cli/nunit.framework-2.6.3/nunit.framework.dll ]; then
  echo "mono/NUnit 설치 중..."
  (apt-get update -qq && apt-get install -y -qq mono-mcs mono-runtime libnunit-framework2.6.3-cil nunit-console) >/dev/null 2>&1 \
    || { echo "설치 실패: apt를 쓸 수 없는 환경이면 CI(editmode) 결과로 확인"; exit 2; }
fi

OUT="${TMPDIR:-/tmp}/stage-tests"
mkdir -p "$OUT"
NUNIT=/usr/lib/cli/nunit.framework-2.6.3/nunit.framework.dll
# 테스트 어셈블리 이름은 AssemblyInfo.cs의 InternalsVisibleTo와 같아야 internal 멤버를 볼 수 있다
TESTS="IBlameYou.Tests.EditMode"

# 테스트 asmdef가 참조하는 순수 어셈블리들 (UnityEngine 없이 컴파일된다)
mcs -nologo -nowarn:0168,0219,0414 -target:library -out:"$OUT/IBlameYou.StageGeneration.dll" \
  Assets/Scripts/Systems/MapGeneration/Chunks/*.cs
mcs -nologo -nowarn:0168,0219,0414 -target:library -out:"$OUT/IBlameYou.Formula.dll" Assets/Scripts/Systems/Stats/Formula/*.cs
mcs -nologo -nowarn:0168,0219,0414 -target:library -out:"$OUT/IBlameYou.Relics.dll" Assets/Scripts/Player/Relics/Data/*.cs
mcs -nologo -nowarn:0168,0219,0414 -target:library -out:"$OUT/IBlameYou.SkillRules.dll" Assets/Scripts/Player/Skills/Rules/*.cs

FILTER="${1:-}"
FILES=()
for f in Assets/Tests/EditMode/*Tests.cs; do
  [ -f "$f" ] || continue
  if [ -z "$FILTER" ] || [[ "$f" == *"$FILTER"* ]]; then FILES+=("$f"); fi
done
[ ${#FILES[@]} -gt 0 ] || { echo "맞는 테스트 파일 없음: $FILTER"; exit 1; }

mcs -nologo -nowarn:0168,0219,0414 -target:library -r:"$OUT/IBlameYou.StageGeneration.dll" -r:"$OUT/IBlameYou.Formula.dll" -r:"$OUT/IBlameYou.Relics.dll" -r:"$OUT/IBlameYou.SkillRules.dll" -r:"$NUNIT" \
  -out:"$OUT/$TESTS.dll" "${FILES[@]}"

cd "$OUT"
export MONO_PATH="$OUT"
RESULT=$(nunit-console -nologo "$TESTS.dll" 2>&1 || true)
echo "$RESULT" | grep -E "^Tests run" || true
echo "$RESULT" | grep -E "^[0-9]+\) " -A4 || true
echo "$RESULT" | grep -qE "Errors: 0, Failures: 0" && exit 0 || exit 1
